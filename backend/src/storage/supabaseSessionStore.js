const { createClient } = require('@supabase/supabase-js');

function createSupabaseSessionStore({ url, secretKey, client } = {}) {
  if (!client && (!url || !secretKey)) {
    throw new Error('Supabase configuration is incomplete');
  }

  const supabase = client || createClient(url, secretKey, {
    auth: {
      persistSession: false,
      autoRefreshToken: false
    },
    realtime: {
      // Node 20 lacks global WebSocket; we only use HTTP RPC, never Realtime.
      transport: class DisabledWebSocket {}
    }
  });

  async function save(report) {
    const runId = report.metadata.runId;

    const { data, error } = await supabase.rpc('ingest_session_report', {
      payload: report
    });

    if (error) {
      const message = typeof error.message === 'string' && error.message.length > 0
        ? error.message
        : 'unknown error';
      throw new Error(`Supabase RPC ingest_session_report failed: ${message}`);
    }

    if (!data || (data.status !== 'created' && data.status !== 'already_exists')) {
      throw new Error('Unexpected response from ingest_session_report RPC');
    }

    return {
      status: data.status,
      runId: data.runId || runId
    };
  }

  return {
    save
  };
}

module.exports = {
  createSupabaseSessionStore
};
