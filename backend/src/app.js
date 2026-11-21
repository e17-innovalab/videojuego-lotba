const express = require('express');
const { createSessionsRouter } = require('./routes/sessions');
const { createLocalSessionStore } = require('./storage/localSessionStore');

function createApp(options = {}) {
  const sessionStore = options.sessionStore || createLocalSessionStore();
  const app = express();

  app.use(express.json({ limit: '2mb' }));

  app.get('/api/health', (req, res) => {
    res.status(200).json({ status: 'ok' });
  });

  app.use('/api/v1/sessions', createSessionsRouter(sessionStore));

  // Handle malformed JSON and unexpected errors without leaking stack traces or HTML
  app.use((err, req, res, next) => {
    if (err instanceof SyntaxError && err.status === 400 && 'body' in err) {
      return res.status(400).json({
        error: 'invalid_json',
        details: ['Malformed JSON body']
      });
    }

    if (err && (err.type === 'entity.too.large' || err.status === 413)) {
      return res.status(413).json({
        error: 'payload_too_large'
      });
    }

    console.error('[backend] Unexpected error:', err);
    return res.status(500).json({
      error: 'internal_server_error'
    });
  });

  return app;
}

module.exports = {
  createApp
};
