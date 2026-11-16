const express = require('express');
const { validateSessionReport } = require('../validators/sessionReportValidator');

function createSessionsRouter(sessionStore) {
  const router = express.Router();

  router.post('/', async (req, res, next) => {
    const validation = validateSessionReport(req.body);
    if (!validation.valid) {
      return res.status(400).json({
        error: 'invalid_payload',
        details: validation.details
      });
    }

    try {
      const result = await sessionStore.save(req.body);
      if (result.status === 'already_exists') {
        return res.status(200).json({
          status: 'already_exists',
          runId: result.runId
        });
      }

      return res.status(201).json({
        status: 'created',
        runId: result.runId
      });
    } catch (err) {
      return next(err);
    }
  });

  return router;
}

module.exports = {
  createSessionsRouter
};
