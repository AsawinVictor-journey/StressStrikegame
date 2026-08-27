using System;
using UnityEngine;

/// <summary>
/// The per-player gameplay facts Coach Byte talks about, persisted locally.
///
/// WHY THIS EXISTS: before this, almost nothing a coach would want to mention
/// survived an app restart. Boxing's high score was the only persisted stat
/// (PlayerPrefs "Player_HighScore", owned by ScoreManager); Rage Room persisted
/// nothing at all; best combo existed only as a FinalizeMatch out-param; the
/// check-in mood was a private field. A coach that references any of those
/// would have been inventing them.
///
/// Local-only, same guardrail as BriefCopeResult and CoachByteHistory - this is
/// never cloud-synced.
///
/// NOT a second source of truth for anything that already has one:
///   - Boxing best score stays owned by ScoreManager's "Player_HighScore" key;
///     BoxingBestScore below just reads it.
///   - Level/XP stay owned by PlayerProgression (see PlayerPrefsDataStore).
/// Everything else here had no home before.
/// </summary>
[Serializable]
public class PlayerStatsData
{
    public string lastMode = "";        // GameMode.ToString(): Boxing / RageRoom / Meditate
    public long lastPlayedUtc;
    public string lastPlayedDate = "";  // yyyy-MM-dd, LOCAL date - streaks are a calendar concept

    public int currentStreak;
    public int longestStreak;

    public int boxingLastScore;
    public int boxingLastCombo;
    public int boxingBestCombo;

    public int rageLastScore;
    public int rageBestScore;
    public int rageLastStreak;
    public int rageBestStreak;

    public string lastMood = "";        // angry / anxious / tired / calm - the CheckInManager chips
    public long lastMoodUtc;
}

public static class PlayerStats
{
    private const string PrefsKey = "CoachByte_PlayerStats";
    private const string BoxingHighScoreKey = "Player_HighScore"; // owned by ScoreManager, read-only here

    private const string ModeBoxing = "Boxing";
    private const string ModeRageRoom = "RageRoom";

    private static PlayerStatsData _cached;

    public static PlayerStatsData Data
    {
        get
        {
            if (_cached != null) return _cached;

            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { _cached = JsonUtility.FromJson<PlayerStatsData>(json); }
                catch { _cached = null; } // corrupt value - start clean rather than throwing on every read
            }

            return _cached ?? (_cached = new PlayerStatsData());
        }
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    /// <summary>Boxing's best score. Read straight from ScoreManager's key so there is exactly one high score in the game.</summary>
    public static int BoxingBestScore
    {
        get { return PlayerPrefs.GetInt(BoxingHighScoreKey, 0); }
    }

    public static bool HasAnyHistory
    {
        get { return !string.IsNullOrEmpty(Data.lastMode); }
    }

    /// <summary>Whole days since the last recorded session, or null if they have never played.</summary>
    public static int? DaysSinceLastPlay
    {
        get
        {
            if (Data.lastPlayedUtc <= 0) return null;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return (int)Math.Max(0, (now - Data.lastPlayedUtc) / 86400);
        }
    }

    /// <summary>
    /// Records a finished session and advances the day streak.
    ///
    /// Streak lives HERE rather than in PlayerProgression: that class documents a
    /// deliberate removal of its own streak feature because nothing persisted it.
    /// This store is that missing persistence, so the feature is reintroduced in
    /// the layer that can actually back it, not in the one that could not.
    /// </summary>
    public static void RecordSession(string mode, int score, int combo, bool isNewBest)
    {
        var d = Data;

        UpdateStreak(d);

        d.lastMode = mode ?? "";
        d.lastPlayedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (mode == ModeBoxing)
        {
            d.boxingLastScore = score;
            d.boxingLastCombo = combo;
            if (combo > d.boxingBestCombo) d.boxingBestCombo = combo;
        }
        else if (mode == ModeRageRoom)
        {
            d.rageLastScore = score;
            d.rageLastStreak = combo; // Rage Room's "combo" is its hit streak
            if (score > d.rageBestScore) d.rageBestScore = score;
            if (combo > d.rageBestStreak) d.rageBestStreak = combo;
        }

        Save();
    }

    /// <summary>Yoga/meditation has no score or combo - it still counts for the streak and "last mode".</summary>
    public static void RecordSession(string mode)
    {
        RecordSession(mode, 0, 0, false);
    }

    // Same calendar day = no change (playing twice today is still one day).
    // Yesterday = the chain continues. Anything else (including a first-ever
    // session, where lastPlayedDate is empty) starts a new chain at 1.
    private static void UpdateStreak(PlayerStatsData d)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (d.lastPlayedDate == today) return;

        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        d.currentStreak = (d.lastPlayedDate == yesterday) ? d.currentStreak + 1 : 1;

        if (d.currentStreak > d.longestStreak) d.longestStreak = d.currentStreak;
        d.lastPlayedDate = today;
    }

    /// <summary>
    /// True only while the streak is still live - i.e. the last session was today
    /// or yesterday. A stored streak of 5 from three weeks ago is stale and must
    /// not be spoken aloud as if it were current.
    /// </summary>
    public static bool HasLiveStreak
    {
        get
        {
            if (Data.currentStreak < 2 || string.IsNullOrEmpty(Data.lastPlayedDate)) return false;

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
            return Data.lastPlayedDate == today || Data.lastPlayedDate == yesterday;
        }
    }

    public static void RecordMood(string mood)
    {
        if (string.IsNullOrEmpty(mood)) return;

        Data.lastMood = mood;
        Data.lastMoodUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }

    /// <summary>
    /// The check-in mood, but only while it is still THIS session's mood. A mood
    /// from two days ago says nothing about how the player feels now, and a coach
    /// referencing it would be guessing.
    /// </summary>
    public static string RecentMood
    {
        get
        {
            if (string.IsNullOrEmpty(Data.lastMood) || Data.lastMoodUtc <= 0) return null;

            long ageSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Data.lastMoodUtc;
            return ageSeconds <= 21600 ? Data.lastMood : null; // 6 hours
        }
    }

    /// <summary>Test/debug helper - clears every stat this store owns. Does NOT touch Player_HighScore (ScoreManager owns it).</summary>
    public static void ClearAll()
    {
        _cached = new PlayerStatsData();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }
}
