-- Migration 006: Complete schemaVersion 20 contract (v0.5.0 recovery system, multipliers,
-- transition decisions, and administrative school journey context).
-- Incremental, idempotent, and backwards-compatible with schemaVersion 13, 19, and early 20 (v0.4.1).
-- Works both on a clean installation (after 001-005) and on an environment where BI roles
-- (lotba_bi_definer / lotba_bi_readonly) are already active.
-- Security note: Do NOT expose ingest_session_report() or base tables directly to clients.

BEGIN;

-- 1. Add journey and finalized schemaVersion 20 summary columns to public.sessions
--    All columns are nullable so historical runs keep NULL (never coerced to 0).
ALTER TABLE public.sessions
    ADD COLUMN IF NOT EXISTS journey_id text,
    ADD COLUMN IF NOT EXISTS school_name text,
    ADD COLUMN IF NOT EXISTS school_shift text,
    ADD COLUMN IF NOT EXISTS school_date date,
    ADD COLUMN IF NOT EXISTS real_time_seconds double precision,
    ADD COLUMN IF NOT EXISTS recovery_offers integer,
    ADD COLUMN IF NOT EXISTS recovery_succeeded integer,
    ADD COLUMN IF NOT EXISTS recovery_failed integer,
    ADD COLUMN IF NOT EXISTS recovery_declined integer,
    ADD COLUMN IF NOT EXISTS recovery_interrupted integer,
    ADD COLUMN IF NOT EXISTS recovery_unavailable integer,
    ADD COLUMN IF NOT EXISTS recovery_offers_closed integer,
    ADD COLUMN IF NOT EXISTS points_lost bigint,
    ADD COLUMN IF NOT EXISTS points_recovered bigint,
    ADD COLUMN IF NOT EXISTS gems_recovered integer,
    ADD COLUMN IF NOT EXISTS recovery_zone_collisions integer,
    ADD COLUMN IF NOT EXISTS recovery_zone_points_lost bigint,
    ADD COLUMN IF NOT EXISTS recovery_zone_gems_lost integer,
    ADD COLUMN IF NOT EXISTS recovery_net_points bigint,
    ADD COLUMN IF NOT EXISTS recovery_net_gems integer,
    ADD COLUMN IF NOT EXISTS purchased_gem_multiplier integer,
    ADD COLUMN IF NOT EXISTS purchased_point_multiplier integer;

-- 2. Add finalized schemaVersion 20 event columns to public.session_events
ALTER TABLE public.session_events
    ADD COLUMN IF NOT EXISTS turbo_lifetime_mode text,
    ADD COLUMN IF NOT EXISTS transition_section integer,
    ADD COLUMN IF NOT EXISTS loss_origin text,
    ADD COLUMN IF NOT EXISTS gem_reward_multiplier integer,
    ADD COLUMN IF NOT EXISTS point_reward_multiplier integer,
    ADD COLUMN IF NOT EXISTS recovery_offer_id text,
    ADD COLUMN IF NOT EXISTS recovery_attempt_id text,
    ADD COLUMN IF NOT EXISTS recovery_outcome text,
    ADD COLUMN IF NOT EXISTS recovery_cause text,
    ADD COLUMN IF NOT EXISTS recovery_section integer,
    ADD COLUMN IF NOT EXISTS recovery_decision_number integer,
    ADD COLUMN IF NOT EXISTS recovery_accepted boolean,
    ADD COLUMN IF NOT EXISTS recovery_eligible_loss_sequences integer[],
    ADD COLUMN IF NOT EXISTS recovery_eligible_points bigint,
    ADD COLUMN IF NOT EXISTS recovery_offered_points bigint,
    ADD COLUMN IF NOT EXISTS recovery_returned_points bigint,
    ADD COLUMN IF NOT EXISTS recovery_eligible_gems integer,
    ADD COLUMN IF NOT EXISTS recovery_offered_gems integer,
    ADD COLUMN IF NOT EXISTS recovery_returned_gems integer;

-- 3. Create child table for root recoveryOpportunities[] (one row per recovery opportunity)
CREATE TABLE IF NOT EXISTS public.session_recovery_opportunities (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id uuid NOT NULL REFERENCES public.sessions(id) ON DELETE CASCADE,
    opportunity_index integer NOT NULL,
    offer_id text NOT NULL,
    attempt_id text,
    outcome text,
    cause text,
    section integer,
    decision_number integer,
    accepted boolean,
    eligible_loss_sequences integer[],
    eligible_points bigint,
    offered_points bigint,
    returned_points bigint,
    eligible_gems integer,
    offered_gems integer,
    returned_gems integer,
    offer_distance_metres double precision,
    start_distance_metres double precision,
    end_distance_metres double precision,
    challenge_metres double precision,
    start_time_seconds double precision,
    end_time_seconds double precision,
    fog_local_z double precision,
    point_loss_fraction double precision,
    gem_loss_fraction double precision,
    minimum_point_loss integer,
    turbo_lifetime_mode text,
    turbo_duration_seconds double precision,
    turbo_speed_bonus_mps double precision,
    turbo_gem_multiplier integer,
    turbo_point_multiplier integer,
    CONSTRAINT uq_session_recovery_opportunities_idx UNIQUE (session_id, opportunity_index)
);

