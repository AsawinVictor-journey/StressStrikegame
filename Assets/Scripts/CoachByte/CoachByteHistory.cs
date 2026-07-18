using System;
using System.IO;
using UnityEngine;

// Local "database" of every AI coaching message Coach Byte has generated - one
// JSON object per line in Application.persistentDataPath/coachbyte_history.jsonl.
// Local-only, same guardrail as BriefCopeResult (no cloud sync).
public static class CoachByteHistory
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "coachbyte_history.jsonl");

    [Serializable]
    public class Entry
    {
        public long timestamp;
        public string context; // e.g. "BriefCopeSurvey", "MainMenuGreeting"
        public string mode;    // recommended GameMode name, if applicable
        public string message; // the generated text
    }

    public static void Append(string context, string mode, string message)
    {
        var entry = new Entry
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            context = context,
            mode = mode ?? "",
            message = message,
        };
        File.AppendAllText(FilePath, JsonUtility.ToJson(entry) + "\n");
    }

    // Most recent entry, optionally filtered to one context (e.g. only survey results).
    public static Entry GetLatest(string context = null)
    {
        if (!File.Exists(FilePath)) return null;

        Entry latest = null;
        foreach (var line in File.ReadAllLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            Entry e;
            try { e = JsonUtility.FromJson<Entry>(line); }
            catch { continue; }

            if (context != null && e.context != context) continue;
            latest = e;
        }
        return latest;
    }
}
