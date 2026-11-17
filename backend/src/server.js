const { createApp } = require('./app');
const { createLocalSessionStore } = require('./storage/localSessionStore');
const { createSupabaseSessionStore } = require('./storage/supabaseSessionStore');

function resolveSessionStore() {
  const supabaseUrl = process.env.SUPABASE_URL ? process.env.SUPABASE_URL.trim() : '';
  const supabaseSecretKey = process.env.SUPABASE_SECRET_KEY
    ? process.env.SUPABASE_SECRET_KEY.trim()
    : '';

  const hasUrl = supabaseUrl.length > 0;
  const hasKey = supabaseSecretKey.length > 0;

  if (hasUrl !== hasKey) {
    console.error('[backend] Incomplete Supabase configuration: both SUPABASE_URL and SUPABASE_SECRET_KEY must be set together.');
    process.exit(1);
  }

  if (hasUrl && hasKey) {
    console.log('[backend] Storage mode: supabase');
    return createSupabaseSessionStore({
      url: supabaseUrl,
      secretKey: supabaseSecretKey
    });
  }

  console.log('[backend] Storage mode: local');
  return createLocalSessionStore();
}

const port = Number(process.env.PORT) || 3000;
const sessionStore = resolveSessionStore();
const app = createApp({ sessionStore });

app.listen(port, () => {
  console.log(`[backend] Listening on http://localhost:${port}`);
});
