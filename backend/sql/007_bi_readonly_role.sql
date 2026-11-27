-- Migration 007: Dedicated Read-Only BI Role & View Boundary Encapsulation
-- Configures lotba_bi_definer (NOLOGIN view owner) and lotba_bi_readonly (LOGIN, CONNECTION LIMIT 10)
-- with strict RLS and least-privilege access to the 4 authorized BI views ONLY:
--   * public.vw_bi_session_overview
--   * public.vw_bi_behavior_metrics
--   * public.vw_bi_speed_decisions
--   * public.vw_bi_recovery_opportunities
--
-- Security & Idempotency notes:
--   * Contains NO passwords or credentials.
--   * On initial creation, lotba_bi_readonly has PASSWORD NULL (cannot authenticate until an
--     administrator assigns a strong password out-of-band).
--   * On re-execution, existing passwords on lotba_bi_readonly are NEVER modified or cleared.
--   * Safe to run both on a clean database (after 001-006) and on an environment where earlier
--     BI role hardening was already applied.

BEGIN;

-- 0. Capture pre-migration row counts and deterministic MD5 checksums of all 4 BI views
CREATE TEMP TABLE _bi_checksums_before ON COMMIT DROP AS
SELECT 'vw_bi_session_overview'::text AS view_name, COUNT(*)::bigint AS rows,
       md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id), '')) AS md5
FROM public.vw_bi_session_overview t
UNION ALL
SELECT 'vw_bi_behavior_metrics'::text, COUNT(*)::bigint,
       md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id), ''))
FROM public.vw_bi_behavior_metrics t
UNION ALL
SELECT 'vw_bi_speed_decisions'::text, COUNT(*)::bigint,
       md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id), ''))
FROM public.vw_bi_speed_decisions t
UNION ALL
SELECT 'vw_bi_recovery_opportunities'::text, COUNT(*)::bigint,
       md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id, opportunity_index), ''))
FROM public.vw_bi_recovery_opportunities t;

-- 1. Internal NOLOGIN view-owner role (evaluates views without superuser privileges)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'lotba_bi_definer') THEN
        CREATE ROLE lotba_bi_definer
            NOLOGIN
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT
            NOREPLICATION
            NOBYPASSRLS;
    ELSE
        ALTER ROLE lotba_bi_definer
            NOLOGIN
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT;
    END IF;
END
$$;

-- 2. Dedicated read-only role for Looker Studio (CONNECTION LIMIT 10; preserves existing password if present)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'lotba_bi_readonly') THEN
        CREATE ROLE lotba_bi_readonly
            LOGIN
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT
            NOREPLICATION
            NOBYPASSRLS
            CONNECTION LIMIT 10;
    ELSE
        ALTER ROLE lotba_bi_readonly
            LOGIN
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT
            CONNECTION LIMIT 10;
    END IF;
END
$$;

ALTER ROLE lotba_bi_readonly SET default_transaction_read_only = on;
ALTER ROLE lotba_bi_readonly SET statement_timeout = '15s';
ALTER ROLE lotba_bi_readonly SET idle_in_transaction_session_timeout = '15s';
ALTER ROLE lotba_bi_readonly SET lock_timeout = '5s';
ALTER ROLE lotba_bi_readonly SET search_path = public;

-- 3. Remove residual privileges on schema, base tables, sequences, and ingestion RPC
REVOKE CREATE ON SCHEMA public FROM PUBLIC, anon, authenticated, lotba_bi_readonly, lotba_bi_definer;

REVOKE ALL ON TABLE
    public.sessions,
    public.session_events,
    public.session_recovery_opportunities
FROM PUBLIC, anon, authenticated, lotba_bi_readonly, lotba_bi_definer;

REVOKE ALL ON SEQUENCE
    public.session_events_id_seq,
    public.session_recovery_opportunities_id_seq
FROM PUBLIC, anon, authenticated, lotba_bi_readonly, lotba_bi_definer;

