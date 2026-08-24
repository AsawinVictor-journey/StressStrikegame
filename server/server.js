// StressStrike cloud sync API.
//
// Sits between the Unity client and MongoDB so the connection string never ships
// in a game build. Unity POSTs records it already stored locally; this mirrors
// them into Atlas for long-term analytics and the statistics board.

const express = require('express');
const { MongoClient } = require('mongodb');

const PORT = process.env.PORT || 3000;
const MONGODB_URI = process.env.MONGODB_URI;
const DB_NAME = process.env.DB_NAME || 'stressstrike';
const GEMINI_API_KEY = process.env.GEMINI_API_KEY;

if (!MONGODB_URI) {
  console.error('MONGODB_URI is not set. Copy .env.example to .env and fill it in.');
  process.exit(1);
}

if (!GEMINI_API_KEY) {
  // Non-fatal: cloud sync and stats still work without AI recommendations.
  console.warn('GEMINI_API_KEY is not set - /api/gemini/generate will return 500.');
}

const app = express();
app.use(express.json({ limit: '64kb' }));

let db;

const VALID_MODES = new Set(['Boxing', 'RageRoom', 'Meditate']);

// Reject anything malformed with 400. The client treats 4xx as permanent and drops
// the record, so validation must only fail on things a retry could never fix.
function validateSession(b) {
  if (!b || typeof b !== 'object') return 'body must be an object';
  if (!b.playerId || typeof b.playerId !== 'string') return 'playerId required';
  if (!b.sessionId || typeof b.sessionId !== 'string') return 'sessionId required';
  if (!VALID_MODES.has(b.mode)) return `mode must be one of ${[...VALID_MODES].join(', ')}`;
  if (typeof b.timestamp !== 'number') return 'timestamp must be a number';
  return null;
}

function validateSurvey(b) {
  if (!b || typeof b !== 'object') return 'body must be an object';
  if (!b.playerId || typeof b.playerId !== 'string') return 'playerId required';
  if (!b.surveyId || typeof b.surveyId !== 'string') return 'surveyId required';
  if (typeof b.timestamp !== 'number') return 'timestamp must be a number';
  // A skipped survey legitimately has no recommendation, so only enforce the
  // mode vocabulary when one is actually present.
  if (b.recommendedMode && !VALID_MODES.has(b.recommendedMode)) return 'invalid recommendedMode';
  return null;
}

// Upsert keyed on the client-generated id, so a retry after an ambiguous timeout
// overwrites rather than inserting a second copy.
function upsertRoute(collectionName, idField, validate) {
  return async (req, res) => {
    const problem = validate(req.body);
    if (problem) return res.status(400).json({ error: problem });

    try {
      const doc = { ...req.body, receivedAt: new Date() };
      await db.collection(collectionName).updateOne(
        { [idField]: doc[idField] },
        { $set: doc },
        { upsert: true }
      );
      res.status(200).json({ ok: true });
    } catch (err) {
      // 5xx so the client keeps it queued and tries again.
      console.error(`${collectionName} upsert failed:`, err.message);
      res.status(500).json({ error: 'storage failure' });
    }
  };
}

app.post('/api/sessions', upsertRoute('sessions', 'sessionId', validateSession));
app.post('/api/surveys', upsertRoute('surveys', 'surveyId', validateSurvey));

app.get('/health', (_req, res) => res.json({ ok: true }));

// Gemini proxy. The Unity client never holds GEMINI_API_KEY (a build's strings
// can always be extracted) - it POSTs {model, prompt} here and this forwards
// the call server-side. The system instruction is enforced here, not in the
// client, so it can't be bypassed by editing the app: this app only ever
// recommends an activity, never diagnoses or gives medical/clinical advice.
const GEMINI_SYSTEM_INSTRUCTION =
  'You are an activity-recommendation assistant inside StressStrike, a stress-relief app with ' +
  "exactly three activities: boxing, rage room, and yoga/meditation. Only ever recommend which " +
  'activity fits what the player reports, or write brief supportive copy for the app UI, in the ' +
  'exact format the prompt asks for. Never diagnose, label, or give medical, clinical, or ' +
  'mental-health advice.';

function validateGeminiRequest(b) {
  if (!b || typeof b !== 'object') return 'body must be an object';
  if (!b.prompt || typeof b.prompt !== 'string') return 'prompt required';
  if (b.prompt.length > 4000) return 'prompt too long';
  return null;
}

