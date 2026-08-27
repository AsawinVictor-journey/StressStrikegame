using System;
using System.Collections.Generic;
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

    /// <summary>
    /// The last <paramref name="count"/> messages for one context, oldest first.
    ///
    /// Fed to CoachBytePromptBuilder purely as an anti-repetition hint - the model
    /// is shown what it recently said and told to phrase the next one differently.
    /// Only the message TEXT leaves this file; timestamps and modes stay local, and
    /// the file itself is never uploaded anywhere.
    /// </summary>
    public static List<string> GetRecentMessages(int count, string context = null)
    {
        var messages = new List<string>();
        if (count <= 0 || !File.Exists(FilePath)) return messages;

        string[] lines;
        try { lines = File.ReadAllLines(FilePath); }
        catch { return messages; } // unreadable history must never break a greeting

        // Walk backwards so we only parse as far as we need on a long file.
        for (int i = lines.Length - 1; i >= 0 && messages.Count < count; i--)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            Entry e;
            try { e = JsonUtility.FromJson<Entry>(lines[i]); }
            catch { continue; }

            if (e == null || string.IsNullOrEmpty(e.message)) continue;
            if (context != null && e.context != context) continue;

            messages.Add(e.message);
        }

        messages.Reverse(); // oldest first reads more naturally in the prompt
        return messages;
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
