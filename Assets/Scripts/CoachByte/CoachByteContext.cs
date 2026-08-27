using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything Coach Byte is allowed to know about the player, gathered from the
/// systems that already own each fact. This class NEVER computes or invents a
/// stat - it only reads.
///
/// Every field is nullable/empty-able on purpose. An absent value means "we do
/// not know this", and CoachBytePromptBuilder omits it entirely rather than
/// sending a zero. That is what keeps the coach from congratulating someone on a
/// 0-hit combo they never threw.
///
/// Sources:
///   PlayerStats            - last mode, streak, per-mode last/best score & combo, mood
///   PlayerPrefs            - "Player_HighScore" (ScoreManager), "BriefCope_LastResult"
///   PlayerProgression      - level (now persisted, see PlayerPrefsDataStore)
///   CoachByteHistory       - recent messages, for anti-repetition only
/// </summary>
public class CoachByteContext
{
    private const string BriefCopePrefsKey = "BriefCope_LastResult";

    // --- This session's result (set by the result screens; null on the menu) ---
    public string currentMode;
    public int? sessionScore;
    public int? sessionCombo;
    public bool isNewPersonalBest;
    public bool leveledUp;
    public int? newLevel;

    // --- Standing history ---
    public string lastMode;
    public int? daysSinceLastPlay;
    public int? currentStreak;
    public int? level;

    public int? boxingBestScore;
    public int? boxingBestCombo;
    public int? rageBestScore;
    public int? rageBestStreak;

    // --- Wellbeing context (softened, never raw) ---
    public string recommendedMode;    // "Boxing" / "RageRoom" / "Meditate"
    public string copingDescriptor;   // plain-language, NEVER the raw bucket name
    public string recentMood;         // angry / anxious / tired / calm

    // --- Anti-repetition ---
    public List<string> recentMessages = new List<string>();

    /// <summary>
    /// Builds the standing context available anywhere (menu, result screens).
    /// Result-specific fields stay null - the caller fills those in.
    /// </summary>
    public static CoachByteContext Gather(string historyContext = null)
    {
        var ctx = new CoachByteContext();

        if (PlayerStats.HasAnyHistory)
        {
            var d = PlayerStats.Data;
            ctx.lastMode = string.IsNullOrEmpty(d.lastMode) ? null : d.lastMode;
            ctx.daysSinceLastPlay = PlayerStats.DaysSinceLastPlay;

            // Only a LIVE streak is reported. A stale 5-day streak from last month
            // is a fact about the past, not something to cheer on today.
            if (PlayerStats.HasLiveStreak) ctx.currentStreak = d.currentStreak;

            if (d.boxingBestCombo > 0) ctx.boxingBestCombo = d.boxingBestCombo;
            if (d.rageBestScore > 0) ctx.rageBestScore = d.rageBestScore;
            if (d.rageBestStreak > 0) ctx.rageBestStreak = d.rageBestStreak;
        }

        int boxingBest = PlayerStats.BoxingBestScore;
        if (boxingBest > 0) ctx.boxingBestScore = boxingBest;

        ctx.recentMood = PlayerStats.RecentMood;

        if (PlayerProgression.Instance != null && PlayerProgression.Instance.Level > 1)
            ctx.level = PlayerProgression.Instance.Level;

        ReadBriefCope(ctx);

        if (!string.IsNullOrEmpty(historyContext))
            ctx.recentMessages = CoachByteHistory.GetRecentMessages(3, historyContext);

        return ctx;
    }

    // The saved Brief-COPE record, translated on the way in. The raw
    // dominantCopingStyle bucket ("Avoidant" etc.) is a clinical-sounding
    // classification and never leaves this method - only the soft descriptor does.
    private static void ReadBriefCope(CoachByteContext ctx)
    {
        string json = PlayerPrefs.GetString(BriefCopePrefsKey, "");
        if (string.IsNullOrEmpty(json)) return;

        BriefCopeResult result;
        try { result = JsonUtility.FromJson<BriefCopeResult>(json); }
        catch { return; }

        if (result == null || result.skipped) return;

        if (!string.IsNullOrEmpty(result.mode)) ctx.recommendedMode = result.mode;
        ctx.copingDescriptor = DescribeCoping(result.dominantCopingStyle);
    }

    /// <summary>
    /// Turns a Brief-COPE bucket into something a friendly coach could say out
    /// loud. Deliberately vague and non-diagnostic: it steers TONE, it is not a
    /// finding about the player, and the player must never see the bucket itself.
    /// </summary>
    private static string DescribeCoping(string bucket)
    {
        switch (bucket)
        {
            case "Approach": return "tends to take things head-on";
            case "Avoidant": return "may appreciate a gentler session today";
            case "Context":  return "mixes up how they handle a rough day";
            default:         return null;
        }
    }

    /// <summary>Best combo the player has ever hit in the mode they last played, if any.</summary>
    public int? BestComboForLastMode
    {
        get
        {
            if (lastMode == "Boxing") return boxingBestCombo;
            if (lastMode == "RageRoom") return rageBestStreak;
            return null;
        }
    }
}
