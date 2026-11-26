const RUN_ID_PATTERN = /^[a-zA-Z0-9_-]+$/;
const ISO_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

const SUMMARY_INTEGER_FIELDS = [
  'score',
  'gems',
  'collisions',
  'voluntaryBoosts',
  'bonusGems',
  'manualBoosts',
  'recoveryBoosts',
  'recoveryAttempts',
  'recoverySucceeded',
  'recoveryFailed',
  'recoveryDeclined',
  'recoveryInterrupted',
  'recoveryOffers',
  'recoveryUnavailable',
  'recoveryOffersClosed',
  'recoveryZoneCollisions',
  'recoveryZonePointsLost',
  'recoveryZoneGemsLost',
  'recoveryNetPoints',
  'recoveryNetGems',
  'purchasedGemMultiplier',
  'purchasedPointMultiplier',
  'pointsRecovered',
  'pointsLost',
  'brakePresses',
  'brakeReleases',
  'decisionsShown',
  'decisionsKept',
  'gemsLost',
  'gemsRecovered',
  'gemBalance',
  'boostCollisions',
  'gemsSpent',
  'speedUpgrades',
  'pinkGemsCollected',
  'pinkGemsMissed',
  'pinkGemNearMisses',
  'bestPinkGemStreak',
  'pinkDodgeCleared',
  'pinkDodgeCollisions'
];

const SUMMARY_NUMBER_FIELDS = [
  'timeSeconds',
  'realTimeSeconds',
  'distanceMetres',
  'peakSpeedKmh',
  'brakeHeldSeconds',
  'purchasedSpeedKmh'
];

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isFiniteNumber(value) {
  return typeof value === 'number' && Number.isFinite(value);
}

function isValidIsoDate(value) {
  if (typeof value !== 'string' || !ISO_DATE_PATTERN.test(value)) {
    return false;
  }
  const [year, month, day] = value.split('-').map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  return (
    parsed.getUTCFullYear() === year &&
    parsed.getUTCMonth() === month - 1 &&
    parsed.getUTCDate() === day
  );
}

function validateTurboObject(turbo, prefix, details) {
  if (turbo === undefined || turbo === null) {
    return;
  }
  if (!isPlainObject(turbo)) {
    details.push(`${prefix} must be an object`);
    return;
  }
  if (turbo.lifetimeMode !== undefined && turbo.lifetimeMode !== null && typeof turbo.lifetimeMode !== 'string') {
    details.push(`${prefix}.lifetimeMode must be a string`);
  }
  for (const field of ['durationSeconds', 'speedBonusMetresPerSecond']) {
    if (turbo[field] !== undefined && turbo[field] !== null && !isFiniteNumber(turbo[field])) {
      details.push(`${prefix}.${field} must be a finite number`);
    }
  }
  for (const field of ['gemMultiplier', 'pointMultiplier']) {
    if (turbo[field] !== undefined && turbo[field] !== null && !Number.isInteger(turbo[field])) {
      details.push(`${prefix}.${field} must be an integer`);
    }
  }
}

function isBlankRecoverySnapshot(rec) {
  const stringFields = ['offerId', 'attemptId', 'outcome', 'cause'];
  for (const key of stringFields) {
    if (typeof rec[key] === 'string' && rec[key].trim() !== '') {
      return false;
    }
  }
  if (rec.accepted === true) {
    return false;
  }
  if (Array.isArray(rec.eligibleLossSequences) && rec.eligibleLossSequences.length > 0) {
    return false;
  }
  return true;
}

function validateRecoveryFields(rec, prefix, details, requireOfferId) {
  for (const field of ['offerId', 'attemptId', 'outcome', 'cause']) {
    if (rec[field] !== undefined && rec[field] !== null && typeof rec[field] !== 'string') {
      details.push(`${prefix}.${field} must be a string`);
    }
  }

  if (requireOfferId && (typeof rec.offerId !== 'string' || rec.offerId.trim() === '')) {
    details.push(`${prefix}.offerId must be a non-empty string`);
  }

  if (rec.accepted !== undefined && rec.accepted !== null && typeof rec.accepted !== 'boolean') {
    details.push(`${prefix}.accepted must be a boolean`);
  }

  if (rec.eligibleLossSequences !== undefined && rec.eligibleLossSequences !== null) {
    if (
      !Array.isArray(rec.eligibleLossSequences) ||
      !rec.eligibleLossSequences.every((item) => Number.isInteger(item))
    ) {
      details.push(`${prefix}.eligibleLossSequences must be an array of integers`);
    }
  }

  const intFields = [
    'section',
    'decisionNumber',
    'eligiblePoints',
    'offeredPoints',
    'returnedPoints',
    'eligibleGems',
    'offeredGems',
    'returnedGems',
    'minimumPointLoss'
  ];
  for (const field of intFields) {
    if (rec[field] !== undefined && rec[field] !== null && !Number.isInteger(rec[field])) {
      details.push(`${prefix}.${field} must be an integer`);
    }
  }

  const floatFields = [
    'offerDistanceMetres',
    'startDistanceMetres',
    'endDistanceMetres',
    'challengeMetres',
    'startTimeSeconds',
    'endTimeSeconds',
    'fogLocalZ',
    'pointLossFraction',
    'gemLossFraction'
  ];
  for (const field of floatFields) {
    if (rec[field] !== undefined && rec[field] !== null && !isFiniteNumber(rec[field])) {
      details.push(`${prefix}.${field} must be a finite number`);
    }
  }

  validateTurboObject(rec.turbo, `${prefix}.turbo`, details);
}