-- 4. Indexes for journey filtering and recovery opportunity joins
CREATE INDEX IF NOT EXISTS idx_sessions_journey_id ON public.sessions (journey_id);
CREATE INDEX IF NOT EXISTS idx_sessions_school_date ON public.sessions (school_date);
CREATE INDEX IF NOT EXISTS idx_session_recovery_opp_session_id ON public.session_recovery_opportunities (session_id);
CREATE INDEX IF NOT EXISTS idx_session_recovery_opp_outcome ON public.session_recovery_opportunities (outcome);

-- 5. Base table security and RLS for public.session_recovery_opportunities
REVOKE ALL ON TABLE public.session_recovery_opportunities FROM PUBLIC, anon, authenticated;
REVOKE ALL ON SEQUENCE public.session_recovery_opportunities_id_seq FROM PUBLIC, anon, authenticated;

GRANT SELECT, INSERT ON TABLE public.session_recovery_opportunities TO service_role;
GRANT USAGE, SELECT ON SEQUENCE public.session_recovery_opportunities_id_seq TO service_role;

ALTER TABLE public.session_recovery_opportunities ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS session_recovery_opportunities_service_role_all ON public.session_recovery_opportunities;
CREATE POLICY session_recovery_opportunities_service_role_all ON public.session_recovery_opportunities
    FOR ALL TO service_role USING (true) WITH CHECK (true);

-- 6. Update atomic, idempotent ingestion RPC across all 3 tables
--    Normalizes empty objects serialized by Unity JsonUtility into SQL NULLs
--    and preserves NULL (not 0) for fields absent in historical schema versions (13, 19, early 20).
CREATE OR REPLACE FUNCTION public.ingest_session_report(payload jsonb)
RETURNS jsonb
LANGUAGE plpgsql
SECURITY INVOKER
AS $$
DECLARE
    v_metadata jsonb;
    v_braking jsonb;
    v_has_braking boolean;
    v_summary jsonb;
    v_journey jsonb;
    v_run_id text;
    v_session_id uuid;
