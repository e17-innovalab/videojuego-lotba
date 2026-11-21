const fs = require('node:fs/promises');
const path = require('node:path');

const DEFAULT_SESSIONS_DIR = path.resolve(__dirname, '..', '..', 'data', 'sessions');

function createLocalSessionStore(sessionsDir = DEFAULT_SESSIONS_DIR) {
  async function save(report) {
    const runId = report.metadata.runId;
    await fs.mkdir(sessionsDir, { recursive: true });

    const filePath = path.join(sessionsDir, `session_${runId}.json`);
    const payload = JSON.stringify(report, null, 2);

    try {
      await fs.writeFile(filePath, payload, { encoding: 'utf8', flag: 'wx' });
      return { status: 'created', runId, filePath };
    } catch (err) {
      if (err && err.code === 'EEXIST') {
        return { status: 'already_exists', runId, filePath };
      }
      throw err;
    }
  }

  return {
    sessionsDir,
    save
  };
}

module.exports = {
  DEFAULT_SESSIONS_DIR,
  createLocalSessionStore
};
