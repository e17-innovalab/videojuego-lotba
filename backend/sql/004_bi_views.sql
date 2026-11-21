-- Migration 004: BI / Dashboard views layer over 003 analytics views.
-- Idempotent (CREATE OR REPLACE VIEW), compatible with schemaVersion 13 and 19.
-- Security notes:
--   * All views use security_invoker = true.
--   * service_role is intended for backend/testing only.
--   * For future external BI (e.g. Looker Studio), create a dedicated read-only PostgreSQL role;
--     never expose the Supabase Secret Key in Looker Studio or any external BI tool.

-- 1. Session-level BI overview (one row per session)
CREATE OR REPLACE VIEW public.vw_bi_session_overview
WITH (security_invoker = true) AS
SELECT
    (s.started_at_utc AT TIME ZONE 'UTC')::date AS session_date_utc,
    s.started_at_utc,
    s.finished_at_utc,
    s.run_id,
    s.game_version,
    s.schema_version,
    s.platform,
    s.route,
    s.ending,
    s.braked_voluntarily,
    (s.ending = 'route_complete') AS completed_route,
    (s.ending = 'overload') AS overloaded,
    s.time_seconds AS duration_seconds,
    s.distance_metres,
    s.peak_speed_kmh,
    s.score,
    s.gems,
    s.gem_balance,
    s.collisions,
    s.boost_collisions,
    (COALESCE(s.collisions, 0) > 0) AS had_collision,
    s.decisions_shown,
    s.decisions_kept,
    s.decisions_accepted,
    s.decisions_unresolved,
    s.decision_acceptance_pct,
    s.voluntary_boosts,
    s.manual_boosts,
    s.gems_lost,
    s.gems_spent,
    s.speed_upgrades,
    s.purchased_speed_kmh,
    s.pink_gems_collected,
    s.pink_gems_missed,
    s.pink_gem_near_misses,
    s.best_pink_gem_streak,
    s.pink_dodge_cleared,
    s.pink_dodge_collisions,
    CASE
        WHEN s.schema_version >= 19 THEN (COALESCE(s.pink_gem_near_misses, 0) > 0)
        ELSE NULL
    END AS had_casi
FROM public.vw_sessions_summary s;

-- 2. Behavior after loss per session (one row per EVERY session, including 0 adverse events)
CREATE OR REPLACE VIEW public.vw_bi_behavior_metrics
WITH (security_invoker = true) AS
SELECT
    s.run_id,
    s.schema_version,
    s.ending,
    COALESCE(agg.adverse_events_count, 0)::integer AS adverse_events_count,
    COALESCE(agg.obstacle_hits_count, 0)::integer AS obstacle_hits_count,
    COALESCE(agg.pink_gem_missed_count, 0)::integer AS pink_gem_missed_count,
    COALESCE(agg.continued_playing_count, 0)::integer AS continued_playing_count,
    COALESCE(agg.progressed_distance_count, 0)::integer AS progressed_distance_count,
    COALESCE(agg.took_speed_decision_after_count, 0)::integer AS took_speed_decision_after_count,
    COALESCE(agg.activated_boost_after_count, 0)::integer AS activated_boost_after_count,
    COALESCE(agg.finished_within_5s_count, 0)::integer AS finished_within_5s_count,
    agg.avg_distance_progress_after_loss,
    agg.avg_seconds_to_next_event
FROM public.vw_bi_session_overview s
LEFT JOIN (
    SELECT
        b.run_id,
        COUNT(*) AS adverse_events_count,
        COUNT(*) FILTER (WHERE b.adverse_type = 'obstacle_hit') AS obstacle_hits_count,
        COUNT(*) FILTER (WHERE b.adverse_type = 'pink_gem_missed') AS pink_gem_missed_count,
        COUNT(*) FILTER (WHERE b.continued_playing) AS continued_playing_count,
        COUNT(*) FILTER (WHERE b.progressed_after_loss) AS progressed_distance_count,
        COUNT(*) FILTER (WHERE b.took_speed_decision_after) AS took_speed_decision_after_count,
        COUNT(*) FILTER (WHERE b.activated_boost_after) AS activated_boost_after_count,
        COUNT(*) FILTER (WHERE b.finished_within_5s) AS finished_within_5s_count,
        ROUND(AVG(b.distance_progress_after_loss)::numeric, 2) AS avg_distance_progress_after_loss,
        ROUND(AVG(b.seconds_to_next_event)::numeric, 3) AS avg_seconds_to_next_event
    FROM public.vw_behavior_after_loss b
    GROUP BY b.run_id
) agg ON agg.run_id = s.run_id;