BEGIN
    v_metadata := COALESCE(payload->'metadata', '{}'::jsonb);
    v_braking := CASE
        WHEN jsonb_typeof(v_metadata->'braking') = 'object' THEN v_metadata->'braking'
        ELSE '{}'::jsonb
    END;
    v_has_braking := NULLIF(btrim(v_braking->>'mode'), '') IS NOT NULL;
    v_summary := COALESCE(payload->'summary', '{}'::jsonb);
    v_journey := CASE
        WHEN jsonb_typeof(payload->'journey') = 'object' THEN payload->'journey'
        ELSE '{}'::jsonb
    END;
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
        brake_held_seconds,
        journey_id,
        school_name,
        school_shift,
        school_date,
        real_time_seconds,
        recovery_offers,
        recovery_succeeded,
        recovery_failed,
        recovery_declined,
        recovery_interrupted,
        recovery_unavailable,
        recovery_offers_closed,
        points_lost,
        points_recovered,
        gems_recovered,
        recovery_zone_collisions,
        recovery_zone_points_lost,
        recovery_zone_gems_lost,
        recovery_net_points,
        recovery_net_gems,
        purchased_gem_multiplier,
        purchased_point_multiplier
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
        CASE WHEN v_has_braking THEN NULLIF(btrim(v_braking->>'mode'), '') ELSE NULL END,
        CASE WHEN v_has_braking THEN (v_braking->>'decelerationMetresPerSecondSquared')::double precision ELSE NULL END,
        CASE WHEN v_has_braking THEN (v_braking->>'releaseAccelerationMetresPerSecondSquared')::double precision ELSE NULL END,
        (v_summary->>'recoveryBoosts')::integer,
        (v_summary->>'brakePresses')::integer,
        (v_summary->>'brakeReleases')::integer,
        (v_summary->>'brakeHeldSeconds')::double precision,
        NULLIF(btrim(v_journey->>'journeyId'), ''),
        NULLIF(btrim(v_journey->>'schoolName'), ''),
        NULLIF(btrim(v_journey->>'schoolShift'), ''),
        NULLIF(btrim(v_journey->>'schoolDate'), '')::date,
        (v_summary->>'realTimeSeconds')::double precision,
        (v_summary->>'recoveryOffers')::integer,
        (v_summary->>'recoverySucceeded')::integer,
        (v_summary->>'recoveryFailed')::integer,
        (v_summary->>'recoveryDeclined')::integer,
        (v_summary->>'recoveryInterrupted')::integer,
        (v_summary->>'recoveryUnavailable')::integer,
        (v_summary->>'recoveryOffersClosed')::integer,
        (v_summary->>'pointsLost')::bigint,
        (v_summary->>'pointsRecovered')::bigint,
        (v_summary->>'gemsRecovered')::integer,
        (v_summary->>'recoveryZoneCollisions')::integer,
        (v_summary->>'recoveryZonePointsLost')::bigint,
        (v_summary->>'recoveryZoneGemsLost')::integer,
        (v_summary->>'recoveryNetPoints')::bigint,
        (v_summary->>'recoveryNetGems')::integer,
        (v_summary->>'purchasedGemMultiplier')::integer,
        (v_summary->>'purchasedPointMultiplier')::integer
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
        turbo_lifetime_mode,
        turbo_duration_seconds,
        turbo_speed_bonus_mps,
        turbo_gem_multiplier,
        turbo_point_multiplier,
        transition_section,
        loss_origin,
        gem_reward_multiplier,
        point_reward_multiplier,
        recovery_offer_id,
        recovery_attempt_id,
        recovery_outcome,
        recovery_cause,
        recovery_section,
        recovery_decision_number,
        recovery_accepted,
        recovery_eligible_loss_sequences,
        recovery_eligible_points,
        recovery_offered_points,
        recovery_returned_points,
        recovery_eligible_gems,
        recovery_offered_gems,
        recovery_returned_gems
    )
    SELECT
        v_session_id,
        (ev->>'sequence')::integer,
        NULLIF(btrim(ev->>'type'), ''),
        ev->>'zone',
        NULLIF(btrim(ev->>'detail'), ''),
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
        CASE WHEN flags.has_turbo THEN NULLIF(btrim(tc->>'lifetimeMode'), '') ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'durationSeconds')::double precision ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'speedBonusMetresPerSecond')::double precision ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'gemMultiplier')::integer ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'pointMultiplier')::integer ELSE NULL END,
        (ev->>'transitionSection')::integer,
        NULLIF(btrim(ev->>'lossOrigin'), ''),
        (ev->>'gemRewardMultiplier')::integer,
        (ev->>'pointRewardMultiplier')::integer,
        CASE WHEN flags.has_recovery THEN NULLIF(btrim(rec->>'offerId'), '') ELSE NULL END,
        CASE WHEN flags.has_recovery THEN NULLIF(btrim(rec->>'attemptId'), '') ELSE NULL END,
        CASE WHEN flags.has_recovery THEN NULLIF(btrim(rec->>'outcome'), '') ELSE NULL END,
        CASE WHEN flags.has_recovery THEN NULLIF(btrim(rec->>'cause'), '') ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'section')::integer ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'decisionNumber')::integer ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'accepted')::boolean ELSE NULL END,
        CASE
            WHEN flags.has_recovery AND jsonb_typeof(rec->'eligibleLossSequences') = 'array' THEN
                ARRAY(
                    SELECT (seq_elem)::integer
                    FROM jsonb_array_elements_text(rec->'eligibleLossSequences') AS seq_elem
                )
            ELSE NULL
        END,
        CASE WHEN flags.has_recovery THEN (rec->>'eligiblePoints')::bigint ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'offeredPoints')::bigint ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'returnedPoints')::bigint ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'eligibleGems')::integer ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'offeredGems')::integer ELSE NULL END,
        CASE WHEN flags.has_recovery THEN (rec->>'returnedGems')::integer ELSE NULL END
    FROM jsonb_array_elements(
        CASE WHEN jsonb_typeof(payload->'events') = 'array' THEN payload->'events' ELSE '[]'::jsonb END
    ) AS ev
    CROSS JOIN LATERAL (
        SELECT
            CASE WHEN jsonb_typeof(ev->'turboConfiguration') = 'object' THEN ev->'turboConfiguration' ELSE '{}'::jsonb END AS tc,
            CASE WHEN jsonb_typeof(ev->'recovery') = 'object' THEN ev->'recovery' ELSE '{}'::jsonb END AS rec
    ) AS sub(tc, rec)
    CROSS JOIN LATERAL (
        SELECT
            (
                NULLIF(btrim(tc->>'lifetimeMode'), '') IS NOT NULL
                OR COALESCE((tc->>'durationSeconds')::double precision, 0) <> 0
                OR COALESCE((tc->>'speedBonusMetresPerSecond')::double precision, 0) <> 0
                OR COALESCE((tc->>'gemMultiplier')::integer, 0) <> 0
                OR COALESCE((tc->>'pointMultiplier')::integer, 0) <> 0
            ) AS has_turbo,
            (
                NULLIF(btrim(rec->>'offerId'), '') IS NOT NULL
                OR NULLIF(btrim(rec->>'outcome'), '') IS NOT NULL
            ) AS has_recovery
    ) AS flags;

    INSERT INTO public.session_recovery_opportunities (
        session_id,
        opportunity_index,
        offer_id,
        attempt_id,
        outcome,
        cause,
        section,
        decision_number,
        accepted,
        eligible_loss_sequences,
        eligible_points,
        offered_points,
        returned_points,
        eligible_gems,
        offered_gems,
        returned_gems,
        offer_distance_metres,
        start_distance_metres,
        end_distance_metres,
        challenge_metres,
        start_time_seconds,
        end_time_seconds,
        fog_local_z,
        point_loss_fraction,
        gem_loss_fraction,
        minimum_point_loss,
        turbo_lifetime_mode,
        turbo_duration_seconds,
        turbo_speed_bonus_mps,
        turbo_gem_multiplier,
        turbo_point_multiplier
    )
    SELECT
        v_session_id,
        (ord - 1)::integer,
        NULLIF(btrim(opp->>'offerId'), ''),
        NULLIF(btrim(opp->>'attemptId'), ''),
        NULLIF(btrim(opp->>'outcome'), ''),
        NULLIF(btrim(opp->>'cause'), ''),
        (opp->>'section')::integer,
        (opp->>'decisionNumber')::integer,
        (opp->>'accepted')::boolean,
        CASE
            WHEN jsonb_typeof(opp->'eligibleLossSequences') = 'array' THEN
                ARRAY(
                    SELECT (seq_elem)::integer
                    FROM jsonb_array_elements_text(opp->'eligibleLossSequences') AS seq_elem
                )
            ELSE ARRAY[]::integer[]
        END,
        (opp->>'eligiblePoints')::bigint,
        (opp->>'offeredPoints')::bigint,
        (opp->>'returnedPoints')::bigint,
        (opp->>'eligibleGems')::integer,
        (opp->>'offeredGems')::integer,
        (opp->>'returnedGems')::integer,
        (opp->>'offerDistanceMetres')::double precision,
        (opp->>'startDistanceMetres')::double precision,
        (opp->>'endDistanceMetres')::double precision,
        (opp->>'challengeMetres')::double precision,
        (opp->>'startTimeSeconds')::double precision,
        (opp->>'endTimeSeconds')::double precision,
        (opp->>'fogLocalZ')::double precision,
        (opp->>'pointLossFraction')::double precision,
        (opp->>'gemLossFraction')::double precision,
        (opp->>'minimumPointLoss')::integer,
        CASE WHEN flags.has_turbo THEN NULLIF(btrim(tc->>'lifetimeMode'), '') ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'durationSeconds')::double precision ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'speedBonusMetresPerSecond')::double precision ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'gemMultiplier')::integer ELSE NULL END,
        CASE WHEN flags.has_turbo THEN (tc->>'pointMultiplier')::integer ELSE NULL END
    FROM jsonb_array_elements(
        CASE WHEN jsonb_typeof(payload->'recoveryOpportunities') = 'array' THEN payload->'recoveryOpportunities' ELSE '[]'::jsonb END
    ) WITH ORDINALITY AS arr(opp, ord)
    CROSS JOIN LATERAL (
        SELECT CASE WHEN jsonb_typeof(opp->'turbo') = 'object' THEN opp->'turbo' ELSE '{}'::jsonb END
    ) AS sub(tc)
    CROSS JOIN LATERAL (
        SELECT (
            NULLIF(btrim(tc->>'lifetimeMode'), '') IS NOT NULL
            OR COALESCE((tc->>'durationSeconds')::double precision, 0) <> 0
            OR COALESCE((tc->>'speedBonusMetresPerSecond')::double precision, 0) <> 0
            OR COALESCE((tc->>'gemMultiplier')::integer, 0) <> 0
            OR COALESCE((tc->>'pointMultiplier')::integer, 0) <> 0
        ) AS has_turbo
    ) AS flags
    WHERE NULLIF(btrim(opp->>'offerId'), '') IS NOT NULL;

    RETURN jsonb_build_object(
        'status', 'created',
        'runId', v_run_id
    );