function validateJourney(journey, details) {
  if (journey === undefined || journey === null) {
    return;
  }
  if (!isPlainObject(journey)) {
    details.push('journey must be an object');
    return;
  }

  for (const field of ['journeyId', 'schoolName', 'schoolShift', 'schoolDate']) {
    if (journey[field] !== undefined && journey[field] !== null && typeof journey[field] !== 'string') {
      details.push(`journey.${field} must be a string`);
      return;
    }
  }

  const journeyId = (journey.journeyId ?? '').trim();
  const schoolName = (journey.schoolName ?? '').trim();
  const schoolShift = (journey.schoolShift ?? '').trim();
  const schoolDate = (journey.schoolDate ?? '').trim();

  // Unity JsonUtility serializes null [Serializable] references as objects with "" fields.
  const isAllEmpty = journeyId === '' && schoolName === '' && schoolShift === '' && schoolDate === '';
  if (isAllEmpty) {
    return;
  }

  if (journeyId === '' || journeyId.length > 128) {
    details.push('journey.journeyId must be a non-empty string up to 128 chars when journey is present');
  }
  if (schoolName === '' || schoolName.length > 200) {
    details.push('journey.schoolName must be a non-empty string up to 200 chars when journey is present');
  }
  if (schoolShift.length > 64) {
    details.push('journey.schoolShift must be at most 64 chars');
  }
  if (schoolDate !== '' && !isValidIsoDate(schoolDate)) {
    details.push('journey.schoolDate must be a valid YYYY-MM-DD date');
  }
}

