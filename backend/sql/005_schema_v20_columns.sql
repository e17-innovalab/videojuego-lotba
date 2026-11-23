-- Migration 005: Add schemaVersion 20 columns (progressive braking, recovery boosts, turbo configuration)
-- and update ingest_session_report RPC + summary/BI session views.
-- Incremental, idempotent, and backwards-compatible with schemaVersion 13 and 19 payloads.
-- Security note: Do NOT expose ingest_session_report() or views directly to the Unity client
-- with privileged credentials. Must be invoked exclusively by the Backend service_role.

-- 1. Add schemaVersion 20 columns to public.sessions (nullable for historical compatibility)
ALTER TABLE public.sessions
    ADD COLUMN IF NOT EXISTS braking_mode text,
    ADD COLUMN IF NOT EXISTS braking_deceleration_mps2 double precision,
    ADD COLUMN IF NOT EXISTS braking_release_acceleration_mps2 double precision,
    ADD COLUMN IF NOT EXISTS recovery_boosts integer,
    ADD COLUMN IF NOT EXISTS brake_presses integer,
    ADD COLUMN IF NOT EXISTS brake_releases integer,
    ADD COLUMN IF NOT EXISTS brake_held_seconds double precision;

-- 2. Add schemaVersion 20 event columns to public.session_events (nullable for historical compatibility)
ALTER TABLE public.session_events
    ADD COLUMN IF NOT EXISTS turbo_duration_seconds double precision,
    ADD COLUMN IF NOT EXISTS turbo_speed_bonus_mps double precision,
    ADD COLUMN IF NOT EXISTS turbo_gem_multiplier integer,
    ADD COLUMN IF NOT EXISTS turbo_point_multiplier integer;

-- 3. Update transactional ingestion function (preserves signature, SECURITY INVOKER, and schema 13/19 compatibility)
CREATE OR REPLACE FUNCTION public.ingest_session_report(payload jsonb)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY INVOKER
AS $$
DECLARE
    v_metadata jsonb;
    v_braking jsonb;
    v_summary jsonb;
    v_run_id text;
    v_session_id uuid;