END;
$$;

REVOKE EXECUTE ON FUNCTION public.ingest_session_report(jsonb) FROM PUBLIC, anon, authenticated;
GRANT EXECUTE ON FUNCTION public.ingest_session_report(jsonb) TO service_role;

-- 7. Switch to lotba_bi_definer if BI encapsulation is already active in this database
--    so CREATE OR REPLACE VIEW preserves existing view ownership without superuser privileges.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'lotba_bi_definer') THEN
        REVOKE ALL ON TABLE public.session_recovery_opportunities FROM lotba_bi_definer;
        REVOKE ALL ON SEQUENCE public.session_recovery_opportunities_id_seq FROM lotba_bi_definer;
        REVOKE ALL ON FUNCTION public.ingest_session_report(jsonb) FROM lotba_bi_definer;

        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'lotba_bi_readonly') THEN
            REVOKE ALL ON TABLE public.session_recovery_opportunities FROM lotba_bi_readonly;
            REVOKE ALL ON SEQUENCE public.session_recovery_opportunities_id_seq FROM lotba_bi_readonly;
            REVOKE ALL ON FUNCTION public.ingest_session_report(jsonb) FROM lotba_bi_readonly;
        END IF;

        GRANT lotba_bi_definer TO postgres;
        GRANT USAGE, CREATE ON SCHEMA public TO lotba_bi_definer;
        GRANT SELECT ON TABLE
            public.sessions,
            public.session_events,
            public.session_recovery_opportunities
        TO lotba_bi_definer;

        DROP POLICY IF EXISTS session_recovery_opportunities_bi_definer_select ON public.session_recovery_opportunities;
        CREATE POLICY session_recovery_opportunities_bi_definer_select ON public.session_recovery_opportunities
            FOR SELECT TO lotba_bi_definer USING (true);

        PERFORM set_config('role', 'lotba_bi_definer', true);
    END IF;
