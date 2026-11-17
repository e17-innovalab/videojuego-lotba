-- Migration 002: Add schemaVersion 19 columns (CASI / pink gems, pink dodge) and update ingest_session_report RPC.
-- Incremental, idempotent, and backwards-compatible with schemaVersion 13 payloads.
-- Security note: Do NOT expose ingest_session_report() directly to the Unity client
-- with privileged credentials. It must be invoked exclusively by our Backend service.

-- 1. Add schemaVersion 19 summary columns to public.sessions (nullable for historical compatibility)
ALTER TABLE public.sessions
    ADD COLUMN IF NOT EXISTS pink_gems_collected integer,
    ADD COLUMN IF NOT EXISTS pink_gems_missed integer,
    ADD COLUMN IF NOT EXISTS pink_gem_near_misses integer,
    ADD COLUMN IF NOT EXISTS best_pink_gem_streak integer,
    ADD COLUMN IF NOT EXISTS pink_dodge_cleared integer,
    ADD COLUMN IF NOT EXISTS pink_dodge_collisions integer;

-- 2. Add schemaVersion 19 event columns to public.session_events (nullable for historical compatibility)
ALTER TABLE public.session_events
    ADD COLUMN IF NOT EXISTS pink_gem_streak integer,
    ADD COLUMN IF NOT EXISTS score_multiplier integer,
    ADD COLUMN IF NOT EXISTS pink_gem_streak_before integer,
    ADD COLUMN IF NOT EXISTS pink_gem_near_miss boolean,
    ADD COLUMN IF NOT EXISTS pink_dodge_encounter integer;

-- 3. Update transactional ingestion function (preserves signature, SECURITY INVOKER, and schema 13 compatibility)
CREATE OR REPLACE FUNCTION public.ingest_session_report(payload jsonb)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY INVOKER
AS $$
DECLARE
    v_metadata jsonb;
    v_summary jsonb;
    v_run_id text;
    v_session_id uuid;
BEGIN
    v_metadata := COALESCE(payload->'metadata', '{}'::jsonb);
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
        pink_dodge_collisions
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
        (v_summary->>'pinkDodgeCollisions')::integer
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
        pink_dodge_encounter
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
        (ev->>'pinkDodgeEncounter')::integer
    FROM jsonb_array_elements(COALESCE(payload->'events', '[]'::jsonb)) AS ev;

    RETURN jsonb_build_object(
        'status', 'created',
        'runId', v_run_id
    );
END;
$$;

-- 4. Reassert RPC execution permissions (new columns inherit existing table-level grants)
REVOKE EXECUTE ON FUNCTION public.ingest_session_report(jsonb) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION public.ingest_session_report(jsonb) FROM anon;
REVOKE EXECUTE ON FUNCTION public.ingest_session_report(jsonb) FROM authenticated;

GRANT EXECUTE ON FUNCTION public.ingest_session_report(jsonb) TO service_role;
