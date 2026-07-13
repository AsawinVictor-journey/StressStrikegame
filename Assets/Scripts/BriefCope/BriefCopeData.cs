using System.Collections.Generic;

// Brief COPE (Carver, 1997), trimmed to 1 item per subscale (14 of the original
// 28 items) to keep the pre-game survey short. The original instrument pairs 2
// near-synonymous items per subscale for reliability; since this is a quick,
// non-clinical "just a suggestion" screen (not a diagnostic tool) and the final
// decision aggregates 5-6 subscales per bucket, single-item measurement per
// subscale is an acceptable tradeoff. Item wording otherwise verbatim.
// Source: Carver, C. S. (1997). International Journal of Behavioral Medicine, 4(1), 92-100.
//         https://www.psy.miami.edu/faculty/ccarver/brief-cope.html

public enum CopeSubscale
{
    SelfDistraction,
    ActiveCoping,
    Denial,
    SubstanceUse,
    EmotionalSupport,
    InstrumentalSupport,
    BehavioralDisengagement,
    Venting,
    PositiveReframing,
    Planning,
    Humor,
    Acceptance,
    Religion,
    SelfBlame
}

public enum CopeBucket
{
    Approach,
    Avoidant,
    Context
}

public struct CopeQuestion
{
    public int id;
    public string text;
    public CopeSubscale subscale;

    public CopeQuestion(int id, string text, CopeSubscale subscale)
    {
        this.id = id;
        this.text = text;
        this.subscale = subscale;
    }
}

public static class BriefCopeData
{
    // Matches the shortened button labels used in the working prototype (index.html),
    // not the longer canonical Brief-COPE instrument wording.
    public static readonly (int value, string label)[] ResponseScale =
    {
        (1, "Not at all"),
        (2, "A little bit"),
        (3, "A medium amount"),
        (4, "A lot"),
    };

    // IDs kept from the original 1-28 numbering (used as the answers-dictionary key,
    // not an array index) so scoring stays traceable to the source instrument.
    public static readonly CopeQuestion[] Questions =
    {
        new CopeQuestion(19, "I've been doing something to think about it less, such as going to movies, watching TV, reading, daydreaming, sleeping, or shopping.", CopeSubscale.SelfDistraction),
        new CopeQuestion(7, "I've been taking action to try to make the situation better.", CopeSubscale.ActiveCoping),
        new CopeQuestion(8, "I've been refusing to believe that it has happened.", CopeSubscale.Denial),
        new CopeQuestion(4, "I've been using alcohol or other drugs to make myself feel better.", CopeSubscale.SubstanceUse),
        new CopeQuestion(15, "I've been getting comfort and understanding from someone.", CopeSubscale.EmotionalSupport),
        new CopeQuestion(6, "I've been giving up trying to deal with it.", CopeSubscale.BehavioralDisengagement),
        new CopeQuestion(21, "I've been expressing my negative feelings.", CopeSubscale.Venting),
        new CopeQuestion(10, "I've been getting help and advice from other people.", CopeSubscale.InstrumentalSupport),
        new CopeQuestion(17, "I've been looking for something good in what is happening.", CopeSubscale.PositiveReframing),
        new CopeQuestion(13, "I've been criticizing myself.", CopeSubscale.SelfBlame),
        new CopeQuestion(14, "I've been trying to come up with a strategy about what to do.", CopeSubscale.Planning),
        new CopeQuestion(18, "I've been making jokes about it.", CopeSubscale.Humor),
        new CopeQuestion(20, "I've been accepting the reality of the fact that it has happened.", CopeSubscale.Acceptance),
        // Uses the "religion or spiritual beliefs" item, not the other canonical Religion
        // item ("praying or meditating") - that wording would collide with this game's
        // own Meditate/Yoga mode name and bias how players read the question.
        new CopeQuestion(22, "I've been trying to find comfort in my religion or spiritual beliefs.", CopeSubscale.Religion),
    };

    private static readonly CopeSubscale[] AllSubscales =
    {
        CopeSubscale.SelfDistraction, CopeSubscale.ActiveCoping, CopeSubscale.Denial, CopeSubscale.SubstanceUse,
        CopeSubscale.EmotionalSupport, CopeSubscale.InstrumentalSupport, CopeSubscale.BehavioralDisengagement,
        CopeSubscale.Venting, CopeSubscale.PositiveReframing, CopeSubscale.Planning, CopeSubscale.Humor,
        CopeSubscale.Acceptance, CopeSubscale.Religion, CopeSubscale.SelfBlame,
    };

    private static readonly Dictionary<CopeSubscale, CopeBucket> BucketBySubscale = new Dictionary<CopeSubscale, CopeBucket>
    {
        { CopeSubscale.ActiveCoping, CopeBucket.Approach },
        { CopeSubscale.Planning, CopeBucket.Approach },
        { CopeSubscale.PositiveReframing, CopeBucket.Approach },
        { CopeSubscale.Acceptance, CopeBucket.Approach },
        { CopeSubscale.EmotionalSupport, CopeBucket.Approach },
        { CopeSubscale.InstrumentalSupport, CopeBucket.Approach },
        { CopeSubscale.Denial, CopeBucket.Avoidant },
        { CopeSubscale.SubstanceUse, CopeBucket.Avoidant },
        { CopeSubscale.BehavioralDisengagement, CopeBucket.Avoidant },
        { CopeSubscale.SelfDistraction, CopeBucket.Avoidant },
        { CopeSubscale.SelfBlame, CopeBucket.Avoidant },
        { CopeSubscale.Humor, CopeBucket.Context },
        { CopeSubscale.Religion, CopeBucket.Context },
        { CopeSubscale.Venting, CopeBucket.Context },
    };

    public static Dictionary<CopeSubscale, int> ScoreSubscales(Dictionary<int, int> answers)
    {
        var totals = new Dictionary<CopeSubscale, int>();
        foreach (var s in AllSubscales) totals[s] = 0;

        foreach (var q in Questions)
        {
            if (answers.TryGetValue(q.id, out int value))
                totals[q.subscale] += value;
        }
        return totals;
    }

    public static Dictionary<CopeBucket, int> ScoreBuckets(Dictionary<CopeSubscale, int> subscaleScores)
    {
        var totals = new Dictionary<CopeBucket, int>
        {
            { CopeBucket.Approach, 0 },
            { CopeBucket.Avoidant, 0 },
            { CopeBucket.Context, 0 },
        };

        foreach (var s in AllSubscales)
            totals[BucketBySubscale[s]] += subscaleScores[s];

        return totals;
    }

    public static CopeSubscale TopSubscaleInBucket(Dictionary<CopeSubscale, int> subscaleScores, CopeBucket bucket)
    {
        CopeSubscale best = CopeSubscale.ActiveCoping;
        bool found = false;

        foreach (var s in AllSubscales)
        {
            if (BucketBySubscale[s] != bucket) continue;
            if (!found || subscaleScores[s] > subscaleScores[best])
            {
                best = s;
                found = true;
            }
        }
        return best;
    }
}
