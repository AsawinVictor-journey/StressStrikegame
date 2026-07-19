// End-to-end smoke test for the cloud sync API.
//
// Spawns the real server.js against a throwaway mongod rather than importing the
// app, so startup (index creation included) is covered too. The contract being
// checked here is the one CloudSyncService.cs depends on: 2xx means "delivered,
// drop from queue", 4xx means "never retry", 5xx means "keep queued".

const { test, before, after, describe } = require('node:test');
const assert = require('node:assert');
const { spawn } = require('node:child_process');
const { MongoMemoryServer } = require('mongodb-memory-server');
const { MongoClient } = require('mongodb');
const path = require('node:path');

const PORT = 3999;
const BASE = `http://127.0.0.1:${PORT}`;
const DB_NAME = 'stressstrike_test';

let mongod, server, client, db;

before(async () => {
  mongod = await MongoMemoryServer.create();
  const uri = mongod.getUri();

  client = await MongoClient.connect(uri);
  db = client.db(DB_NAME);

  server = spawn(process.execPath, [path.join(__dirname, '..', 'server.js')], {
    env: { ...process.env, MONGODB_URI: uri, DB_NAME, PORT: String(PORT) },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  server.stderr.on('data', (d) => process.stderr.write(`[server] ${d}`));

  // Poll /health rather than sleeping a fixed amount - index creation makes
  // startup time variable, and a fixed wait is how these tests go flaky.
  const deadline = Date.now() + 30000;
  for (;;) {
    try {
      const r = await fetch(`${BASE}/health`);
      if (r.ok) break;
    } catch {
      // not listening yet
    }
    if (Date.now() > deadline) throw new Error('server did not become healthy');
    await new Promise((r) => setTimeout(r, 200));
  }
});

after(async () => {
  if (server) server.kill();
  if (client) await client.close();
  if (mongod) await mongod.stop();
});

const post = (route, body) =>
  fetch(BASE + route, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });

const session = (over = {}) => ({
  playerId: 'player-1',
  sessionId: 'session-1',
  timestamp: 1750000000,
  mode: 'Boxing',
  durationSeconds: 300,
  score: 42,
  baselineHR: 0,
  baselineHRV: 0,
  postGameHR: 0,
  postGameHRV: 0,
  caloriesBurned: 12,
  ...over,
});

const survey = (over = {}) => ({
  playerId: 'player-1',
  surveyId: 'survey-1',
  timestamp: 1750000000,
  skipped: false,
  recommendedMode: 'Meditate',
  topSubscale: 'SelfDistraction',
  aiMessage: '',
  aiModel: '',
  ...over,
});

describe('sessions', () => {
  test('accepts a valid record', async () => {
    const res = await post('/api/sessions', session());
    assert.equal(res.status, 200);
    assert.equal(await db.collection('sessions').countDocuments({ sessionId: 'session-1' }), 1);
  });

  test('is idempotent - a retry updates instead of duplicating', async () => {
    // This is what protects the queue: CloudSyncService re-sends anything it
    // could not confirm, so an ambiguous timeout must not double-count a session.
    await post('/api/sessions', session({ score: 99 }));
    const docs = await db.collection('sessions').find({ sessionId: 'session-1' }).toArray();
    assert.equal(docs.length, 1);
    assert.equal(docs[0].score, 99);
  });

  test('rejects an unknown mode with 400', async () => {
    const res = await post('/api/sessions', session({ sessionId: 'bad-mode', mode: 'Yoga' }));
    assert.equal(res.status, 400);
  });

  test('rejects a missing playerId with 400', async () => {
    const body = session({ sessionId: 'no-player' });
    delete body.playerId;
    assert.equal((await post('/api/sessions', body)).status, 400);
  });

  test('rejects a non-numeric timestamp with 400', async () => {
    const res = await post('/api/sessions', session({ sessionId: 'bad-ts', timestamp: 'now' }));
    assert.equal(res.status, 400);
  });
});

describe('surveys', () => {
  test('accepts a valid record', async () => {
    assert.equal((await post('/api/surveys', survey())).status, 200);
  });

  test('accepts a skipped survey with no recommendation', async () => {
    // SkipSurvey() posts a record with nothing but skipped=true populated, so
    // the mode vocabulary must not be enforced on an absent recommendedMode.
    const res = await post('/api/surveys', {
      playerId: 'player-1',
      surveyId: 'survey-skipped',
      timestamp: 1750000001,
      skipped: true,
      recommendedMode: '',
    });
    assert.equal(res.status, 200);
  });

  test('the AI-text follow-up updates the same document', async () => {
    // Finish() sends twice under one surveyId: deterministic first, then the
    // Ollama rewrite. The second must overwrite, not add a second survey.
    await post('/api/surveys', survey({ aiMessage: 'Nice work today.', aiModel: 'llama3' }));
    const docs = await db.collection('surveys').find({ surveyId: 'survey-1' }).toArray();
    assert.equal(docs.length, 1);
    assert.equal(docs[0].aiMessage, 'Nice work today.');
  });

  test('rejects an unknown recommendedMode with 400', async () => {
    const res = await post('/api/surveys', survey({ surveyId: 'bad-rec', recommendedMode: 'Sleep' }));
    assert.equal(res.status, 400);
  });
});

describe('stats', () => {
  before(async () => {
    // Two RageRoom sessions from a second player: one with the glove connected,
    // one without (zeros). The unmeasured one must not drag the HR average down.
    await post('/api/sessions', session({
      playerId: 'player-2', sessionId: 'rr-1', mode: 'RageRoom',
      durationSeconds: 100, score: 10, baselineHR: 80, postGameHR: 100,
    }));
    await post('/api/sessions', session({
      playerId: 'player-2', sessionId: 'rr-2', mode: 'RageRoom',
      durationSeconds: 200, score: 20, baselineHR: 0, postGameHR: 0,
    }));
  });

  test('excludes uncaptured heart rate from the averages', async () => {
    const stats = await (await fetch(`${BASE}/api/stats`)).json();
    const rr = stats.byMode.find((m) => m._id === 'RageRoom');

    assert.equal(rr.sessions, 2);
    // 80, not 40 - the zero-HR session is removed from the mean, not averaged in.
    assert.equal(rr.avgBaselineHR, 80);
    assert.equal(rr.avgPostGameHR, 100);
    // Duration has no such carve-out, so both sessions count: (100+200)/2.
    assert.equal(rr.avgDuration, 150);
  });

  test('counts unique players across all sessions', async () => {
    const stats = await (await fetch(`${BASE}/api/stats`)).json();
    assert.equal(stats.totals.uniquePlayers, 2);
    assert.equal(stats.totals.totalSessions, 3);
  });

  test('scopes to one device when playerId is passed', async () => {
    const stats = await (await fetch(`${BASE}/api/stats?playerId=player-2`)).json();
    assert.equal(stats.totals.uniquePlayers, 1);
    assert.equal(stats.totals.totalSessions, 2);
    assert.ok(stats.byMode.every((m) => m._id === 'RageRoom'));
  });

  test('returns zeroed totals for an unknown player rather than erroring', async () => {
    const stats = await (await fetch(`${BASE}/api/stats?playerId=nobody`)).json();
    assert.equal(stats.totals.totalSessions, 0);
    assert.equal(stats.totals.uniquePlayers, 0);
  });
});