REVOKE ALL ON FUNCTION public.ingest_session_report(jsonb)
FROM PUBLIC, anon, authenticated, lotba_bi_readonly, lotba_bi_definer;

-- Reassert backend (service_role) privileges across all 3 base tables and 2 sequences
GRANT USAGE ON SCHEMA public TO service_role;
GRANT SELECT, INSERT ON TABLE
    public.sessions,
    public.session_events,
    public.session_recovery_opportunities
TO service_role;
GRANT USAGE, SELECT ON SEQUENCE
    public.session_events_id_seq,
    public.session_recovery_opportunities_id_seq
TO service_role;
GRANT EXECUTE ON FUNCTION public.ingest_session_report(jsonb) TO service_role;

-- 4. Enable Row Level Security and enforce explicit policies on all 3 base tables
ALTER TABLE public.sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.session_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.session_recovery_opportunities ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS sessions_bi_definer_select ON public.sessions;
CREATE POLICY sessions_bi_definer_select ON public.sessions
    FOR SELECT TO lotba_bi_definer USING (true);

DROP POLICY IF EXISTS session_events_bi_definer_select ON public.session_events;
CREATE POLICY session_events_bi_definer_select ON public.session_events
    FOR SELECT TO lotba_bi_definer USING (true);

DROP POLICY IF EXISTS session_recovery_opportunities_bi_definer_select ON public.session_recovery_opportunities;
CREATE POLICY session_recovery_opportunities_bi_definer_select ON public.session_recovery_opportunities
    FOR SELECT TO lotba_bi_definer USING (true);

DROP POLICY IF EXISTS sessions_service_role_all ON public.sessions;
CREATE POLICY sessions_service_role_all ON public.sessions
    FOR ALL TO service_role USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS session_events_service_role_all ON public.session_events;
CREATE POLICY session_events_service_role_all ON public.session_events
    FOR ALL TO service_role USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS session_recovery_opportunities_service_role_all ON public.session_recovery_opportunities;
CREATE POLICY session_recovery_opportunities_service_role_all ON public.session_recovery_opportunities
    FOR ALL TO service_role USING (true) WITH CHECK (true);

-- 5. Database and schema grants performed as admin before switching role
DO $$
BEGIN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO lotba_bi_readonly', current_database());
END
$$;
GRANT USAGE ON SCHEMA public TO lotba_bi_readonly;

-- Grant temporary membership and schema CREATE so non-superuser postgres can transfer ownership
-- and execute view-owner DDL/ACL operations cleanly (both on initial run and idempotent re-runs).
GRANT lotba_bi_definer TO postgres;
GRANT USAGE, CREATE ON SCHEMA public TO lotba_bi_definer;
GRANT SELECT ON TABLE
    public.sessions,
    public.session_events,
    public.session_recovery_opportunities
TO lotba_bi_definer;

ALTER VIEW public.vw_sessions_summary          OWNER TO lotba_bi_definer;
ALTER VIEW public.vw_speed_decisions           OWNER TO lotba_bi_definer;
ALTER VIEW public.vw_behavior_after_loss       OWNER TO lotba_bi_definer;
ALTER VIEW public.vw_daily_metrics             OWNER TO lotba_bi_definer;
ALTER VIEW public.vw_bi_session_overview       OWNER TO lotba_bi_definer;
ALTER VIEW public.vw_bi_behavior_metrics       OWNER TO lotba_bi_definer;
ALTER VIEW public.vw_bi_speed_decisions        OWNER TO lotba_bi_definer;
ALTER VIEW public.vw_bi_recovery_opportunities OWNER TO lotba_bi_definer;

-- 6. Execute all view option and ACL changes as the view owner (lotba_bi_definer)
SET LOCAL ROLE lotba_bi_definer;

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
FROM PUBLIC, anon, authenticated, lotba_bi_readonly;

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

GRANT SELECT ON
    public.vw_bi_session_overview,
    public.vw_bi_behavior_metrics,
    public.vw_bi_speed_decisions,
    public.vw_bi_recovery_opportunities
