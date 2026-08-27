using System.Collections.Generic;
using System.Text;

/// <summary>
/// Decides WHAT Coach Byte talks about, and writes the prompt that says it.
///
/// Two rules shape everything here:
///
/// 1. AT MOST TWO FACTS. Dumping the whole context into every prompt produces
///    the stat-sheet voice ("Level 4, 5-day streak, best combo 38, welcome
///    back!"). Picking the one or two most interesting things is what makes it
///    sound like the game noticed something.
///
/// 2. FACTS ARE PRE-WRITTEN, NOT RAW. Every fact reaches the model as a short
///    English clause containing its own number. The model is then told it may
///    only use what it was given, so there is nothing left for it to fill in.
/// </summary>
public static class CoachBytePromptBuilder
{
    public const int MaxWords = 14;

    // Context names, kept as constants so the history file and the prompt
    // switch below can never drift apart.
    public const string MainMenuGreeting = "MainMenuGreeting";
    public const string BoxingResult = "BoxingResult";
    public const string RageRoomResult = "RageRoomResult";
    public const string CheckIn = "CheckIn";
    public const string BriefCopeIntro = "BriefCopeIntro";
    public const string BriefCopeHalfway = "BriefCopeHalfway";
    public const string ModeRecommendation = "ModeRecommendation";

    private const string Persona =
        "You are Coach Byte, a cheerful, energetic AI coach inside the stress-relief game " +
        "StressStrike. You sound like a game companion who pays attention, not a therapist.";

    private const string HardRules =
        "Reply with ONE sentence of AT MOST 14 words. Aim for 8 to 12. " +
        "Use ONLY the player facts supplied above - never invent or estimate a score, combo, " +
        "streak, level, mode or feeling that is not listed. If no facts are listed, give a " +
        "short general welcome. Do not diagnose the player, do not mention stress unless a " +
        "fact mentions it, and do not give medical advice. No emojis, no quotation marks, " +
        "no hashtags, no bullet points. Do not mention being an AI or a language model.";

    /// <summary>Builds the full prompt for one Coach Byte moment.</summary>
    public static string Build(string contextName, CoachByteContext ctx)
    {
        var sb = new StringBuilder();
        sb.Append(Persona).Append(" ").Append(Situation(contextName)).Append(" ");

        var facts = SelectFacts(contextName, ctx);
        if (facts.Count > 0)
        {
            sb.Append("Player facts you may use: ");
            for (int i = 0; i < facts.Count; i++)
            {
                sb.Append(facts[i]);
                if (!facts[i].EndsWith(".")) sb.Append(".");
                sb.Append(" ");
            }
        }
        else
        {
            sb.Append("No notable player facts are available this time. ");
        }

        if (ctx != null && ctx.recentMessages != null && ctx.recentMessages.Count > 0)
        {
            sb.Append("You recently said: ");
            foreach (var m in ctx.recentMessages) sb.Append("[").Append(m).Append("] ");
            sb.Append("Say something clearly different this time - new wording and a new angle. ");
        }

        sb.Append(HardRules);
        return sb.ToString();
    }

    private static string Situation(string contextName)
    {
        switch (contextName)
        {
            case BoxingResult:       return "The player just finished a Boxing match and is looking at their results.";
            case RageRoomResult:     return "The player just finished a Rage Room session and is looking at their results.";
            case CheckIn:            return "The player just told you how they are feeling before picking a mode.";
            case BriefCopeIntro:     return "The player is about to start a short check-in about how they have been coping.";
            case BriefCopeHalfway:   return "The player is exactly halfway through a short check-in. Do not name any game mode yet.";
            case ModeRecommendation: return "You are suggesting which mode suits the player right now. Make it feel like a friendly suggestion, never a diagnosis.";
            default:                 return "The player just opened the main menu.";
        }
    }

