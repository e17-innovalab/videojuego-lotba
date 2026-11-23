const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const os = require('node:os');
const path = require('node:path');

const { createApp } = require('../src/app');
const { createLocalSessionStore } = require('../src/storage/localSessionStore');

async function withServer(fn) {
  const tempDir = await fs.mkdtemp(path.join(os.tmpdir(), 'lotba-sessions-test-'));
  const store = createLocalSessionStore(tempDir);
  const app = createApp({ sessionStore: store });

  const server = await new Promise((resolve) => {
    const s = app.listen(0, '127.0.0.1', () => resolve(s));
  });

  const { port } = server.address();
  const baseUrl = `http://127.0.0.1:${port}`;

  try {
    await fn({ baseUrl, tempDir });
  } finally {
    await new Promise((resolve, reject) => server.close((err) => (err ? reject(err) : resolve())));
    await fs.rm(tempDir, { recursive: true, force: true });
  }
}

function makeValidReport(runId = 'run_test_001') {
  return {
    metadata: {
      reportVersion: '1.0',
      schemaVersion: 13,
      runId,
      gameVersion: '0.3.0',
      platform: 'WindowsEditor',
      startedAtUtc: '2026-11-16T23:12:18.0000000Z',
      finishedAtUtc: '2026-11-16T23:12:20.0000000Z',
      route: 'Route_LaBocaToObelisco',
      seed: 2710,
      routeDistanceMetres: 0
    },
    summary: {
      ending: 'brake',
      timeSeconds: 2,
      distanceMetres: 36,
      peakSpeedKmh: 66.5,
      score: 10,
      gems: 1,
      collisions: 0
    },
    events: [
      { sequence: 1, type: 'run_started', timeSeconds: 0 },
      { sequence: 2, type: 'run_finished', detail: 'brake', timeSeconds: 2 }
    ]
  };
}

test('GET /api/health returns 200 { status: "ok" }', async () => {
  await withServer(async ({ baseUrl }) => {
    const res = await fetch(`${baseUrl}/api/health`);
    assert.equal(res.status, 200);
    const body = await res.json();
    assert.deepEqual(body, { status: 'ok' });
  });
});

test('POST /api/v1/sessions rejects invalid payload with 400', async () => {
  await withServer(async ({ baseUrl }) => {
    const res = await fetch(`${baseUrl}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ metadata: { runId: '../bad' } })
    });
    assert.equal(res.status, 400);
    const body = await res.json();
    assert.equal(body.error, 'invalid_payload');
    assert.ok(Array.isArray(body.details) && body.details.length > 0);
  });
});

test('POST /api/v1/sessions rejects malformed JSON with 400 JSON error', async () => {
  await withServer(async ({ baseUrl }) => {
    const res = await fetch(`${baseUrl}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: '{ invalid json'
    });
    assert.equal(res.status, 400);
    const body = await res.json();
    assert.equal(body.error, 'invalid_json');
  });
});

test('POST /api/v1/sessions stores valid SessionReport once and deduplicates retries', async () => {
  await withServer(async ({ baseUrl, tempDir }) => {
    const report = makeValidReport('abc123_run');

    // First POST -> 201 Created
    const first = await fetch(`${baseUrl}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(report)
    });
    assert.equal(first.status, 201);
    assert.deepEqual(await first.json(), { status: 'created', runId: 'abc123_run' });

    // Second POST with same runId -> 200 already_exists
    const second = await fetch(`${baseUrl}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(report)
    });
    assert.equal(second.status, 200);
    assert.deepEqual(await second.json(), { status: 'already_exists', runId: 'abc123_run' });

    // Confirm only one file exists and its JSON matches payload
    const files = await fs.readdir(tempDir);
    assert.deepEqual(files, ['session_abc123_run.json']);

    const saved = JSON.parse(await fs.readFile(path.join(tempDir, files[0]), 'utf8'));
    assert.deepEqual(saved, report);
  });
});

