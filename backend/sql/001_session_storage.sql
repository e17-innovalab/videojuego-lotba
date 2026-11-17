-- Migration 001: SessionReport v1 PostgreSQL / Supabase storage
-- Anonymous session telemetry only (no personal data, no user profiles, no device IDs).
-- Security note: Do NOT expose ingest_session_report() directly to the Unity client
-- with privileged credentials. It must be invoked exclusively by our Backend service.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1. Sessions table (one row per finished run: metadata + RunTelemetrySummary)
CREATE TABLE IF NOT EXISTS sessions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id text NOT NULL,
    report_version text NOT NULL,
    schema_version integer,
    game_version text,
    platform text,
    started_at_utc timestamptz,
    finished_at_utc timestamptz,
    route text,
    seed integer,
    route_distance_metres double precision,
    ending text,
    time_seconds double precision,
    distance_metres double precision,
    peak_speed_kmh double precision,
    score bigint,
    gems integer,
    collisions integer,
    voluntary_boosts integer,
    bonus_gems integer,
    manual_boosts integer,
    recovery_attempts integer,
    decisions_shown integer,
    decisions_kept integer,
    gems_lost integer,
    gem_balance integer,
    boost_collisions integer,
    gems_spent integer,
    speed_upgrades integer,
    purchased_speed_kmh double precision,
    ingested_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_sessions_run_id UNIQUE (run_id)
);

-- 2. Session events table (one row per RunTelemetryEvent)
CREATE TABLE IF NOT EXISTS session_events (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id uuid NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    sequence integer NOT NULL,
    type text NOT NULL,
    zone text,
    detail text,
    time_seconds double precision,
    distance_metres double precision,
    speed_kmh double precision,
    lane integer,
    score bigint,
    score_delta bigint,
    gems_collected integer,
    gem_balance integer,
    gem_delta integer,
    during_boost boolean,
    decision_number integer,
    purchased_speed_kmh double precision,
    CONSTRAINT uq_session_events_session_sequence UNIQUE (session_id, sequence)
);

-- 3. Minimal MVP indexes
CREATE INDEX IF NOT EXISTS idx_sessions_started_at_utc ON sessions (started_at_utc);
CREATE INDEX IF NOT EXISTS idx_sessions_ending ON sessions (ending);
CREATE INDEX IF NOT EXISTS idx_sessions_route ON sessions (route);
CREATE INDEX IF NOT EXISTS idx_sessions_platform ON sessions (platform);

CREATE INDEX IF NOT EXISTS idx_session_events_type ON session_events (type);

-- 4. Atomic transactional ingestion function
-- Security note: Invoke only from our trusted Backend, never directly from Unity with service/privileged keys.
CREATE OR REPLACE FUNCTION ingest_session_report(payload jsonb)
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

    INSERT INTO sessions (
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
        purchased_speed_kmh
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
        (v_summary->>'purchasedSpeedKmh')::double precision
    )
    ON CONFLICT (run_id) DO NOTHING
    RETURNING id INTO v_session_id;

    IF v_session_id IS NULL THEN
        RETURN jsonb_build_object(
            'status', 'already_exists',
            'runId', v_run_id
        );
    END IF;

    INSERT INTO session_events (
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
        purchased_speed_kmh
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
        (ev->>'purchasedSpeedKmh')::double precision
    FROM jsonb_array_elements(COALESCE(payload->'events', '[]'::jsonb)) AS ev;

    RETURN jsonb_build_object(
        'status', 'created',
        'runId', v_run_id
    );
END;
$$;

REVOKE EXECUTE ON FUNCTION ingest_session_report(jsonb) FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION ingest_session_report(jsonb) FROM anon;
REVOKE EXECUTE ON FUNCTION ingest_session_report(jsonb) FROM authenticated;

GRANT EXECUTE ON FUNCTION ingest_session_report(jsonb) TO service_role;
