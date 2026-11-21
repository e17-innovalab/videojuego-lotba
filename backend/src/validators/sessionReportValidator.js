const RUN_ID_PATTERN = /^[a-zA-Z0-9_-]+$/;

function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
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
  }

  if (!isPlainObject(body.summary)) {
    details.push('summary must be an object');
  }

  if (!Array.isArray(body.events)) {
    details.push('events must be an array');
  }

  return details.length === 0
    ? { valid: true, details: [] }
    : { valid: false, details };
}

module.exports = {
  validateSessionReport
};