END
$$;

-- 8. Analytics views (existing column names, types, and order strictly preserved; new columns appended at end)

-- 8a. Session-level summary (one row per session)
--     decisions_accepted counts ONLY speed decisions (speed_upgrade_purchased / boost_activated).
--     decisions_recovery tracks transition recovery choices separately so they are neither
--     counted as accepted speed decisions nor misclassified as unresolved offers.
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
        COALESCE(s.decisions_shown, 0)
        - COALESCE(s.decisions_kept, 0)
        - COALESCE(ev.decisions_accepted, 0)
        - COALESCE(ev.decisions_recovery, 0)
    )::integer AS decisions_unresolved,
    CASE
        WHEN GREATEST(0, COALESCE(s.decisions_shown, 0) - COALESCE(ev.decisions_recovery, 0)) > 0
            THEN ROUND(
                100.0 * COALESCE(ev.decisions_accepted, 0)
                / NULLIF(GREATEST(0, COALESCE(s.decisions_shown, 0) - COALESCE(ev.decisions_recovery, 0)), 0),
                2
            )
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
    s.brake_held_seconds,
    s.journey_id,
    s.school_name,
    s.school_shift,
    s.school_date,
    s.real_time_seconds,
    COALESCE(ev.decisions_recovery, 0)::integer AS decisions_recovery,
    CASE
        WHEN s.ending = 'brake' THEN GREATEST(
            0,
            COALESCE(s.decisions_shown, 0)
            - COALESCE(s.decisions_kept, 0)
            - COALESCE(ev.decisions_accepted, 0)
            - COALESCE(ev.decisions_recovery, 0)
        )
        ELSE 0
    END::integer AS decisions_braked,
    s.recovery_offers,
    s.recovery_succeeded,
    s.recovery_failed,
    s.recovery_declined,
    s.recovery_interrupted,
    s.recovery_unavailable,
    s.recovery_offers_closed,
    s.points_lost,
    s.points_recovered,
    s.gems_recovered,
    s.recovery_zone_collisions,
    s.recovery_zone_points_lost,
    s.recovery_zone_gems_lost,
    s.recovery_net_points,
    s.recovery_net_gems,
    s.purchased_gem_multiplier,
    s.purchased_point_multiplier
FROM public.sessions s
LEFT JOIN LATERAL (
    SELECT
        COUNT(DISTINCT e.decision_number) FILTER (
            WHERE COALESCE(e.decision_number, 0) > 0
              AND e.type IN ('speed_upgrade_purchased', 'boost_activated')
        ) AS decisions_accepted,
        COUNT(DISTINCT e.decision_number) FILTER (
            WHERE COALESCE(e.decision_number, 0) > 0
              AND e.type = 'recovery_entered'
              AND e.detail = 'transition_choice'
        ) AS decisions_recovery
    FROM public.session_events e
    WHERE e.session_id = s.id
) ev ON true;