BEGIN
    v_metadata := COALESCE(payload->'metadata', '{}'::jsonb);
    v_braking := COALESCE(v_metadata->'braking', '{}'::jsonb);
    v_summary := COALESCE(payload->'summary', '{}'::jsonb);
    v_run_id := NULLIF(btrim(v_metadata->>'runId'), '');

    IF v_run_id IS NULL THEN
        RAISE EXCEPTION 'metadata.runId is required';
    END IF;

    INSERT INTO public.sessions (
        run_id,
        report_version,
        schema_version,
        game_version,
        platform,
        started_at_utc,
        finished_at_utc,
        route,
        seed,
        route_distance_metres,
        ending,
        time_seconds,
        distance_metres,
        peak_speed_kmh,
        score,
        gems,
        collisions,
        voluntary_boosts,
        bonus_gems,
        manual_boosts,
        recovery_attempts,
        decisions_shown,
        decisions_kept,
        gems_lost,
        gem_balance,
        boost_collisions,
        gems_spent,
        speed_upgrades,
        purchased_speed_kmh,
        pink_gems_collected,
        pink_gems_missed,
        pink_gem_near_misses,
        best_pink_gem_streak,
        pink_dodge_cleared,
        pink_dodge_collisions,
        braking_mode,
        braking_deceleration_mps2,
        braking_release_acceleration_mps2,
        recovery_boosts,
        brake_presses,
        brake_releases,
        brake_held_seconds
    )
    VALUES (
        v_run_id,
        NULLIF(btrim(v_metadata->>'reportVersion'), ''),
        (v_metadata->>'schemaVersion')::integer,
        v_metadata->>'gameVersion',
        v_metadata->>'platform',
        NULLIF(btrim(v_metadata->>'startedAtUtc'), '')::timestamptz,
        NULLIF(btrim(v_metadata->>'finishedAtUtc'), '')::timestamptz,
        v_metadata->>'route',
        (v_metadata->>'seed')::integer,
        (v_metadata->>'routeDistanceMetres')::double precision,
        v_summary->>'ending',
        (v_summary->>'timeSeconds')::double precision,
        (v_summary->>'distanceMetres')::double precision,
        (v_summary->>'peakSpeedKmh')::double precision,
        (v_summary->>'score')::bigint,
        (v_summary->>'gems')::integer,
        (v_summary->>'collisions')::integer,
        (v_summary->>'voluntaryBoosts')::integer,
        (v_summary->>'bonusGems')::integer,
        (v_summary->>'manualBoosts')::integer,
        (v_summary->>'recoveryAttempts')::integer,
        (v_summary->>'decisionsShown')::integer,
        (v_summary->>'decisionsKept')::integer,
        (v_summary->>'gemsLost')::integer,
        (v_summary->>'gemBalance')::integer,
        (v_summary->>'boostCollisions')::integer,
        (v_summary->>'gemsSpent')::integer,
        (v_summary->>'speedUpgrades')::integer,
        (v_summary->>'purchasedSpeedKmh')::double precision,
        (v_summary->>'pinkGemsCollected')::integer,
        (v_summary->>'pinkGemsMissed')::integer,
        (v_summary->>'pinkGemNearMisses')::integer,
        (v_summary->>'bestPinkGemStreak')::integer,
        (v_summary->>'pinkDodgeCleared')::integer,
        (v_summary->>'pinkDodgeCollisions')::integer,
        NULLIF(btrim(v_braking->>'mode'), ''),
        (v_braking->>'decelerationMetresPerSecondSquared')::double precision,
        (v_braking->>'releaseAccelerationMetresPerSecondSquared')::double precision,
        (v_summary->>'recoveryBoosts')::integer,
        (v_summary->>'brakePresses')::integer,
        (v_summary->>'brakeReleases')::integer,
        (v_summary->>'brakeHeldSeconds')::double precision
    )
    ON CONFLICT (run_id) DO NOTHING
    RETURNING id INTO v_session_id;

    IF v_session_id IS NULL THEN
        RETURN jsonb_build_object(
            'status', 'already_exists',
            'runId', v_run_id
        );
    END IF;

    INSERT INTO public.session_events (
        session_id,
        sequence,
        type,
        zone,
        detail,
        time_seconds,
        distance_metres,
        speed_kmh,
        lane,
        score,
        score_delta,
        gems_collected,
        gem_balance,
        gem_delta,
        during_boost,
        decision_number,
        purchased_speed_kmh,
        pink_gem_streak,
        score_multiplier,
        pink_gem_streak_before,
        pink_gem_near_miss,
        pink_dodge_encounter,
        turbo_duration_seconds,
        turbo_speed_bonus_mps,
        turbo_gem_multiplier,
        turbo_point_multiplier
    )
    SELECT
        v_session_id,
        (ev->>'sequence')::integer,
        NULLIF(btrim(ev->>'type'), ''),
        ev->>'zone',
        ev->>'detail',
        (ev->>'timeSeconds')::double precision,
        (ev->>'distanceMetres')::double precision,
        (ev->>'speedKmh')::double precision,
        (ev->>'lane')::integer,
        (ev->>'score')::bigint,
        (ev->>'scoreDelta')::bigint,
        (ev->>'gemsCollected')::integer,
        (ev->>'gemBalance')::integer,
        (ev->>'gemDelta')::integer,
        (ev->>'duringBoost')::boolean,
        (ev->>'decisionNumber')::integer,
        (ev->>'purchasedSpeedKmh')::double precision,
        (ev->>'pinkGemStreak')::integer,
        (ev->>'scoreMultiplier')::integer,
        (ev->>'pinkGemStreakBefore')::integer,
        (ev->>'pinkGemNearMiss')::boolean,
        (ev->>'pinkDodgeEncounter')::integer,
        ((ev->'turboConfiguration')->>'durationSeconds')::double precision,
        ((ev->'turboConfiguration')->>'speedBonusMetresPerSecond')::double precision,
        ((ev->'turboConfiguration')->>'gemMultiplier')::integer,
        ((ev->'turboConfiguration')->>'pointMultiplier')::integer
    FROM jsonb_array_elements(COALESCE(payload->'events', '[]'::jsonb)) AS ev;

    RETURN jsonb_build_object(
        'status', 'created',
        'runId', v_run_id
    );
END;
$$;

-- 4. Append schemaVersion 20 columns to vw_sessions_summary (preserves existing column order)
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
    s.pink_dodge_collisions,
    s.braking_mode,
    s.braking_deceleration_mps2,
    s.braking_release_acceleration_mps2,
    s.recovery_boosts,
    s.recovery_attempts,
    s.brake_presses,
    s.brake_releases,
    s.brake_held_seconds
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

-- 5. Append schemaVersion 20 columns to vw_bi_session_overview (preserves existing column order)
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
    END AS had_casi,
    s.braking_mode,
    s.braking_deceleration_mps2,
    s.braking_release_acceleration_mps2,
    s.recovery_boosts,
    s.recovery_attempts,
    s.brake_presses,
    s.brake_releases,
    s.brake_held_seconds
FROM public.vw_sessions_summary s;

-- 6. Reassert RPC and view permissions (service_role only; never expose to anon/authenticated/PUBLIC)
REVOKE EXECUTE ON FUNCTION public.ingest_session_report(jsonb) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION public.ingest_session_report(jsonb) FROM anon;
REVOKE EXECUTE ON FUNCTION public.ingest_session_report(jsonb) FROM authenticated;
GRANT EXECUTE ON FUNCTION public.ingest_session_report(jsonb) TO service_role;

REVOKE ALL ON
    public.vw_sessions_summary,
    public.vw_bi_session_overview
FROM PUBLIC, anon, authenticated;

GRANT SELECT ON
    public.vw_sessions_summary,
    public.vw_bi_session_overview
TO service_role;