    /// <summary>
    /// The priority ladder. Returns at most two facts, most interesting first.
    /// Anything the context does not know is simply skipped.
    /// </summary>
    public static List<string> SelectFacts(string contextName, CoachByteContext ctx)
    {
        var facts = new List<string>();
        if (ctx == null) return facts;

        // 1 - beating your own record outranks everything else.
        if (ctx.isNewPersonalBest && ctx.sessionScore.HasValue)
            facts.Add("They just set a new personal best of " + ctx.sessionScore.Value + " points");

        // 2 - levelling up is the other genuinely new thing that can happen.
        if (facts.Count < 2 && ctx.leveledUp && ctx.newLevel.HasValue)
            facts.Add("They just reached level " + ctx.newLevel.Value);

        // 3 - a big combo is the most quotable in-session moment.
        if (facts.Count < 2 && ctx.sessionCombo.HasValue && ctx.sessionCombo.Value >= 10)
            facts.Add("They hit a " + ctx.sessionCombo.Value + "-hit combo in " + ModeLabel(ctx.currentMode));

        // 4 - otherwise, the plain result of the round they just played.
        if (facts.Count < 2 && ctx.sessionScore.HasValue && !ctx.isNewPersonalBest)
            facts.Add("They scored " + ctx.sessionScore.Value + " in " + ModeLabel(ctx.currentMode));

        // 5 - consistency, but only while the streak is actually live.
        if (facts.Count < 2 && ctx.currentStreak.HasValue && ctx.currentStreak.Value >= 2)
            facts.Add("They have played " + ctx.currentStreak.Value + " days in a row");

        // 6 - what they were doing last time, or how long they have been gone.
        if (facts.Count < 2 && contextName == MainMenuGreeting && !string.IsNullOrEmpty(ctx.lastMode))
        {
            int days = ctx.daysSinceLastPlay ?? 0;
            if (days >= 3)
            {
                facts.Add("They have not played for " + days + " days");
            }
            else
            {
                int? best = ctx.BestComboForLastMode;
                facts.Add(best.HasValue && best.Value >= 10
                    ? "Last time they played " + ModeLabel(ctx.lastMode) + " and their best combo there is " + best.Value
                    : "Last time they played " + ModeLabel(ctx.lastMode));
            }
        }

        // 7 - the suggestion from their own check-in.
        if (facts.Count < 2 && !string.IsNullOrEmpty(ctx.recommendedMode))
            facts.Add("Their check-in suggests " + ModeLabel(ctx.recommendedMode) + " suits them today");

        // 8 - how they said they were feeling, if it is still recent.
        if (facts.Count < 2 && !string.IsNullOrEmpty(ctx.recentMood))
            facts.Add("They said they are feeling " + ctx.recentMood);

        // 9 - tone hint only. Deliberately last, deliberately vague.
        if (facts.Count < 2 && !string.IsNullOrEmpty(ctx.copingDescriptor))
            facts.Add("For tone only, do not repeat this back: this player " + ctx.copingDescriptor);

        // 10 - nothing notable. An empty list is fine; Build() handles it.
        return facts;
    }

    /// <summary>
    /// Local line used when the backend is unreachable or the model overruns the
    /// word limit. Every entry is hand-counted to fit the chat bubble, and only
    /// ever states something the context actually knows.
    /// </summary>
    public static string Fallback(string contextName, CoachByteContext ctx)
    {
        if (ctx != null)
        {
            if (ctx.isNewPersonalBest)
                return "New personal best. That is the one to beat now.";

            if (ctx.leveledUp && ctx.newLevel.HasValue)
                return "Level " + ctx.newLevel.Value + " reached. Nice work out there.";

            if (ctx.sessionCombo.HasValue && ctx.sessionCombo.Value >= 10)
                return "That " + ctx.sessionCombo.Value + "-hit combo was sharp. Ready for another?";

            if (ctx.currentStreak.HasValue && ctx.currentStreak.Value >= 2)
                return ctx.currentStreak.Value + " days running now. Keep that going.";
        }

        switch (contextName)
        {
            case BoxingResult:       return "Good round. Every session counts, so let us go again.";
            case RageRoomResult:     return "Nice work in there. That is one way to unwind.";
            case CheckIn:            return "Got it. Let us find something that fits today.";
            case BriefCopeIntro:     return "Hey, I am Coach Byte. A few quick questions first.";
            case BriefCopeHalfway:   return "Halfway there. Keep going, there are no wrong answers.";
            case ModeRecommendation: return "Here is what looks like a good fit today.";
            default:                 return "Ready when you are. Pick whatever suits you today.";
        }
    }

    /// <summary>Player-facing mode name. "Meditate" is called Yoga everywhere in the UI.</summary>
    public static string ModeLabel(string mode)
    {
        switch (mode)
        {
            case "Boxing":   return "Boxing";
            case "RageRoom": return "Rage Room";
            case "Meditate": return "Yoga";
            default:         return string.IsNullOrEmpty(mode) ? "the game" : mode;
        }
    }
}