-- 8b. Speed & transition decisions (one row per decision_number > 0)
--     Supports both legacy speed_offer_shown and v0.5.0 transition_decision_shown.
--     Does NOT treat the 0 km/h physical gate stop of transition_decision_shown as speed_kmh_at_offer.
CREATE OR REPLACE VIEW public.vw_speed_decisions
WITH (security_invoker = true) AS
WITH decision_keys AS (
    SELECT DISTINCT
        e.session_id,
        e.decision_number
    FROM public.session_events e
    WHERE COALESCE(e.decision_number, 0) > 0
      AND (
          e.type IN ('speed_offer_shown', 'transition_decision_shown', 'speed_kept', 'speed_upgrade_purchased', 'boost_activated')
          OR (e.type = 'recovery_entered' AND e.detail = 'transition_choice')
      )
),
offers AS (
    SELECT DISTINCT ON (e.session_id, e.decision_number)
        e.session_id,
        e.decision_number,
        e.sequence AS offer_sequence,
        e.type AS offer_type,
        e.time_seconds AS offer_time_seconds,
        e.distance_metres AS offer_distance_metres,
        e.zone,
        CASE
            WHEN e.type = 'speed_offer_shown' THEN e.speed_kmh
            ELSE NULL
        END AS speed_kmh_at_offer,
        e.purchased_speed_kmh AS base_speed_kmh_at_offer
    FROM public.session_events e
    WHERE COALESCE(e.decision_number, 0) > 0
      AND e.type IN ('speed_offer_shown', 'transition_decision_shown')
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
      AND (
          e.type IN ('speed_kept', 'speed_upgrade_purchased', 'boost_activated')
          OR (e.type = 'recovery_entered' AND e.detail = 'transition_choice')
      )
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
    END AS accepted,
    of.offer_type,
    of.base_speed_kmh_at_offer,
    CASE
        WHEN oc.outcome_type IN ('speed_upgrade_purchased', 'boost_activated') THEN 'speed'
        WHEN oc.outcome_type = 'speed_kept' THEN 'continuity'
        WHEN oc.outcome_type = 'recovery_entered' THEN 'recovery'
        WHEN oc.outcome_sequence IS NULL AND s.ending = 'brake' THEN 'brake'
        ELSE 'unresolved'
    END AS decision_category
FROM decision_keys dk
JOIN public.sessions s ON s.id = dk.session_id
LEFT JOIN offers of ON of.session_id = dk.session_id AND of.decision_number = dk.decision_number
LEFT JOIN outcomes oc ON oc.session_id = dk.session_id AND oc.decision_number = dk.decision_number;

-- 8c. Post-adverse-event behavior (one row per obstacle_hit or pink_gem_missed)
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
    ((s.time_seconds - e.time_seconds) <= 5.0) AS finished_within_5s,
    e.loss_origin,
    COALESCE(after_stats.entered_recovery_after, false) AS entered_recovery_after,
    COALESCE(after_stats.recovered_after_loss, false) AS recovered_after_loss
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
        BOOL_OR(e2.type = 'boost_activated') AS activated_boost_after,
        BOOL_OR(e2.type = 'recovery_entered') AS entered_recovery_after,
        BOOL_OR(e2.type = 'recovery_succeeded') AS recovered_after_loss
    FROM public.session_events e2
    WHERE e2.session_id = e.session_id
      AND e2.sequence > e.sequence
) after_stats ON true
WHERE e.type IN ('obstacle_hit', 'pink_gem_missed');

-- 8d. Daily UTC metrics rollup (excludes transition recovery choices from speed-acceptance denominator)
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
        100.0 * COALESCE(SUM(decisions_accepted), 0)
        / NULLIF(GREATEST(0, COALESCE(SUM(decisions_shown), 0) - COALESCE(SUM(decisions_recovery), 0)), 0),
        2
    ) AS acceptance_rate_pct,
    COALESCE(SUM(pink_gems_collected), 0) AS pink_gems_collected,
    COALESCE(SUM(pink_gems_missed), 0) AS pink_gems_missed,
    COALESCE(SUM(pink_gem_near_misses), 0) AS pink_gem_near_misses
FROM public.vw_sessions_summary
WHERE started_at_utc IS NOT NULL
GROUP BY (started_at_utc AT TIME ZONE 'UTC')::date
ORDER BY session_date_utc DESC;

-- 9. BI views (strictly 1 row per session on the 3 existing views, and 1 row per opportunity on the 4th)