function validateSessionReport(body) {
  const details = [];

  if (!isPlainObject(body)) {
    return { valid: false, details: ['Body must be a JSON object'] };
  }

  if (!isPlainObject(body.metadata)) {
    details.push('metadata must be an object');
  } else {
    if (typeof body.metadata.runId !== 'string' || body.metadata.runId.trim() === '') {
      details.push('metadata.runId must be a non-empty string');
    } else if (!RUN_ID_PATTERN.test(body.metadata.runId)) {
      details.push('metadata.runId must match ^[a-zA-Z0-9_-]+$');
    }

    if (typeof body.metadata.reportVersion !== 'string' || body.metadata.reportVersion.trim() === '') {
      details.push('metadata.reportVersion must be a non-empty string');
    }

    if (
      body.metadata.schemaVersion !== undefined &&
      body.metadata.schemaVersion !== null &&
      !Number.isInteger(body.metadata.schemaVersion)
    ) {
      details.push('metadata.schemaVersion must be an integer');
    }

    if (body.metadata.seed !== undefined && body.metadata.seed !== null && !Number.isInteger(body.metadata.seed)) {
      details.push('metadata.seed must be an integer');
    }

    if (
      body.metadata.routeDistanceMetres !== undefined &&
      body.metadata.routeDistanceMetres !== null &&
      !isFiniteNumber(body.metadata.routeDistanceMetres)
    ) {
      details.push('metadata.routeDistanceMetres must be a finite number');
    }

    for (const tsField of ['startedAtUtc', 'finishedAtUtc']) {
      const val = body.metadata[tsField];
      if (val !== undefined && val !== null) {
        if (typeof val !== 'string' || (val.trim() !== '' && Number.isNaN(Date.parse(val)))) {
          details.push(`metadata.${tsField} must be a valid ISO-8601 timestamp string`);
        }
      }
    }

    if (body.metadata.braking !== undefined && body.metadata.braking !== null) {
      if (!isPlainObject(body.metadata.braking)) {
        details.push('metadata.braking must be an object');
      } else {
        const b = body.metadata.braking;
        if (b.mode !== undefined && b.mode !== null && typeof b.mode !== 'string') {
          details.push('metadata.braking.mode must be a string');
        }
        for (const f of ['decelerationMetresPerSecondSquared', 'releaseAccelerationMetresPerSecondSquared']) {
          if (b[f] !== undefined && b[f] !== null && !isFiniteNumber(b[f])) {
            details.push(`metadata.braking.${f} must be a finite number`);
          }
        }
      }
    }
  }

  if (!isPlainObject(body.summary)) {
    details.push('summary must be an object');
  } else {
    if (
      body.summary.ending !== undefined &&
      body.summary.ending !== null &&
      typeof body.summary.ending !== 'string'
    ) {
      details.push('summary.ending must be a string');
    }
    for (const field of SUMMARY_INTEGER_FIELDS) {
      if (
        body.summary[field] !== undefined &&
        body.summary[field] !== null &&
        !Number.isInteger(body.summary[field])
      ) {
        details.push(`summary.${field} must be an integer`);
      }
    }
    for (const field of SUMMARY_NUMBER_FIELDS) {
      if (
        body.summary[field] !== undefined &&
        body.summary[field] !== null &&
        !isFiniteNumber(body.summary[field])
      ) {
        details.push(`summary.${field} must be a finite number`);
      }
    }
  }

  if (!Array.isArray(body.events)) {
    details.push('events must be an array');
  } else {
    for (let i = 0; i < body.events.length; i += 1) {
      const ev = body.events[i];
      const prefix = `events[${i}]`;
      if (!isPlainObject(ev)) {
        details.push(`${prefix} must be an object`);
        continue;
      }
      if (!Number.isInteger(ev.sequence)) {
        details.push(`${prefix}.sequence must be an integer`);
      }
      if (typeof ev.type !== 'string' || ev.type.trim() === '') {
        details.push(`${prefix}.type must be a non-empty string`);
      }
      if (ev.lossOrigin !== undefined && ev.lossOrigin !== null && typeof ev.lossOrigin !== 'string') {
        details.push(`${prefix}.lossOrigin must be a string`);
      }
      for (const intField of [
        'lane',
        'score',
        'scoreDelta',
        'gemsCollected',
        'gemBalance',
        'gemDelta',
        'decisionNumber',
        'pinkGemStreak',
        'scoreMultiplier',
        'pinkGemStreakBefore',
        'pinkDodgeEncounter',
        'transitionSection',
        'gemRewardMultiplier',
        'pointRewardMultiplier'
      ]) {
        if (ev[intField] !== undefined && ev[intField] !== null && !Number.isInteger(ev[intField])) {
          details.push(`${prefix}.${intField} must be an integer`);
        }
      }
      for (const numField of ['timeSeconds', 'distanceMetres', 'speedKmh', 'purchasedSpeedKmh']) {
        if (ev[numField] !== undefined && ev[numField] !== null && !isFiniteNumber(ev[numField])) {
          details.push(`${prefix}.${numField} must be a finite number`);
        }
      }
      for (const boolField of ['duringBoost', 'pinkGemNearMiss']) {
        if (ev[boolField] !== undefined && ev[boolField] !== null && typeof ev[boolField] !== 'boolean') {
          details.push(`${prefix}.${boolField} must be a boolean`);
        }
      }

      validateTurboObject(ev.turboConfiguration, `${prefix}.turboConfiguration`, details);

      if (ev.recovery !== undefined && ev.recovery !== null) {
        if (!isPlainObject(ev.recovery)) {
          details.push(`${prefix}.recovery must be an object`);
        } else {
          const requireOfferId = !isBlankRecoverySnapshot(ev.recovery);
          validateRecoveryFields(ev.recovery, `${prefix}.recovery`, details, requireOfferId);
        }
      }
    }
  }

  if (body.recoveryOpportunities !== undefined && body.recoveryOpportunities !== null) {
    if (!Array.isArray(body.recoveryOpportunities)) {
      details.push('recoveryOpportunities must be an array');
    } else {
      for (let i = 0; i < body.recoveryOpportunities.length; i += 1) {
        const opp = body.recoveryOpportunities[i];
        const prefix = `recoveryOpportunities[${i}]`;
        if (!isPlainObject(opp)) {
          details.push(`${prefix} must be an object`);
          continue;
        }
        validateRecoveryFields(opp, prefix, details, true);
      }
    }
  }

  validateJourney(body.journey, details);

  return details.length === 0
    ? { valid: true, details: [] }
    : { valid: false, details };
}

module.exports = {
  validateSessionReport
};
