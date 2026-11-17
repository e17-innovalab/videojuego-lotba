const test = require('node:test');
const assert = require('node:assert/strict');

const { createSupabaseSessionStore } = require('../src/storage/supabaseSessionStore');

function makeReport(runId = 'run_supa_001') {
  return {
    metadata: {
      reportVersion: '1.0',
      runId
    },
    summary: { ending: 'brake' },
    events: []
  };
}

test('supabaseSessionStore returns created when RPC returns created', async () => {
  let receivedCall = null;
  const fakeClient = {
    async rpc(fnName, params) {
      receivedCall = { fnName, params };
      return { data: { status: 'created', runId: 'run_supa_001' }, error: null };
    }
  };

  const store = createSupabaseSessionStore({ client: fakeClient });
  const report = makeReport('run_supa_001');
  const result = await store.save(report);

  assert.equal(receivedCall.fnName, 'ingest_session_report');
  assert.deepEqual(receivedCall.params, { payload: report });
  assert.deepEqual(result, { status: 'created', runId: 'run_supa_001' });
});

test('supabaseSessionStore returns already_exists when RPC returns already_exists', async () => {
  const fakeClient = {
    async rpc() {
      return { data: { status: 'already_exists', runId: 'run_supa_dup' }, error: null };
    }
  };

  const store = createSupabaseSessionStore({ client: fakeClient });
  const result = await store.save(makeReport('run_supa_dup'));

  assert.deepEqual(result, { status: 'already_exists', runId: 'run_supa_dup' });
});

test('supabaseSessionStore throws sanitized error when RPC returns error', async () => {
  const fakeClient = {
    async rpc() {
      return { data: null, error: { message: 'permission denied' } };
    }
  };

  const store = createSupabaseSessionStore({ client: fakeClient });
  await assert.rejects(
    () => store.save(makeReport('run_supa_err')),
    (err) => err instanceof Error && err.message.includes('permission denied')
  );
});

test('supabaseSessionStore throws when RPC returns unexpected response', async () => {
  const fakeClient = {
    async rpc() {
      return { data: { status: 'unknown_status' }, error: null };
    }
  };

  const store = createSupabaseSessionStore({ client: fakeClient });
  await assert.rejects(
    () => store.save(makeReport('run_supa_bad')),
    (err) => err instanceof Error && err.message === 'Unexpected response from ingest_session_report RPC'
  );
});