-- 9a. BI View 1: Session overview (1 row per session; no 1:N joins)
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
    s.brake_held_seconds,
    s.journey_id,
    s.school_name,
    s.school_shift,
    s.school_date,
    s.real_time_seconds,
    s.decisions_recovery,
    s.decisions_braked,
    s.recovery_offers,
    s.recovery_succeeded,
    s.recovery_failed,
    s.recovery_declined,
    s.recovery_interrupted,
    s.recovery_unavailable,
    s.recovery_offers_closed,
    s.points_lost,
    s.points_recovered,
    s.gems_recovered,
    s.recovery_zone_collisions,
    s.recovery_zone_points_lost,
    s.recovery_zone_gems_lost,
    s.recovery_net_points,
    s.recovery_net_gems,
    s.purchased_gem_multiplier,
    s.purchased_point_multiplier
FROM public.vw_sessions_summary s;

-- 9b. BI View 2: Behavior after loss per session (1 row per session; pre-grouped by run_id)
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
    agg.avg_seconds_to_next_event,
    s.journey_id,
    s.school_name,
    s.school_shift,
    s.school_date,
    COALESCE(agg.recovery_zone_hits_count, 0)::integer AS recovery_zone_hits_count,
    COALESCE(agg.entered_recovery_after_loss_count, 0)::integer AS entered_recovery_after_loss_count,
    COALESCE(agg.recovered_after_loss_count, 0)::integer AS recovered_after_loss_count
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
        ROUND(AVG(b.seconds_to_next_event)::numeric, 3) AS avg_seconds_to_next_event,
        COUNT(*) FILTER (WHERE b.adverse_type = 'obstacle_hit' AND b.loss_origin = 'recovery') AS recovery_zone_hits_count,
        COUNT(*) FILTER (WHERE b.entered_recovery_after) AS entered_recovery_after_loss_count,
        COUNT(*) FILTER (WHERE b.recovered_after_loss) AS recovered_after_loss_count
    FROM public.vw_behavior_after_loss b
    GROUP BY b.run_id
) agg ON agg.run_id = s.run_id;

-- 9c. BI View 3: Speed & transition decisions per session (1 row per session; pre-grouped by run_id)
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
        WHEN (COALESCE(agg.accepted_decisions, 0) + COALESCE(agg.rejected_decisions, 0)) > 0
            THEN ROUND(
                100.0 * COALESCE(agg.accepted_decisions, 0)
                / (COALESCE(agg.accepted_decisions, 0) + COALESCE(agg.rejected_decisions, 0)),
                2
            )
        ELSE NULL
    END AS acceptance_pct,
    agg.avg_speed_at_offer,
    agg.max_speed_at_offer,
    agg.avg_purchased_speed_kmh,
    COALESCE(agg.speed_kept_count, 0)::integer AS speed_kept_count,
    COALESCE(agg.speed_upgrade_purchased_count, 0)::integer AS speed_upgrade_purchased_count,
    COALESCE(agg.boost_activated_offer_count, 0)::integer AS boost_activated_offer_count,
    s.journey_id,
    s.school_name,
    s.school_shift,
    s.school_date,
    COALESCE(agg.recovery_chosen_count, 0)::integer AS recovery_chosen_count,
    COALESCE(agg.braked_at_decision_count, 0)::integer AS braked_at_decision_count,
    agg.avg_base_speed_at_offer
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
        COUNT(*) FILTER (WHERE d.outcome_type = 'boost_activated') AS boost_activated_offer_count,
        COUNT(*) FILTER (WHERE d.decision_category = 'recovery') AS recovery_chosen_count,
        COUNT(*) FILTER (WHERE d.decision_category = 'brake') AS braked_at_decision_count,
        ROUND(AVG(d.base_speed_kmh_at_offer)::numeric, 2) AS avg_base_speed_at_offer
    FROM public.vw_speed_decisions d
    GROUP BY d.run_id
) agg ON agg.run_id = s.run_id;

