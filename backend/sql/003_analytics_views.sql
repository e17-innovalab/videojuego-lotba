-- Migration 003: First analytics views layer over public.sessions and public.session_events.
-- Idempotent (CREATE OR REPLACE VIEW), compatible with schemaVersion 13 and 19.
-- Uses security_invoker = true (PostgreSQL 15+) so views enforce caller permissions and RLS.

-- 1. Session-level summary (one row per session; real accepted/unresolved decisions from events)
CREATE OR REPLACE VIEW public.vw_sessions_summary
WITH (security_invoker = true) AS
SELECT
    s.id,
    s.run_id,
    s.started_at_utc,
    s.finished_at_utc,
    s.game_version,
    s.schema_version,
    s.platform,
    s.route,
    s.ending,
    (s.ending = 'brake') AS braked_voluntarily,
    s.time_seconds,
    s.distance_metres,
    s.peak_speed_kmh,
    s.score,
    s.gems,
    s.collisions,
    s.boost_collisions,
    s.decisions_shown,
    s.decisions_kept,
    COALESCE(ev.decisions_accepted, 0)::integer AS decisions_accepted,
    GREATEST(
        0,
        COALESCE(s.decisions_shown, 0) - COALESCE(s.decisions_kept, 0) - COALESCE(ev.decisions_accepted, 0)
    )::integer AS decisions_unresolved,
    CASE
        WHEN COALESCE(s.decisions_shown, 0) > 0
            THEN ROUND(100.0 * COALESCE(ev.decisions_accepted, 0) / NULLIF(s.decisions_shown, 0), 2)
        ELSE NULL
    END AS decision_acceptance_pct,
    s.voluntary_boosts,
    s.manual_boosts,
    s.gems_lost,
    s.gems_spent,
    s.gem_balance,
    s.speed_upgrades,
    s.purchased_speed_kmh,
    s.pink_gems_collected,
    s.pink_gems_missed,
    s.pink_gem_near_misses,
    s.best_pink_gem_streak,
    s.pink_dodge_cleared,
    s.pink_dodge_collisions
FROM public.sessions s
LEFT JOIN LATERAL (
    SELECT
        COUNT(DISTINCT e.decision_number) FILTER (
            WHERE COALESCE(e.decision_number, 0) > 0
              AND e.type IN ('speed_upgrade_purchased', 'boost_activated')
        ) AS decisions_accepted
    FROM public.session_events e
    WHERE e.session_id = s.id
) ev ON true;

-- 2. Speed decisions (one row per decision_number > 0; excludes manual turbo button)
CREATE OR REPLACE VIEW public.vw_speed_decisions
WITH (security_invoker = true) AS
WITH decision_keys AS (
    SELECT DISTINCT
        e.session_id,
        e.decision_number
    FROM public.session_events e
    WHERE COALESCE(e.decision_number, 0) > 0
      AND e.type IN ('speed_offer_shown', 'speed_kept', 'speed_upgrade_purchased', 'boost_activated')
),
offers AS (
    SELECT DISTINCT ON (e.session_id, e.decision_number)
        e.session_id,
        e.decision_number,
        e.sequence AS offer_sequence,
        e.time_seconds AS offer_time_seconds,
        e.distance_metres AS offer_distance_metres,
        e.zone,
        e.speed_kmh AS speed_kmh_at_offer
    FROM public.session_events e
    WHERE COALESCE(e.decision_number, 0) > 0
      AND e.type = 'speed_offer_shown'
    ORDER BY e.session_id, e.decision_number, e.sequence ASC
),
outcomes AS (
    SELECT DISTINCT ON (e.session_id, e.decision_number)
        e.session_id,
        e.decision_number,
        e.sequence AS outcome_sequence,
        e.type AS outcome_type,
        e.purchased_speed_kmh,
        e.gem_delta
    FROM public.session_events e
    WHERE COALESCE(e.decision_number, 0) > 0
      AND e.type IN ('speed_kept', 'speed_upgrade_purchased', 'boost_activated')
    ORDER BY e.session_id, e.decision_number, e.sequence DESC
)
SELECT
    s.run_id,
    s.schema_version,
    dk.session_id,
    dk.decision_number,
    of.offer_sequence,
    of.offer_time_seconds,
    of.offer_distance_metres,
    of.zone,
    of.speed_kmh_at_offer,
    oc.outcome_sequence,
    oc.outcome_type,
    oc.purchased_speed_kmh,
    oc.gem_delta,
    (oc.outcome_sequence IS NOT NULL) AS resolved,
    CASE
        WHEN oc.outcome_type IN ('speed_upgrade_purchased', 'boost_activated') THEN true
        WHEN oc.outcome_type = 'speed_kept' THEN false
        ELSE NULL
    END AS accepted
FROM decision_keys dk
JOIN public.sessions s ON s.id = dk.session_id
LEFT JOIN offers of ON of.session_id = dk.session_id AND of.decision_number = dk.decision_number
LEFT JOIN outcomes oc ON oc.session_id = dk.session_id AND oc.decision_number = dk.decision_number;