test('POST /api/v1/sessions rejects payloads larger than 2mb with 413', async () => {
  await withServer(async ({ baseUrl }) => {
    const oversizedReport = makeValidReport('oversized_run_001');
    oversizedReport.events.push({
      sequence: 3,
      type: 'oversized_test',
      detail: 'x'.repeat(2 * 1024 * 1024 + 1024),
      timeSeconds: 2.5
    });

    const res = await fetch(`${baseUrl}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(oversizedReport)
    });
    assert.equal(res.status, 413);
    const body = await res.json();
    assert.deepEqual(body, { error: 'payload_too_large' });
  });
});

test('POST /api/v1/sessions accepts schemaVersion 19 and schemaVersion 20 payloads without breaking schema 13', async () => {
  await withServer(async ({ baseUrl, tempDir }) => {
    const v13 = makeValidReport('run_schema_13');
    const v19 = {
      ...makeValidReport('run_schema_19'),
      metadata: { ...makeValidReport('run_schema_19').metadata, schemaVersion: 19, gameVersion: '0.4.0' },
      summary: {
        ...makeValidReport('run_schema_19').summary,
        pinkGemsCollected: 3,
        pinkGemsMissed: 1,
        pinkGemNearMisses: 1,
        bestPinkGemStreak: 2,
        pinkDodgeCleared: 1,
        pinkDodgeCollisions: 0
      }
    };
    const v20 = {
      metadata: {
        ...makeValidReport('run_schema_20').metadata,
        schemaVersion: 20,
        gameVersion: '0.4.0',
        braking: {
          mode: 'hold_to_stop',
          decelerationMetresPerSecondSquared: 28.5,
          releaseAccelerationMetresPerSecondSquared: 12.0
        }
      },
      summary: {
        ...makeValidReport('run_schema_20').summary,
        recoveryBoosts: 1,
        recoveryAttempts: 1,
        brakePresses: 3,
        brakeReleases: 2,
        brakeHeldSeconds: 1.45
      },
      events: [
        { sequence: 1, type: 'run_started', timeSeconds: 0 },
        {
          sequence: 2,
          type: 'boost_activated',
          timeSeconds: 1.2,
          turboConfiguration: {
            durationSeconds: 3.5,
            speedBonusMetresPerSecond: 6.0,
            gemMultiplier: 2,
            pointMultiplier: 2
          }
        },
        { sequence: 3, type: 'run_finished', detail: 'brake', timeSeconds: 2.0 }
      ]
    };

    for (const report of [v13, v19, v20]) {
      const first = await fetch(`${baseUrl}/api/v1/sessions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(report)
      });
      assert.equal(first.status, 201);
      assert.deepEqual(await first.json(), { status: 'created', runId: report.metadata.runId });

      const retry = await fetch(`${baseUrl}/api/v1/sessions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(report)
      });
      assert.equal(retry.status, 200);
      assert.deepEqual(await retry.json(), { status: 'already_exists', runId: report.metadata.runId });
    }

    const files = (await fs.readdir(tempDir)).sort();
    assert.deepEqual(files, [
      'session_run_schema_13.json',
      'session_run_schema_19.json',
      'session_run_schema_20.json'
    ]);
  });
});

test('Delivery state machine: transient 5xx stays pending and retries; permanent 4xx becomes rejected; orphan report binds to BaseUrl', async () => {
  // Mirrors SessionReportTransport.IsTransientFailure and status transitions
  const isTransientFailure = (code) => code === 0 || code === 408 || code === 429 || code >= 500;
  const applyResponse = (delivery, code, body, expectedRunId) => {
    const confirmed =
      body &&
      body.runId === expectedRunId &&
      ((code === 201 && body.status === 'created') || (code === 200 && body.status === 'already_exists'));
    if (confirmed) {
      delivery.status = 'delivered';
      delivery.lastError = '';
      return;
    }
    delivery.status = isTransientFailure(code) ? 'pending' : 'rejected';
    delivery.lastError = `HTTP ${code}`;
  };

  // 1. Orphan report without destination binds to configured BaseUrl
  const orphanRecord = { runId: 'run_orphan_01', originalDestination: '', deliveries: [] };
  const baseUrl = 'https://telemetry.example.com';
  if (baseUrl && !orphanRecord.originalDestination && orphanRecord.deliveries.length === 0) {
    orphanRecord.originalDestination = baseUrl;
    orphanRecord.deliveries.push({ destination: baseUrl, status: 'pending', lastError: '' });
  }
  assert.equal(orphanRecord.originalDestination, baseUrl);
  assert.equal(orphanRecord.deliveries[0].status, 'pending');

  // 2. Transient 5xx keeps status pending
  applyResponse(orphanRecord.deliveries[0], 503, null, 'run_orphan_01');
  assert.equal(orphanRecord.deliveries[0].status, 'pending');

  // 3. Retry succeeds with 201 created -> delivered
  applyResponse(orphanRecord.deliveries[0], 201, { status: 'created', runId: 'run_orphan_01' }, 'run_orphan_01');
  assert.equal(orphanRecord.deliveries[0].status, 'delivered');

  // 4. Duplicate retry with 200 already_exists -> delivered
  const dupDelivery = { destination: baseUrl, status: 'pending', lastError: '' };
  applyResponse(dupDelivery, 200, { status: 'already_exists', runId: 'run_dup_01' }, 'run_dup_01');
  assert.equal(dupDelivery.status, 'delivered');

  // 5. Permanent 4xx (e.g. 400) -> rejected
  const badDelivery = { destination: baseUrl, status: 'pending', lastError: '' };
  applyResponse(badDelivery, 400, { error: 'invalid_payload' }, 'run_bad_01');
  assert.equal(badDelivery.status, 'rejected');
});