TO lotba_bi_readonly;

-- Return to postgres and revoke temporary privileges only AFTER all owner operations are complete
RESET ROLE;
REVOKE CREATE ON SCHEMA public FROM lotba_bi_definer;
REVOKE lotba_bi_definer FROM postgres;

-- 7. Pre-COMMIT automated security and live-query equivalence assertions
DO $$
DECLARE
    v_upward_memberships integer;
    v_unauthorized_grantees integer;
    v_residual_temp_grants integer;
    v_creatable_schemas text;
    v_writable_objects text;
    v_extra_readable_objects text;
    v_residual_anon_auth integer;
    v_non_definer_views integer;
    v_r_overview bigint;
    v_m_overview text;
    v_r_behavior bigint;
    v_m_behavior text;
    v_r_decisions bigint;
    v_m_decisions text;
    v_r_recovery bigint;
    v_m_recovery text;
    v_checksum_mismatches integer;
BEGIN
    -- 7a. Execute real SELECT on all 4 BI views under lotba_bi_readonly before COMMIT
    EXECUTE 'GRANT lotba_bi_readonly TO postgres';
    EXECUTE 'SET LOCAL ROLE lotba_bi_readonly';

    IF current_user <> 'lotba_bi_readonly' THEN
        RAISE EXCEPTION 'Role switch check failed: expected lotba_bi_readonly, got %', current_user;
    END IF;

    SELECT COUNT(*)::bigint, md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id), ''))
      INTO v_r_overview, v_m_overview
    FROM public.vw_bi_session_overview t;

    SELECT COUNT(*)::bigint, md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id), ''))
      INTO v_r_behavior, v_m_behavior
    FROM public.vw_bi_behavior_metrics t;

    SELECT COUNT(*)::bigint, md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id), ''))
      INTO v_r_decisions, v_m_decisions
    FROM public.vw_bi_speed_decisions t;

    SELECT COUNT(*)::bigint, md5(COALESCE(string_agg(row_to_json(t)::text, '|' ORDER BY run_id, opportunity_index), ''))
      INTO v_r_recovery, v_m_recovery
    FROM public.vw_bi_recovery_opportunities t;

    EXECUTE 'RESET ROLE';
    EXECUTE 'REVOKE lotba_bi_readonly FROM postgres';

    -- 7b-0. Role attributes check: neither role may hold elevated flags; CONNECTION LIMIT must be 10
    IF EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname IN ('lotba_bi_readonly', 'lotba_bi_definer')
          AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolinherit OR rolreplication OR rolbypassrls)
    ) OR EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname = 'lotba_bi_definer' AND rolcanlogin
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_roles WHERE rolname = 'lotba_bi_readonly' AND rolcanlogin AND rolconnlimit = 10
    ) THEN
        RAISE EXCEPTION 'Security check failed: unexpected role attributes in pg_roles';
    END IF;

    -- 7b-0b. Verify all 8 views are owned by lotba_bi_definer
    SELECT COUNT(*) INTO v_non_definer_views
    FROM pg_class c
    WHERE c.relnamespace = 'public'::regnamespace
      AND c.relkind = 'v'
      AND c.relname IN (
          'vw_sessions_summary',
          'vw_speed_decisions',
          'vw_behavior_after_loss',
          'vw_daily_metrics',
          'vw_bi_session_overview',
          'vw_bi_behavior_metrics',
          'vw_bi_speed_decisions',
          'vw_bi_recovery_opportunities'
      )
      AND c.relowner <> 'lotba_bi_definer'::regrole;
    IF v_non_definer_views > 0 THEN
        RAISE EXCEPTION 'Security check failed: % views are not owned by lotba_bi_definer', v_non_definer_views;
    END IF;

    -- 7b-1. Outbound escalation check: neither new role may be a member of ANY role
    SELECT COUNT(*) INTO v_upward_memberships
    FROM pg_auth_members
    WHERE member IN ('lotba_bi_readonly'::regrole, 'lotba_bi_definer'::regrole);
    IF v_upward_memberships > 0 THEN
        RAISE EXCEPTION 'Security check failed: new roles must not inherit/belong to any role (%)', v_upward_memberships;
    END IF;

    -- 7b-2. Inbound grantee check: no role other than creator admin (postgres) may hold any grant on the BI roles
    SELECT COUNT(*) INTO v_unauthorized_grantees
    FROM pg_auth_members
    WHERE roleid IN ('lotba_bi_readonly'::regrole, 'lotba_bi_definer'::regrole)
      AND member <> 'postgres'::regrole;
    IF v_unauthorized_grantees > 0 THEN
        RAISE EXCEPTION 'Security check failed: non-postgres role granted membership on BI roles (%)', v_unauthorized_grantees;
    END IF;

    -- 7b-3. Verify temporary GRANTs by postgres were revoked (only bootstrap-superuser CREATEROLE ADMIN row may remain)
    SELECT COUNT(*) INTO v_residual_temp_grants
    FROM pg_auth_members m
    JOIN pg_roles g ON g.oid = m.grantor
    WHERE m.roleid IN ('lotba_bi_readonly'::regrole, 'lotba_bi_definer'::regrole)
      AND m.member = 'postgres'::regrole
      AND (NOT g.rolsuper OR NOT m.admin_option);
    IF v_residual_temp_grants > 0 THEN
        RAISE EXCEPTION 'Security check failed: temporary SET/INHERIT grant on BI roles was not revoked (%)', v_residual_temp_grants;
    END IF;

    -- 7c. Verify zero CREATE privilege on permanent schemas for lotba_bi_readonly
    SELECT string_agg(n.nspname, ', ') INTO v_creatable_schemas
    FROM pg_namespace n
    WHERE n.nspname NOT LIKE 'pg_%'
      AND n.nspname <> 'information_schema'
      AND has_schema_privilege('lotba_bi_readonly', n.oid, 'CREATE');
    IF v_creatable_schemas IS NOT NULL THEN
        RAISE EXCEPTION 'Security check failed: lotba_bi_readonly has CREATE on schemas: %', v_creatable_schemas;
    END IF;

    -- 7d. Verify zero write ACLs on public relations for lotba_bi_readonly
    SELECT string_agg(c.relname, ', ') INTO v_writable_objects
    FROM pg_class c
    WHERE c.relnamespace = 'public'::regnamespace
      AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
      AND (
          has_table_privilege('lotba_bi_readonly', c.oid, 'INSERT')
          OR has_table_privilege('lotba_bi_readonly', c.oid, 'UPDATE')
          OR has_table_privilege('lotba_bi_readonly', c.oid, 'DELETE')
          OR has_table_privilege('lotba_bi_readonly', c.oid, 'TRUNCATE')
      );
    IF v_writable_objects IS NOT NULL THEN
        RAISE EXCEPTION 'Security check failed: lotba_bi_readonly has write ACL on: %', v_writable_objects;
    END IF;

    -- 7e. Verify SELECT in public is strictly limited to the 4 authorized BI views
    SELECT string_agg(c.relname, ', ') INTO v_extra_readable_objects
    FROM pg_class c
    WHERE c.relnamespace = 'public'::regnamespace
      AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
      AND c.relname NOT IN (
          'vw_bi_session_overview',
          'vw_bi_behavior_metrics',
          'vw_bi_speed_decisions',
          'vw_bi_recovery_opportunities'
      )
      AND has_table_privilege('lotba_bi_readonly', c.oid, 'SELECT');
    IF v_extra_readable_objects IS NOT NULL THEN
        RAISE EXCEPTION 'Security check failed: lotba_bi_readonly can SELECT unauthorized objects: %', v_extra_readable_objects;
    END IF;

    -- 7f. Verify complete removal of residual anon/authenticated privileges (including MAINTAIN) on all 3 base tables
    SELECT COUNT(*) INTO v_residual_anon_auth
    FROM pg_class c
    WHERE c.relnamespace = 'public'::regnamespace
      AND c.relname IN ('sessions', 'session_events', 'session_recovery_opportunities')
      AND (
          has_table_privilege('anon', c.oid, 'SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER, MAINTAIN')
          OR has_table_privilege('authenticated', c.oid, 'SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER, MAINTAIN')
      );
    IF v_residual_anon_auth > 0 THEN
        RAISE EXCEPTION 'Security check failed: residual privileges remain for anon/authenticated';
    END IF;

    -- 7g. Verify individual privileges of lotba_bi_readonly and service_role
    IF NOT (
        has_table_privilege('lotba_bi_readonly', 'public.vw_bi_session_overview', 'SELECT')
        AND has_table_privilege('lotba_bi_readonly', 'public.vw_bi_behavior_metrics', 'SELECT')
        AND has_table_privilege('lotba_bi_readonly', 'public.vw_bi_speed_decisions', 'SELECT')
        AND has_table_privilege('lotba_bi_readonly', 'public.vw_bi_recovery_opportunities', 'SELECT')
    ) THEN
        RAISE EXCEPTION 'Security check failed: lotba_bi_readonly missing SELECT on authorized BI views';
    END IF;

    IF has_function_privilege('lotba_bi_readonly', 'public.ingest_session_report(jsonb)', 'EXECUTE') THEN
        RAISE EXCEPTION 'Security check failed: lotba_bi_readonly can EXECUTE ingest_session_report';
    END IF;

    IF NOT (
        has_table_privilege('service_role', 'public.sessions', 'SELECT')
        AND has_table_privilege('service_role', 'public.sessions', 'INSERT')
        AND has_table_privilege('service_role', 'public.session_events', 'SELECT')
        AND has_table_privilege('service_role', 'public.session_events', 'INSERT')
        AND has_table_privilege('service_role', 'public.session_recovery_opportunities', 'SELECT')
        AND has_table_privilege('service_role', 'public.session_recovery_opportunities', 'INSERT')
        AND has_sequence_privilege('service_role', 'public.session_events_id_seq', 'USAGE')
        AND has_sequence_privilege('service_role', 'public.session_events_id_seq', 'SELECT')
        AND has_sequence_privilege('service_role', 'public.session_recovery_opportunities_id_seq', 'USAGE')
        AND has_sequence_privilege('service_role', 'public.session_recovery_opportunities_id_seq', 'SELECT')
        AND has_function_privilege('service_role', 'public.ingest_session_report(jsonb)', 'EXECUTE')
    ) THEN
        RAISE EXCEPTION 'Security check failed: service_role lost required backend privileges';
    END IF;

    -- 7h. Compare live results read as lotba_bi_readonly against pre-migration checksums
    WITH after_checksums AS (
        SELECT 'vw_bi_session_overview'::text AS view_name, v_r_overview AS rows, v_m_overview AS md5
        UNION ALL
        SELECT 'vw_bi_behavior_metrics'::text, v_r_behavior, v_m_behavior
        UNION ALL
        SELECT 'vw_bi_speed_decisions'::text, v_r_decisions, v_m_decisions
        UNION ALL
        SELECT 'vw_bi_recovery_opportunities'::text, v_r_recovery, v_m_recovery
    )
    SELECT COUNT(*) INTO v_checksum_mismatches
    FROM _bi_checksums_before b
    FULL OUTER JOIN after_checksums a USING (view_name)
    WHERE b.rows IS DISTINCT FROM a.rows
       OR b.md5 IS DISTINCT FROM a.md5;

    IF v_checksum_mismatches > 0 THEN
        RAISE EXCEPTION 'Data equivalence check failed: BI views MD5 mismatch (%)', v_checksum_mismatches;
    END IF;
END
$$;

COMMIT;