-- 9d. BI View 4: Recovery opportunities (1 row per opportunity; N:1 join to parent session PK)
CREATE OR REPLACE VIEW public.vw_bi_recovery_opportunities
WITH (security_invoker = true) AS
SELECT
    (s.started_at_utc AT TIME ZONE 'UTC')::date AS session_date_utc,
    s.started_at_utc,
    s.run_id,
    s.game_version,
    s.schema_version,
    s.platform,
    s.route,
    s.ending AS session_ending,
    s.journey_id,
    s.school_name,
    s.school_shift,
    s.school_date,
    ro.opportunity_index,
    ro.offer_id,
    (ro.offer_id LIKE 'trans-offer-%') AS is_transition_offer,
    ro.attempt_id,
    ro.outcome,
    ro.cause,
    ro.section,
    ro.decision_number,
    ro.accepted,
    COALESCE(cardinality(ro.eligible_loss_sequences), 0)::integer AS eligible_losses_count,
    array_to_string(ro.eligible_loss_sequences, ',') AS eligible_loss_sequences_csv,
    ro.eligible_points,
    ro.offered_points,
    ro.returned_points,
    ro.eligible_gems,
    ro.offered_gems,
    ro.returned_gems,
    ro.offer_distance_metres,
    ro.start_distance_metres,
    ro.end_distance_metres,
    ro.challenge_metres,
    ro.start_time_seconds,
    ro.end_time_seconds,
    CASE
        WHEN ro.accepted
             AND ro.end_time_seconds IS NOT NULL
             AND ro.start_time_seconds IS NOT NULL
             AND ro.end_time_seconds >= ro.start_time_seconds
            THEN ROUND((ro.end_time_seconds - ro.start_time_seconds)::numeric, 3)
        ELSE NULL
    END AS attempt_duration_seconds,
    ro.fog_local_z,
    ro.point_loss_fraction,
    ro.gem_loss_fraction,
    ro.minimum_point_loss,
    ro.turbo_lifetime_mode,
    ro.turbo_duration_seconds,
    ro.turbo_speed_bonus_mps,
    ro.turbo_gem_multiplier,
    ro.turbo_point_multiplier
FROM public.session_recovery_opportunities ro
JOIN public.sessions s ON s.id = ro.session_id;

-- 10. Finalize view options, permissions, and role cleanup
DO $$
BEGIN
    IF current_user = 'lotba_bi_definer' THEN
        ALTER VIEW public.vw_sessions_summary          SET (security_invoker = false);
        ALTER VIEW public.vw_speed_decisions           SET (security_invoker = false);
        ALTER VIEW public.vw_behavior_after_loss       SET (security_invoker = false);
        ALTER VIEW public.vw_daily_metrics             SET (security_invoker = false);
        ALTER VIEW public.vw_bi_session_overview       SET (security_invoker = false);
        ALTER VIEW public.vw_bi_behavior_metrics       SET (security_invoker = false);
        ALTER VIEW public.vw_bi_speed_decisions        SET (security_invoker = false);
        ALTER VIEW public.vw_bi_recovery_opportunities SET (security_invoker = false);

        REVOKE ALL ON
            public.vw_sessions_summary,
            public.vw_speed_decisions,
            public.vw_behavior_after_loss,
            public.vw_daily_metrics,
            public.vw_bi_session_overview,
            public.vw_bi_behavior_metrics,
            public.vw_bi_speed_decisions,
            public.vw_bi_recovery_opportunities
        FROM PUBLIC, anon, authenticated;

        GRANT SELECT ON
            public.vw_sessions_summary,
            public.vw_speed_decisions,
            public.vw_behavior_after_loss,
            public.vw_daily_metrics,
            public.vw_bi_session_overview,
            public.vw_bi_behavior_metrics,
            public.vw_bi_speed_decisions,
            public.vw_bi_recovery_opportunities
        TO postgres, service_role;

        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'lotba_bi_readonly') THEN
            REVOKE ALL ON
                public.vw_sessions_summary,
                public.vw_speed_decisions,
                public.vw_behavior_after_loss,
                public.vw_daily_metrics
            FROM lotba_bi_readonly;

            GRANT SELECT ON
                public.vw_bi_session_overview,
                public.vw_bi_behavior_metrics,
                public.vw_bi_speed_decisions,
                public.vw_bi_recovery_opportunities
            TO lotba_bi_readonly;
        END IF;

        PERFORM set_config('role', 'none', true);
        REVOKE CREATE ON SCHEMA public FROM lotba_bi_definer;
        REVOKE lotba_bi_definer FROM postgres;
    ELSE
        REVOKE ALL ON
            public.vw_sessions_summary,
            public.vw_speed_decisions,
            public.vw_behavior_after_loss,
            public.vw_daily_metrics,
            public.vw_bi_session_overview,
            public.vw_bi_behavior_metrics,
            public.vw_bi_speed_decisions,
            public.vw_bi_recovery_opportunities
        FROM PUBLIC, anon, authenticated;

        GRANT SELECT ON
            public.vw_sessions_summary,
            public.vw_speed_decisions,
            public.vw_behavior_after_loss,
            public.vw_daily_metrics,
            public.vw_bi_session_overview,
            public.vw_bi_behavior_metrics,
            public.vw_bi_speed_decisions,
            public.vw_bi_recovery_opportunities
        TO service_role;
    END IF;
END
$$;

COMMIT;
