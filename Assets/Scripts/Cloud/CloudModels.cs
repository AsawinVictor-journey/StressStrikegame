using System;

// Wire format for the cloud sync backend. Kept as plain [Serializable] classes so
// Unity's JsonUtility can handle them - no third-party JSON dependency, and no
// MongoDB driver in the client (see CloudSyncService for why).
namespace StressStrike.Cloud
{
    // One completed play session. This is the unit the statistics board aggregates.
    [Serializable]
    public class SessionRecord
    {
        public string playerId;     // anonymous per-device id, not an account
        public string sessionId;    // guid, so retries can't create duplicates
        public long timestamp;      // unix seconds, UTC
        public string mode;         // "Boxing" | "RageRoom" | "Meditate"
        public float durationSeconds;
        public int score;

        // Biometrics, mirroring HeartRateYogaFlowManager's fields. 0 means "not
        // captured" - the glove is optional hardware, so absence is normal.
        public float baselineHR;
        public float baselineHRV;
        public float postGameHR;
        public float postGameHRV;
        public float caloriesBurned;
    }

    // The result of a Brief-COPE check-in plus what the AI said about it.
    // NOTE: individual question answers are deliberately NOT included. The
    // recommendation and the winning subscale are enough to drive personalisation
    // and the stats board; raw item-level responses to a psychological coping
    // instrument are the most sensitive thing here and stay on-device.
    [Serializable]
    public class SurveyRecord
    {
        public string playerId;
        public string surveyId;
        public long timestamp;
        public bool skipped;
        public string recommendedMode;
        public string topSubscale;
        public string aiMessage;    // what Coach Byte generated, for auditing drift
        public string aiModel;      // which local model produced it
    }

    // Envelope so one endpoint can accept either record type from the offline queue.
    [Serializable]
    public class SyncEnvelope
    {
        public string kind;         // "session" | "survey"
        public string payload;      // the record, JSON-encoded
    }
}
