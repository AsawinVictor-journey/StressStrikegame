using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Tracks Brief-COPE's question ROTATION across days. Local-only, same guardrail
// as BriefCopeResult/CoachByteHistory (no cloud sync - see BRIEF_COPE_CONTEXT.md).
//
// The survey used to ask all 14 items in one sitting, once ever. It now asks a
// short round of QuestionsPerDay items, at most once per calendar day, cycling
// through the full 14-item pool before anything repeats:
//
//   Day 1: 5 never-asked items (randomly chosen from the unused pool)
//   Day 2: 5 more never-asked items
//   Day 3: the last 4 never-asked items + 1 repeat, chosen by "high impact"
//          (whichever already-asked item scored highest - i.e. the subscale the
//          player leans on most, worth re-checking sooner than a subscale that
//          barely registered)
//   Day 4+: every item is now a repeat, so the whole round is picked by
//           highest-score-first; ties broken by whichever was asked longest ago,
//           so the top few subscales rotate between each other instead of the
//           exact same 5 questions repeating forever.
//
// This class is who decides "which 5 questions today" and "what does the game
// actually know about this player's coping style overall" - the latter merges
// every subscale's most recently recorded score (not just today's slice), so
// GameModeRecommendation always sees the fullest picture available instead of
// being skewed toward whichever handful of subscales happened to be asked today.
public static class BriefCopeProgress
{
    private const string PrefsKey = "BriefCope_Progress";

    [Serializable]
    private class QuestionState
    {
        public int id;
        public bool used;
        public int lastScore;        // most recent answer value (1-4). Meaningless while used == false.
        public string lastAskedDate; // yyyy-MM-dd. "" if never asked.
    }

    [Serializable]
    private class ProgressData
    {
        // yyyy-MM-dd of the last completed OR skipped round. Empty = never.
        public string lastSurveyDate = "";
        public List<QuestionState> questions = new List<QuestionState>();
    }

    private static ProgressData _cache;

    private static string Today() => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private static ProgressData Load()
    {
        if (_cache != null) return _cache;

        string json = PlayerPrefs.GetString(PrefsKey, "");
        ProgressData data = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { data = JsonUtility.FromJson<ProgressData>(json); }
            catch { data = null; }
        }
        data ??= new ProgressData();

        // Seed any question id this ProgressData doesn't know about yet - covers
        // both a brand new save and BriefCopeData.Questions gaining an item later.
        var known = new HashSet<int>(data.questions.Select(q => q.id));
        foreach (var q in BriefCopeData.Questions)
        {
            if (known.Contains(q.id)) continue;
            data.questions.Add(new QuestionState { id = q.id, used = false, lastScore = 0, lastAskedDate = "" });
        }

        _cache = data;
        return _cache;
    }

    private static void Save(ProgressData data)
    {
        _cache = data;
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>True once today's round has been answered OR skipped - either way, don't ask again until tomorrow.</summary>
    public static bool HasCompletedToday() => Load().lastSurveyDate == Today();

    /// <summary>
    /// Picks this round's questions: unused items first (random order), then -
    /// only once the unused pool runs out - already-used items ranked by highest
    /// last score, oldest-asked-first among ties.
    /// </summary>
    public static List<CopeQuestion> SelectTodaysQuestions(int count)
    {
        var data = Load();
        var stateById = data.questions.ToDictionary(q => q.id);

        var unused = new List<CopeQuestion>();
        var used = new List<CopeQuestion>();
        foreach (var q in BriefCopeData.Questions)
            (stateById[q.id].used ? used : unused).Add(q);

        Shuffle(unused);

        var picked = new List<CopeQuestion>(count);
        picked.AddRange(unused.Take(count));

        if (picked.Count < count)
        {
            int remaining = count - picked.Count;
            var ranked = used
                .OrderByDescending(q => stateById[q.id].lastScore)
                .ThenByDescending(q => DaysSince(stateById[q.id].lastAskedDate))
                .Take(remaining);
            picked.AddRange(ranked);
        }

        Shuffle(picked); // don't let "fresh items first" leak into a visible ordering
        return picked;
    }

    /// <summary>Call once the player answers a full round. Marks those items used and advances the daily gate.</summary>
    public static void RecordAnswers(Dictionary<int, int> todaysAnswers)
    {
        var data = Load();
        string today = Today();
        var stateById = data.questions.ToDictionary(q => q.id);

        foreach (var kv in todaysAnswers)
        {
            if (!stateById.TryGetValue(kv.Key, out var state)) continue;
            state.used = true;
            state.lastScore = kv.Value;
            state.lastAskedDate = today;
        }

        data.lastSurveyDate = today;
        Save(data);
    }

    /// <summary>Call when the player skips instead of answering. Advances the daily gate without touching any question's history.</summary>
    public static void MarkSkippedToday()
    {
        var data = Load();
        data.lastSurveyDate = Today();
        Save(data);
    }

    /// <summary>
    /// Every subscale's most recently recorded score, merged across every day
    /// asked so far. Feed this to GameModeRecommendation instead of a single
    /// day's slice, so the bucket comparison isn't skewed by whichever handful
    /// of subscales happened to be asked today.
    /// </summary>
    public static Dictionary<int, int> AllKnownAnswers()
    {
        var data = Load();
        var result = new Dictionary<int, int>();
        foreach (var q in data.questions)
            if (q.used) result[q.id] = q.lastScore;
        return result;
    }

    private static int DaysSince(string yyyyMMdd)
    {
        if (string.IsNullOrEmpty(yyyyMMdd)) return int.MaxValue;
        if (DateTime.TryParse(yyyyMMdd, out var d))
            return (DateTime.UtcNow.Date - d.Date).Days;
        return int.MaxValue;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