-- 3. Post-adverse-event behavior (one row per obstacle_hit or pink_gem_missed)
CREATE OR REPLACE VIEW public.vw_behavior_after_loss
WITH (security_invoker = true) AS
WITH ordered_events AS (
    SELECT
        e.*,
        LEAD(e.type) OVER (PARTITION BY e.session_id ORDER BY e.sequence) AS next_event_type,
        LEAD(e.time_seconds) OVER (PARTITION BY e.session_id ORDER BY e.sequence) AS next_event_time_seconds
    FROM public.session_events e
)
SELECT
    s.run_id,
    s.schema_version,
    s.ending AS session_ending,
    e.session_id,
    e.sequence AS adverse_sequence,
    e.type AS adverse_type,
    e.detail AS adverse_detail,
    e.zone,
    e.time_seconds AS adverse_time_seconds,
    e.distance_metres AS adverse_distance_metres,
    after_stats.max_distance_after,
    CASE
        WHEN after_stats.max_distance_after IS NOT NULL AND e.distance_metres IS NOT NULL
            THEN ROUND((after_stats.max_distance_after - e.distance_metres)::numeric, 3)
        ELSE NULL
    END AS distance_progress_after_loss,
    COALESCE(after_stats.max_distance_after > e.distance_metres, false) AS progressed_after_loss,
    e.gem_delta,
    e.score_delta,
    e.pink_gem_streak_before,
    e.pink_gem_near_miss,
    e.pink_dodge_encounter,
    e.next_event_type,
    ROUND((e.next_event_time_seconds - e.time_seconds)::numeric, 3) AS seconds_to_next_event,
    ROUND((s.time_seconds - e.time_seconds)::numeric, 3) AS seconds_remaining_in_run,
    (
        COALESCE(after_stats.has_gameplay_events_after, false)
        OR COALESCE(after_stats.max_distance_after > e.distance_metres, false)
    ) AS continued_playing,
    COALESCE(after_stats.took_speed_decision_after, false) AS took_speed_decision_after,
    COALESCE(after_stats.activated_boost_after, false) AS activated_boost_after,
    ((s.time_seconds - e.time_seconds) <= 5.0) AS finished_within_5s
FROM ordered_events e
JOIN public.sessions s ON s.id = e.session_id
LEFT JOIN LATERAL (
    SELECT
        MAX(e2.distance_metres) AS max_distance_after,
        BOOL_OR(e2.type NOT IN ('run_paused', 'run_resumed', 'run_finished')) AS has_gameplay_events_after,
        BOOL_OR(
            e2.type IN ('speed_kept', 'speed_upgrade_purchased')
            OR (e2.type = 'boost_activated' AND COALESCE(e2.decision_number, 0) > 0)
        ) AS took_speed_decision_after,
        BOOL_OR(e2.type = 'boost_activated') AS activated_boost_after
    FROM public.session_events e2
    WHERE e2.session_id = e.session_id
      AND e2.sequence > e.sequence
) after_stats ON true
WHERE e.type IN ('obstacle_hit', 'pink_gem_missed');

-- 4. Daily UTC metrics rollup (based on vw_sessions_summary)
CREATE OR REPLACE VIEW public.vw_daily_metrics
WITH (security_invoker = true) AS
SELECT
    (started_at_utc AT TIME ZONE 'UTC')::date AS session_date_utc,
    COUNT(*) AS total_sessions,
    COUNT(*) FILTER (WHERE ending = 'brake') AS brake_sessions,
    COUNT(*) FILTER (WHERE ending = 'route_complete') AS route_complete_sessions,
    COUNT(*) FILTER (WHERE ending = 'overload') AS overload_sessions,
    ROUND(100.0 * COUNT(*) FILTER (WHERE ending = 'brake') / NULLIF(COUNT(*), 0), 2) AS brake_pct,
    ROUND(AVG(time_seconds)::numeric, 2) AS avg_duration_seconds,
    ROUND(AVG(peak_speed_kmh)::numeric, 2) AS avg_peak_speed_kmh,
    ROUND(AVG(collisions)::numeric, 2) AS avg_collisions,
    COALESCE(SUM(decisions_shown), 0) AS decisions_shown,
    COALESCE(SUM(decisions_accepted), 0) AS decisions_accepted,
    COALESCE(SUM(decisions_unresolved), 0) AS decisions_unresolved,
    ROUND(
        100.0 * COALESCE(SUM(decisions_accepted), 0) / NULLIF(COALESCE(SUM(decisions_shown), 0), 0),
        2
    ) AS acceptance_rate_pct,
    COALESCE(SUM(pink_gems_collected), 0) AS pink_gems_collected,
    COALESCE(SUM(pink_gems_missed), 0) AS pink_gems_missed,
    COALESCE(SUM(pink_gem_near_misses), 0) AS pink_gem_near_misses
FROM public.vw_sessions_summary
WHERE started_at_utc IS NOT NULL
GROUP BY (started_at_utc AT TIME ZONE 'UTC')::date
ORDER BY session_date_utc DESC;

-- 5. Explicit view permissions (restrict public/client access; backend/service_role only)
REVOKE ALL ON
    public.vw_sessions_summary,
    public.vw_speed_decisions,
    public.vw_behavior_after_loss,
    public.vw_daily_metrics
FROM PUBLIC, anon, authenticated;

GRANT SELECT ON
    public.vw_sessions_summary,
    public.vw_speed_decisions,
    public.vw_behavior_after_loss,
    public.vw_daily_metrics
TO service_role;
