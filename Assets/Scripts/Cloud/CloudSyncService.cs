using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace StressStrike.Cloud
{
    // Opt-in mirror of local gameplay data to MongoDB, via a REST backend.
    //
    // Guardrail (BRIEF_COPE_CONTEXT.md): "Results stored locally by default - no
    // forced cloud sync." So this class is a MIRROR, never the source of truth.
    // Local files/PlayerPrefs stay authoritative and the game is fully playable
    // with sync off, the backend down, or the machine offline.
    //
    // The client never holds a MongoDB connection string. A credential shipped in a
    // build can be pulled straight out of the binary, which would expose every
    // player's survey and heart-rate data. The backend holds it; Unity only speaks
    // HTTP to endpoints that accept its own records.
    public class CloudSyncService : MonoBehaviour
    {
        public static CloudSyncService Instance { get; private set; }

        [Header("Backend")]
        [Tooltip("Base URL of the REST API, e.g. http://localhost:3000")]
        [SerializeField] private string apiBaseUrl = "http://localhost:3000";
        [SerializeField] private int timeoutSeconds = 15;

        [Header("Sync")]
        [Tooltip("Retry the queued records this often (seconds) while consent is on.")]
        [SerializeField] private float flushIntervalSeconds = 60f;

        private const string ConsentKey = "Cloud_SyncConsent";  // 0/1, default 0 = off
        private const string PlayerIdKey = "Cloud_PlayerId";

        private string QueuePath => Path.Combine(Application.persistentDataPath, "cloud_sync_queue.jsonl");
        private bool flushing;

        // Opt-in: absent means off. Nothing leaves the device until the player says yes.
        public static bool ConsentGiven => PlayerPrefs.GetInt(ConsentKey, 0) == 1;

        public static void SetConsent(bool granted)
        {
            PlayerPrefs.SetInt(ConsentKey, granted ? 1 : 0);
            PlayerPrefs.Save();

            // Turning consent off should not silently ship whatever was already
            // queued, so drop the backlog rather than leaving it primed to send.
            if (!granted) Instance?.ClearQueue();
        }

        // Anonymous, per-device. Not an account - "user profiles" are paused for now,
        // so this is just a stable key to group one device's sessions together.
        public static string PlayerId
        {
            get
            {
                string id = PlayerPrefs.GetString(PlayerIdKey, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(PlayerIdKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(FlushLoop());
        }

        // --- Public API -----------------------------------------------------

        public void RecordSession(SessionRecord record)
        {
            if (record == null) return;
            record.playerId = PlayerId;
            if (string.IsNullOrEmpty(record.sessionId)) record.sessionId = Guid.NewGuid().ToString("N");
            if (record.timestamp == 0) record.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Enqueue("session", JsonUtility.ToJson(record));
        }

        public void RecordSurvey(SurveyRecord record)
        {
            if (record == null) return;
            record.playerId = PlayerId;
            if (string.IsNullOrEmpty(record.surveyId)) record.surveyId = Guid.NewGuid().ToString("N");
            if (record.timestamp == 0) record.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Enqueue("survey", JsonUtility.ToJson(record));
        }

        // --- Queue ----------------------------------------------------------

        // Everything goes through an on-disk queue rather than firing immediately, so
        // a session finished on a train still lands once the player is back online.
        private void Enqueue(string kind, string payloadJson)
        {
            if (!ConsentGiven) return;

            var envelope = new SyncEnvelope { kind = kind, payload = payloadJson };
            try
            {
                File.AppendAllText(QueuePath, JsonUtility.ToJson(envelope) + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CloudSync] Could not queue record: " + e.Message);
            }
        }

        private void ClearQueue()
        {
            try { if (File.Exists(QueuePath)) File.Delete(QueuePath); }
            catch (Exception e) { Debug.LogWarning("[CloudSync] Could not clear queue: " + e.Message); }
        }

        private IEnumerator FlushLoop()
        {
            var wait = new WaitForSeconds(flushIntervalSeconds);
            while (true)
            {
                yield return wait;
                if (ConsentGiven) yield return StartCoroutine(Flush());
            }
        }

        public IEnumerator Flush()
        {
            if (flushing || !ConsentGiven) yield break;
            if (!File.Exists(QueuePath)) yield break;

            flushing = true;

            string[] lines;
            try { lines = File.ReadAllLines(QueuePath); }
            catch (Exception e)
            {
                Debug.LogWarning("[CloudSync] Could not read queue: " + e.Message);
                flushing = false;
                yield break;
            }

            // Records that fail stay queued for the next pass. Sends are idempotent
            // on the server (sessionId/surveyId are unique), so a retry after an
            // ambiguous timeout can't create duplicates.
            var unsent = new List<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                SyncEnvelope envelope = null;
                try { envelope = JsonUtility.FromJson<SyncEnvelope>(line); }
                catch { continue; } // malformed line - drop it, retrying won't help

                if (envelope == null || string.IsNullOrEmpty(envelope.kind)) continue;

                bool ok = false;
                yield return StartCoroutine(Post(envelope, success => ok = success));

                if (!ok) unsent.Add(line);
            }

            try
            {
                if (unsent.Count == 0) File.Delete(QueuePath);
                else File.WriteAllLines(QueuePath, unsent);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CloudSync] Could not rewrite queue: " + e.Message);
            }

            flushing = false;
        }

        private IEnumerator Post(SyncEnvelope envelope, Action<bool> done)
        {
            string route = envelope.kind == "survey" ? "/api/surveys" : "/api/sessions";
            byte[] body = Encoding.UTF8.GetBytes(envelope.payload);

            using (var req = new UnityWebRequest(apiBaseUrl.TrimEnd('/') + route, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = timeoutSeconds;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    done(true);
                    yield break;
                }

                // 4xx means the server rejected this record's shape - retrying forever
                // would wedge the queue behind a record that can never succeed, so drop
                // it. 5xx and network errors are transient, so keep those queued.
                bool permanent = req.responseCode >= 400 && req.responseCode < 500;
                if (permanent)
                    Debug.LogWarning($"[CloudSync] Dropping rejected record ({req.responseCode}): {req.error}");

                done(permanent);
            }
        }
    }
}
