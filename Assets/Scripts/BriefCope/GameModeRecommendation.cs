using System.Collections.Generic;

// The actual game only has 3 modes (confirmed against the Unity project:
// Assets/b-o-o-k/BoxingMenu.unity, Assets/Scenes/Rage Room/Rage Room.unity, Assets/evococo/meditation.unity).
public enum GameMode
{
    Boxing,     // structured sparring, strategy rewarded
    RageRoom,   // pure catharsis, hit stuff, no fail state
    Meditate,   // guided breathing, no pressure
}

public struct ModeRecommendation
{
    public GameMode mode;
    public string modeName;
    public string coachMessage;
    public string reason; // plain-language line drawn from the winning bucket's top subscale
}

public struct ModeCardInfo
{
    public GameMode mode;
    public string icon;
    public string title;
    public string blurb;
}

public static class GameModeRecommendation
{
    // Exact scene names for SceneTransitionManager.LoadScene(string).
    public static readonly Dictionary<GameMode, string> SceneNames = new Dictionary<GameMode, string>
    {
        { GameMode.Boxing, "BoxingMenu" },
        { GameMode.RageRoom, "Rage Room" },
        { GameMode.Meditate, "meditation" },
    };

    // All 3 modes are always shown and pickable — the recommendation only pre-selects one.
    public static readonly ModeCardInfo[] ModeCards =
    {
        new ModeCardInfo { mode = GameMode.Boxing, icon = "🏆", title = "Boxing", blurb = "Structured fights, strategy rewarded." },
        new ModeCardInfo { mode = GameMode.RageRoom, icon = "🥊", title = "Rage Room", blurb = "No fail state — just hit stuff." },
        new ModeCardInfo { mode = GameMode.Meditate, icon = "🌿", title = "Meditate", blurb = "Guided breathing, zero pressure." },
    };

    private struct ModeInfo
    {
        public string modeName;
        public string coachMessage;
    }

    private static readonly Dictionary<GameMode, ModeInfo> ModeInfoByMode = new Dictionary<GameMode, ModeInfo>
    {
        {
            GameMode.Boxing,
            new ModeInfo
            {
                modeName = "Boxing",
                coachMessage =
                    "That's real fighter instinct. Let's get you into Boxing, where every match rewards strategy " +
                    "as much as power. Read your opponent, build your gameplan, and work the fight your way.",
            }
        },
        {
            GameMode.RageRoom,
            new ModeInfo
            {
                modeName = "Rage Room",
                coachMessage =
                    "That's exactly what the Rage Room's for: no scorecards, no losing, just you and every ounce " +
                    "of stress you're ready to throw at something.",
            }
        },
        {
            GameMode.Meditate,
            new ModeInfo
            {
                modeName = "Meditate",
                coachMessage =
                    "So let's slow down for a second first. Meditate isn't a step back, it's your warm-up: guided " +
                    "breathing, no pressure, no clock. When you're ready to swing for real, the ring will still be here.",
            }
        },
    };

    // Plain-language explanation shown on the result screen, keyed by whichever
    // subscale scored highest within the winning bucket. Sensitive subscales
    // (SubstanceUse, SelfBlame, Denial, BehavioralDisengagement) are worded as
    // "leaned toward", never "diagnosed with" — see guardrails in BRIEF_COPE_CONTEXT.md.
    public static readonly Dictionary<CopeSubscale, string> ReasonBySubscale = new Dictionary<CopeSubscale, string>
    {
        { CopeSubscale.ActiveCoping, "You scored highest on taking direct action — when stress hits, you'd rather do something about it than sit with it." },
        { CopeSubscale.Planning, "You scored highest on planning — you like to map out a strategy before you make your move." },
        { CopeSubscale.PositiveReframing, "You scored highest on positive reframing — you tend to look for the upside even when things are rough." },
        { CopeSubscale.Acceptance, "You scored highest on acceptance — you deal with hard situations by facing them as they are, not fighting them." },
        { CopeSubscale.EmotionalSupport, "You scored highest on emotional support — you lean on people for comfort when things get heavy." },
        { CopeSubscale.InstrumentalSupport, "You scored highest on getting advice and help — you reach out to others for practical guidance." },
        { CopeSubscale.Denial, "Your answers leaned toward denial — pushing back on believing hard things are really happening." },
        { CopeSubscale.SubstanceUse, "Your answers leaned toward using substances to get through tough moments." },
        { CopeSubscale.BehavioralDisengagement, "Your answers leaned toward stepping back or giving up on dealing with things directly." },
        { CopeSubscale.SelfDistraction, "Your answers leaned toward distraction — keeping busy so you don't have to think about it." },
        { CopeSubscale.SelfBlame, "Your answers leaned toward being hard on yourself when things go wrong." },
        { CopeSubscale.Humor, "You scored highest on humor — you take the edge off stress by joking about it." },
        { CopeSubscale.Religion, "You scored highest on religion or spirituality — you find steadiness through prayer or reflection." },
        { CopeSubscale.Venting, "You scored highest on venting — you deal with stress by letting your feelings out, not bottling them up." },
    };

    public const string Disclaimer =
        "This is just a suggestion based on how you said you've been coping lately — not a diagnosis. " +
        "Pick whatever mode you're actually in the mood for.";

    private static GameMode PickContextMode(CopeSubscale topSubscale)
    {
        if (topSubscale == CopeSubscale.Religion) return GameMode.Meditate;
        return GameMode.RageRoom; // Humor or Venting
    }

    public static ModeRecommendation Recommend(Dictionary<int, int> answers)
    {
        var subscaleScores = BriefCopeData.ScoreSubscales(answers);
        var bucketScores = BriefCopeData.ScoreBuckets(subscaleScores);

        // Fixed order (not dictionary enumeration order) so exact-tie break is
        // deterministic and matches the prototype's Approach>Avoidant>Context precedence.
        CopeBucket topBucket = CopeBucket.Approach;
        foreach (var bucket in new[] { CopeBucket.Approach, CopeBucket.Avoidant, CopeBucket.Context })
        {
            if (bucketScores[bucket] > bucketScores[topBucket])
                topBucket = bucket;
        }

        GameMode mode;
        if (topBucket == CopeBucket.Avoidant)
        {
            mode = GameMode.Meditate;
        }
        else if (topBucket == CopeBucket.Approach)
        {
            mode = GameMode.Boxing;
        }
        else
        {
            mode = PickContextMode(BriefCopeData.TopSubscaleInBucket(subscaleScores, CopeBucket.Context));
        }

        CopeSubscale topSubscale = BriefCopeData.TopSubscaleInBucket(subscaleScores, topBucket);
        var info = ModeInfoByMode[mode];
        return new ModeRecommendation
        {
            mode = mode,
            modeName = info.modeName,
            coachMessage = info.coachMessage,
            reason = ReasonBySubscale[topSubscale],
        };
    }
}
