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

function makeEmptyJsonUtilityRecovery() {
  return {
    offerId: '',
    attemptId: '',
    outcome: '',
    cause: '',
    section: 0,
    decisionNumber: 0,
    accepted: false,
    eligibleLossSequences: [],
    eligiblePoints: 0,
    offeredPoints: 0,
    returnedPoints: 0,
    eligibleGems: 0,
    offeredGems: 0,
    returnedGems: 0,
    offerDistanceMetres: 0,
    startDistanceMetres: 0,
    endDistanceMetres: 0,
    challengeMetres: 0,
    startTimeSeconds: 0,
    endTimeSeconds: 0,
    fogLocalZ: 0,
    pointLossFraction: 0,
    gemLossFraction: 0,
    minimumPointLoss: 0,
    turbo: {
      lifetimeMode: '',
      durationSeconds: 0,
      speedBonusMetresPerSecond: 0,
      gemMultiplier: 0,
      pointMultiplier: 0
    }
  };
}

function makeEmptyJsonUtilityTurbo() {
  return {
    lifetimeMode: '',
    durationSeconds: 0,
    speedBonusMetresPerSecond: 0,
    gemMultiplier: 0,
    pointMultiplier: 0
  };
}

function makeV20FinalReport(runId = 'run_schema_20_v050', withJourney = true) {
  return {
    metadata: {
      reportVersion: '1.0',
      schemaVersion: 20,
      runId,
      gameVersion: '0.5.0',
      platform: 'Android',
      startedAtUtc: '2026-11-26T14:00:00.0000000Z',
      finishedAtUtc: '2026-11-26T14:01:30.0000000Z',
      route: 'Route_ReservaToPalermo',
      seed: 1001,
      routeDistanceMetres: 1800,
      braking: {
        mode: 'hold_to_stop',
        decelerationMetresPerSecondSquared: 28.5,
        releaseAccelerationMetresPerSecondSquared: 12.0
      }
    },
    summary: {
      ending: 'brake',
      timeSeconds: 78.4,
      realTimeSeconds: 90.0,
      distanceMetres: 920.5,
      peakSpeedKmh: 72.0,
      score: 6420,
      gems: 58,
      collisions: 3,
      voluntaryBoosts: 1,
      bonusGems: 4,
      manualBoosts: 1,
      recoveryBoosts: 0,
      recoveryOffers: 2,
      recoveryAttempts: 2,
      recoverySucceeded: 1,
      recoveryFailed: 1,
      recoveryDeclined: 0,
      recoveryInterrupted: 0,
      recoveryUnavailable: 0,
      recoveryOffersClosed: 0,
      recoveryZoneCollisions: 1,
      recoveryZonePointsLost: 107,
      recoveryZoneGemsLost: 21,
      recoveryNetPoints: 2314,
      recoveryNetGems: 222,
      purchasedGemMultiplier: 2,
      purchasedPointMultiplier: 2,
      pointsLost: 4184,
      pointsRecovered: 2421,
      gemsLost: 425,
      gemsRecovered: 243,
      gemBalance: 38,
      boostCollisions: 0,
      gemsSpent: 20,
      speedUpgrades: 1,
      purchasedSpeedKmh: 54.0,
      decisionsShown: 3,
      decisionsKept: 1,
      brakePresses: 2,
      brakeReleases: 1,
      brakeHeldSeconds: 1.8,
      pinkGemsCollected: 5,
      pinkGemsMissed: 1,
      pinkGemNearMisses: 1,
      bestPinkGemStreak: 4,
      pinkDodgeCleared: 2,
      pinkDodgeCollisions: 0
    },
    events: [
      {
        sequence: 1,
        type: 'run_started',
        zone: 'ReservaEcologica',
        detail: '',
        timeSeconds: 0,
        distanceMetres: 0,
        speedKmh: 43.2,
        lane: 1,
        score: 0,
        scoreDelta: 0,
        gemsCollected: 0,
        gemBalance: 0,
        gemDelta: 0,
        duringBoost: false,
        decisionNumber: 0,
        purchasedSpeedKmh: 43.2,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: 1,
        recovery: makeEmptyJsonUtilityRecovery(),
        lossOrigin: '',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      },
      {
        sequence: 2,
        type: 'transition_decision_shown',
        zone: 'Transicion_1',
        detail: '',
        timeSeconds: 22.0,
        distanceMetres: 300.0,
        speedKmh: 0,
        lane: 1,
        score: 2100,
        scoreDelta: 0,
        gemsCollected: 25,
        gemBalance: 25,
        gemDelta: 0,
        duringBoost: false,
        decisionNumber: 1,
        purchasedSpeedKmh: 43.2,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: 1,
        recovery: makeEmptyJsonUtilityRecovery(),
        lossOrigin: '',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      },
      {
        sequence: 3,
        type: 'speed_upgrade_purchased',
        zone: 'Transicion_1',
        detail: '',
        timeSeconds: 22.0,
        distanceMetres: 300.0,
        speedKmh: 0,
        lane: 1,
        score: 2100,
        scoreDelta: 0,
        gemsCollected: 25,
        gemBalance: 5,
        gemDelta: -20,
        duringBoost: false,
        decisionNumber: 1,
        purchasedSpeedKmh: 49.68,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: 1,
        recovery: makeEmptyJsonUtilityRecovery(),
        lossOrigin: '',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      },
      {
        sequence: 4,
        type: 'obstacle_hit',
        zone: 'LaBoca',
        detail: 'Cone',
        timeSeconds: 35.0,
        distanceMetres: 480.0,
        speedKmh: 54.0,
        lane: 0,
        score: 1800,
        scoreDelta: -2421,
        gemsCollected: 30,
        gemBalance: 10,
        gemDelta: -243,
        duringBoost: false,
        decisionNumber: 0,
        purchasedSpeedKmh: 54.0,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: 3,
        recovery: makeEmptyJsonUtilityRecovery(),
        lossOrigin: 'normal',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      },
      {
        sequence: 5,
        type: 'transition_decision_shown',
        zone: 'Transicion_2',
        detail: '',
        timeSeconds: 48.0,
        distanceMetres: 600.0,
        speedKmh: 0,
        lane: 1,
        score: 3200,
        scoreDelta: 0,
        gemsCollected: 40,
        gemBalance: 20,
        gemDelta: 0,
        duringBoost: false,
        decisionNumber: 2,
        purchasedSpeedKmh: 54.0,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: 3,
        recovery: makeEmptyJsonUtilityRecovery(),
        lossOrigin: '',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      },
      {
        sequence: 6,
        type: 'recovery_entered',
        zone: 'Transicion_2',
        detail: 'transition_choice',
        timeSeconds: 48.0,
        distanceMetres: 600.0,
        speedKmh: 0,
        lane: 1,
        score: 3200,
        scoreDelta: 0,
        gemsCollected: 40,
        gemBalance: 20,
        gemDelta: 0,
        duringBoost: false,
        decisionNumber: 2,
        purchasedSpeedKmh: 54.0,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: 3,
        recovery: {
          ...makeEmptyJsonUtilityRecovery(),
          offerId: 'trans-offer-2',
          attemptId: 'attempt-1',
          outcome: 'active',
          section: 2,
          decisionNumber: 2,
          accepted: true,
          eligibleLossSequences: [4],
          eligiblePoints: 2421,
          offeredPoints: 2421,
          returnedPoints: 0,
          eligibleGems: 243,
          offeredGems: 243,
          returnedGems: 0
        },
        lossOrigin: '',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      },
      {
        sequence: 7,
        type: 'recovery_succeeded',
        zone: 'Obelisco',
        detail: 'gem_collected',
        timeSeconds: 56.5,
        distanceMetres: 740.0,
        speedKmh: 58.32,
        lane: 2,
        score: 6420,
        scoreDelta: 2421,
        gemsCollected: 58,
        gemBalance: 38,
        gemDelta: 243,
        duringBoost: false,
        decisionNumber: 0,
        purchasedSpeedKmh: 58.32,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: 5,
        recovery: {
          ...makeEmptyJsonUtilityRecovery(),
          offerId: 'trans-offer-2',
          attemptId: 'attempt-1',
          outcome: 'succeeded',
          cause: 'gem_collected',
          section: 2,
          decisionNumber: 2,
          accepted: true,
          eligibleLossSequences: [4],
          eligiblePoints: 2421,
          offeredPoints: 2421,
          returnedPoints: 2421,
          eligibleGems: 243,
          offeredGems: 243,
          returnedGems: 243
        },
        lossOrigin: '',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      },
      {
        sequence: 8,
        type: 'run_finished',
        zone: 'Obelisco',
        detail: 'brake',
        timeSeconds: 78.4,
        distanceMetres: 920.5,
        speedKmh: 0,
        lane: 1,
        score: 6420,
        scoreDelta: 0,
        gemsCollected: 58,
        gemBalance: 38,
        gemDelta: 0,
        duringBoost: false,
        decisionNumber: 0,
        purchasedSpeedKmh: 58.32,
        turboConfiguration: makeEmptyJsonUtilityTurbo(),
        transitionSection: -1,
        recovery: makeEmptyJsonUtilityRecovery(),
        lossOrigin: '',
        gemRewardMultiplier: 1,
        pointRewardMultiplier: 1
      }
    ],
    recoveryOpportunities: [
      {
        offerId: 'trans-offer-2',
        attemptId: 'attempt-1',
        outcome: 'succeeded',
        cause: 'gem_collected',
        section: 2,
        decisionNumber: 2,
        accepted: true,
        eligibleLossSequences: [4],
        eligiblePoints: 2421,
        offeredPoints: 2421,
        returnedPoints: 2421,
        eligibleGems: 243,
        offeredGems: 243,
        returnedGems: 243,
        offerDistanceMetres: 600.0,
        startDistanceMetres: 600.0,
        endDistanceMetres: 740.0,
        challengeMetres: 140.0,
        startTimeSeconds: 48.0,
        endTimeSeconds: 56.5,
        fogLocalZ: 0,
        pointLossFraction: 0.5,
        gemLossFraction: 0.5,
        minimumPointLoss: 50,
        turbo: makeEmptyJsonUtilityTurbo()
      }
    ],
    journey: withJourney
      ? {
          journeyId: '4f9b7c1e8a204d199b0e112233445566',
          schoolName: 'Escuela Técnica N° 17 Cornelio Saavedra',
          schoolShift: 'mañana',
          schoolDate: '2026-11-26'
        }
      : {
          journeyId: '',
          schoolName: '',
          schoolShift: '',
          schoolDate: ''
        }
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

test('POST /api/v1/sessions accepts schemaVersion 13, 19, early 20, and finalized v0.5.0 20 (with and without journey)', async () => {
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
    const v20Early = {
      metadata: {
        ...makeValidReport('run_schema_20_early').metadata,
        schemaVersion: 20,
        gameVersion: '0.4.1',
        braking: {
          mode: 'hold_to_stop',
          decelerationMetresPerSecondSquared: 28.5,
          releaseAccelerationMetresPerSecondSquared: 12.0
        }
      },
      summary: {
        ...makeValidReport('run_schema_20_early').summary,
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
    const v20FinalWithJourney = makeV20FinalReport('run_schema_20_final_journey', true);
    const v20FinalEmptyJourney = makeV20FinalReport('run_schema_20_final_no_journey', false);

    for (const report of [v13, v19, v20Early, v20FinalWithJourney, v20FinalEmptyJourney]) {
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
      'session_run_schema_20_early.json',
      'session_run_schema_20_final_journey.json',
      'session_run_schema_20_final_no_journey.json'
    ]);
  });
});

test('POST /api/v1/sessions validates internal structure of recoveryOpportunities and journey', async () => {
  await withServer(async ({ baseUrl }) => {
    const badCases = [
      {
        name: 'non-array recoveryOpportunities',
        mutate: (r) => {
          r.recoveryOpportunities = { offerId: 'x' };
        }
      },
      {
        name: 'opportunity missing offerId',
        mutate: (r) => {
          r.recoveryOpportunities[0].offerId = '';
        }
      },
      {
        name: 'non-integer eligibleLossSequences',
        mutate: (r) => {
          r.recoveryOpportunities[0].eligibleLossSequences = ['seq_4'];
        }
      },
      {
        name: 'partial journey missing schoolName',
        mutate: (r) => {
          r.journey = { journeyId: 'j_01', schoolName: '', schoolShift: 'tarde', schoolDate: '2026-11-26' };
        }
      },
      {
        name: 'invalid calendar date in journey.schoolDate',
        mutate: (r) => {
          r.journey.schoolDate = '2026-02-30';
        }
      },
      {
        name: 'non-empty event.recovery snapshot missing offerId',
        mutate: (r) => {
          r.events[5].recovery.offerId = '';
        }
      }
    ];

    for (const tc of badCases) {
      const report = makeV20FinalReport(`bad_${tc.name.replace(/\W+/g, '_')}`, true);
      tc.mutate(report);
      const res = await fetch(`${baseUrl}/api/v1/sessions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(report)
      });
      assert.equal(res.status, 400, `Expected 400 for case: ${tc.name}`);
      const body = await res.json();
      assert.equal(body.error, 'invalid_payload');
      assert.ok(body.details.length > 0);
    }
  });
});

test('Delivery state machine: offline queue, online reconnect, 45s cooldown, periodic 15s tick, and orphan binding', async () => {
  const RETRY_COOLDOWN_SECONDS = 45;
  const isTransientFailure = (code) => code === 0 || code === 408 || code === 429 || code >= 500;

  let now = 100;
  let online = false;
  let wasOnline = false;
  const destinationRetryAfter = new Map();
  const baseUrl = 'https://telemetry.example.com';

  const isInCooldown = (dest) => destinationRetryAfter.has(dest) && now < destinationRetryAfter.get(dest);

  const orphanRecord = { runId: 'run_orphan_01', originalDestination: '', deliveries: [] };
  const tryBindOrphan = (rec) => {
    if (baseUrl && !rec.originalDestination && rec.deliveries.length === 0) {
      rec.originalDestination = baseUrl;
      rec.deliveries.push({ destination: baseUrl, status: 'pending', lastError: '' });
    }
  };

  const retryPendingIfReady = (records) => {
    if (!online) {
      wasOnline = false;
      return [];
    }
    if (!wasOnline) {
      wasOnline = true;
      destinationRetryAfter.clear();
    }
    const dispatched = [];
    for (const rec of records) {
      tryBindOrphan(rec);
      for (const d of rec.deliveries) {
        if (d.status === 'pending' && d.destination && !isInCooldown(d.destination)) {
          dispatched.push({ runId: rec.runId, delivery: d });
        }
      }
    }
    return dispatched;
  };

  const applyResponse = (delivery, code, body, expectedRunId) => {
    const confirmed =
      body &&
      body.runId === expectedRunId &&
      ((code === 201 && body.status === 'created') || (code === 200 && body.status === 'already_exists'));
    if (confirmed) {
      delivery.status = 'delivered';
      delivery.lastError = '';
      destinationRetryAfter.delete(delivery.destination);
      return;
    }
    const retry = isTransientFailure(code);
    delivery.status = retry ? 'pending' : 'rejected';
    delivery.lastError = `HTTP ${code}`;
    if (retry) {
      destinationRetryAfter.set(delivery.destination, now + RETRY_COOLDOWN_SECONDS);
      if (!online) wasOnline = false;
    }
  };

  // 1. Offline periodic tick (t=115): nothing sent while offline
  now = 115;
  assert.equal(retryPendingIfReady([orphanRecord]).length, 0);

  // 2. Network comes online (t=130): orphan binds and dispatches immediately; server returns transient 503
  online = true;
  now = 130;
  let batch = retryPendingIfReady([orphanRecord]);
  assert.equal(batch.length, 1);
  assert.equal(orphanRecord.originalDestination, baseUrl);
  applyResponse(batch[0].delivery, 503, null, 'run_orphan_01');
  assert.equal(orphanRecord.deliveries[0].status, 'pending');

  // 3. Periodic ticks at t=145 (+15s) and t=160 (+30s) respect the 45s cooldown (no HTTP spam)
  now = 145;
  assert.equal(retryPendingIfReady([orphanRecord]).length, 0);
  now = 160;
  assert.equal(retryPendingIfReady([orphanRecord]).length, 0);

  // 4. Periodic tick at t=175 (+45s): cooldown expired -> retries and succeeds with 201 created
  now = 175;
  batch = retryPendingIfReady([orphanRecord]);
  assert.equal(batch.length, 1);
  applyResponse(batch[0].delivery, 201, { status: 'created', runId: 'run_orphan_01' }, 'run_orphan_01');
  assert.equal(orphanRecord.deliveries[0].status, 'delivered');

  // 5. Offline transient failure (HTTP 0) clears cooldown immediately upon Wi-Fi reconnect
  const offlineRun = {
    runId: 'run_wifi_drop_02',
    originalDestination: baseUrl,
    deliveries: [{ destination: baseUrl, status: 'pending', lastError: '' }]
  };
  online = false;
  applyResponse(offlineRun.deliveries[0], 0, null, 'run_wifi_drop_02');
  now = 180; // only +5s later (well before 45s), Wi-Fi reconnects
  online = true;
  batch = retryPendingIfReady([offlineRun]);
  assert.equal(batch.length, 1);
  applyResponse(batch[0].delivery, 200, { status: 'already_exists', runId: 'run_wifi_drop_02' }, 'run_wifi_drop_02');
  assert.equal(offlineRun.deliveries[0].status, 'delivered');

  // 6. Permanent 400 becomes rejected and is not retried
  const badRun = {
    runId: 'run_bad_03',
    originalDestination: baseUrl,
    deliveries: [{ destination: baseUrl, status: 'pending', lastError: '' }]
  };
  applyResponse(badRun.deliveries[0], 400, { error: 'invalid_payload' }, 'run_bad_03');
  assert.equal(badRun.deliveries[0].status, 'rejected');
  now = 300;
  assert.equal(retryPendingIfReady([badRun]).length, 0);
});