-- 3. Speed decisions per session (one row per EVERY session, including 0 decisions)
-- acceptance_pct uses resolved_decisions as denominator (NULL when resolved_decisions = 0).
CREATE OR REPLACE VIEW public.vw_bi_speed_decisions
WITH (security_invoker = true) AS
SELECT
    s.run_id,
    s.schema_version,
    COALESCE(agg.total_decisions, 0)::integer AS total_decisions,
    COALESCE(agg.resolved_decisions, 0)::integer AS resolved_decisions,
    COALESCE(agg.unresolved_decisions, 0)::integer AS unresolved_decisions,
    COALESCE(agg.accepted_decisions, 0)::integer AS accepted_decisions,
    COALESCE(agg.rejected_decisions, 0)::integer AS rejected_decisions,
    CASE
        WHEN COALESCE(agg.resolved_decisions, 0) > 0
            THEN ROUND(100.0 * COALESCE(agg.accepted_decisions, 0) / agg.resolved_decisions, 2)
        ELSE NULL
    END AS acceptance_pct,
    agg.avg_speed_at_offer,
    agg.max_speed_at_offer,
    agg.avg_purchased_speed_kmh,
    COALESCE(agg.speed_kept_count, 0)::integer AS speed_kept_count,
    COALESCE(agg.speed_upgrade_purchased_count, 0)::integer AS speed_upgrade_purchased_count,
    COALESCE(agg.boost_activated_offer_count, 0)::integer AS boost_activated_offer_count
FROM public.vw_bi_session_overview s
LEFT JOIN (
    SELECT
        d.run_id,
        COUNT(*) AS total_decisions,
        COUNT(*) FILTER (WHERE d.resolved) AS resolved_decisions,
        COUNT(*) FILTER (WHERE NOT d.resolved) AS unresolved_decisions,
        COUNT(*) FILTER (WHERE d.accepted IS TRUE) AS accepted_decisions,
        COUNT(*) FILTER (WHERE d.accepted IS FALSE) AS rejected_decisions,
        ROUND(AVG(d.speed_kmh_at_offer)::numeric, 2) AS avg_speed_at_offer,
        ROUND(MAX(d.speed_kmh_at_offer)::numeric, 2) AS max_speed_at_offer,
        ROUND(
            AVG(d.purchased_speed_kmh) FILTER (WHERE d.outcome_type = 'speed_upgrade_purchased')::numeric,
            2
        ) AS avg_purchased_speed_kmh,
        COUNT(*) FILTER (WHERE d.outcome_type = 'speed_kept') AS speed_kept_count,
        COUNT(*) FILTER (WHERE d.outcome_type = 'speed_upgrade_purchased') AS speed_upgrade_purchased_count,
        COUNT(*) FILTER (WHERE d.outcome_type = 'boost_activated') AS boost_activated_offer_count
    FROM public.vw_speed_decisions d
    GROUP BY d.run_id
) agg ON agg.run_id = s.run_id;

-- 4. Explicit view permissions (restrict public/client access; backend/testing service_role only)
REVOKE ALL ON
    public.vw_bi_session_overview,
    public.vw_bi_behavior_metrics,
    public.vw_bi_speed_decisions
FROM PUBLIC, anon, authenticated;

GRANT SELECT ON
    public.vw_bi_session_overview,
    public.vw_bi_behavior_metrics,
    public.vw_bi_speed_decisions
TO service_role;