app.post('/api/gemini/generate', async (req, res) => {
  if (!GEMINI_API_KEY) return res.status(500).json({ error: 'GEMINI_API_KEY not configured on server' });

  const problem = validateGeminiRequest(req.body);
  if (problem) return res.status(400).json({ error: problem });

  const model = typeof req.body.model === 'string' && req.body.model ? req.body.model : 'gemini-2.5-flash-lite';
  const maxOutputTokens = Math.min(Math.max(Number(req.body.maxOutputTokens) || 64, 1), 256);
  const temperature = Math.min(Math.max(Number(req.body.temperature) || 0.2, 0), 1);

  try {
    const geminiRes = await fetch(
      `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${GEMINI_API_KEY}`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          contents: [{ parts: [{ text: req.body.prompt }] }],
          systemInstruction: { parts: [{ text: GEMINI_SYSTEM_INSTRUCTION }] },
          generationConfig: { temperature, maxOutputTokens },
        }),
      }
    );

    const data = await geminiRes.json();
    if (!geminiRes.ok) {
      console.error('Gemini API error:', data);
      return res.status(502).json({ error: data.error?.message || 'Gemini API error' });
    }

    const text = (data.candidates?.[0]?.content?.parts || []).map((p) => p.text || '').join('');
    res.json({ text });
  } catch (err) {
    console.error('Gemini request failed:', err.message);
    res.status(502).json({ error: 'Gemini request failed' });
  }
});

// Statistics board. Aggregate across all players by default; pass ?playerId=... for
// one device's own history.
app.get('/api/stats', async (req, res) => {
  const match = req.query.playerId ? { playerId: String(req.query.playerId) } : {};

  try {
    const [byMode, surveys, totals] = await Promise.all([
      db.collection('sessions').aggregate([
        { $match: match },
        {
          $group: {
            _id: '$mode',
            sessions: { $sum: 1 },
            avgDuration: { $avg: '$durationSeconds' },
            avgScore: { $avg: '$score' },
            totalCalories: { $sum: '$caloriesBurned' },
            // Only average HR over sessions that actually captured it - the glove
            // is optional, and unrecorded sessions store 0, which would drag the
            // mean toward zero and make every mode look artificially calming.
            avgBaselineHR: {
              $avg: { $cond: [{ $gt: ['$baselineHR', 0] }, '$baselineHR', '$$REMOVE'] }
            },
            avgPostGameHR: {
              $avg: { $cond: [{ $gt: ['$postGameHR', 0] }, '$postGameHR', '$$REMOVE'] }
            },
          },
        },
        { $sort: { sessions: -1 } },
      ]).toArray(),

      db.collection('surveys').aggregate([
        { $match: match },
        { $group: { _id: '$recommendedMode', count: { $sum: 1 } } },
        { $sort: { count: -1 } },
      ]).toArray(),

      db.collection('sessions').aggregate([
        { $match: match },
        {
          $group: {
            _id: null,
            totalSessions: { $sum: 1 },
            uniquePlayers: { $addToSet: '$playerId' },
          },
        },
        {
          $project: {
            _id: 0,
            totalSessions: 1,
            uniquePlayers: { $size: '$uniquePlayers' },
          },
        },
      ]).toArray(),
    ]);

    res.json({
      byMode,
      recommendations: surveys,
      totals: totals[0] || { totalSessions: 0, uniquePlayers: 0 },
    });
  } catch (err) {
    console.error('stats query failed:', err.message);
    res.status(500).json({ error: 'query failure' });
  }
});

async function start() {
  const client = new MongoClient(MONGODB_URI);
  await client.connect();
  db = client.db(DB_NAME);

  // Unique ids make the upserts genuinely idempotent even under concurrent retries;
  // the compound indexes back the stats aggregations and per-player lookups.
  await db.collection('sessions').createIndex({ sessionId: 1 }, { unique: true });
  await db.collection('surveys').createIndex({ surveyId: 1 }, { unique: true });
  await db.collection('sessions').createIndex({ playerId: 1, timestamp: -1 });
  await db.collection('surveys').createIndex({ playerId: 1, timestamp: -1 });

  app.listen(PORT, () => console.log(`StressStrike API listening on :${PORT}`));
}

start().catch((err) => {
  console.error('startup failed:', err);
  process.exit(1);
});
