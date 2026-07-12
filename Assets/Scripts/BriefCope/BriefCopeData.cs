using System.Collections.Generic;

// Brief COPE (Carver, 1997) — 28 items, verbatim canonical wording.
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

    public static readonly CopeQuestion[] Questions =
    {
        new CopeQuestion(1, "I've been turning to work or other activities to take my mind off things.", CopeSubscale.SelfDistraction),
        new CopeQuestion(2, "I've been concentrating my efforts on doing something about the situation I'm in.", CopeSubscale.ActiveCoping),
        new CopeQuestion(3, "I've been saying to myself \"this isn't real.\"", CopeSubscale.Denial),
        new CopeQuestion(4, "I've been using alcohol or other drugs to make myself feel better.", CopeSubscale.SubstanceUse),
        new CopeQuestion(5, "I've been getting emotional support from others.", CopeSubscale.EmotionalSupport),
        new CopeQuestion(6, "I've been giving up trying to deal with it.", CopeSubscale.BehavioralDisengagement),
        new CopeQuestion(7, "I've been taking action to try to make the situation better.", CopeSubscale.ActiveCoping),
        new CopeQuestion(8, "I've been refusing to believe that it has happened.", CopeSubscale.Denial),
        new CopeQuestion(9, "I've been saying things to let my unpleasant feelings escape.", CopeSubscale.Venting),
        new CopeQuestion(10, "I've been getting help and advice from other people.", CopeSubscale.InstrumentalSupport),
        new CopeQuestion(11, "I've been using alcohol or other drugs to help me get through it.", CopeSubscale.SubstanceUse),
        new CopeQuestion(12, "I've been trying to see it in a different light, to make it seem more positive.", CopeSubscale.PositiveReframing),
        new CopeQuestion(13, "I've been criticizing myself.", CopeSubscale.SelfBlame),
        new CopeQuestion(14, "I've been trying to come up with a strategy about what to do.", CopeSubscale.Planning),
        new CopeQuestion(15, "I've been getting comfort and understanding from someone.", CopeSubscale.EmotionalSupport),
        new CopeQuestion(16, "I've been giving up the attempt to cope.", CopeSubscale.BehavioralDisengagement),
        new CopeQuestion(17, "I've been looking for something good in what is happening.", CopeSubscale.PositiveReframing),
        new CopeQuestion(18, "I've been making jokes about it.", CopeSubscale.Humor),
        new CopeQuestion(19, "I've been doing something to think about it less, such as going to movies, watching TV, reading, daydreaming, sleeping, or shopping.", CopeSubscale.SelfDistraction),
        new CopeQuestion(20, "I've been accepting the reality of the fact that it has happened.", CopeSubscale.Acceptance),
        new CopeQuestion(21, "I've been expressing my negative feelings.", CopeSubscale.Venting),
        new CopeQuestion(22, "I've been trying to find comfort in my religion or spiritual beliefs.", CopeSubscale.Religion),
        new CopeQuestion(23, "I've been trying to get advice or help from other people about what to do.", CopeSubscale.InstrumentalSupport),
        new CopeQuestion(24, "I've been learning to live with it.", CopeSubscale.Acceptance),
        new CopeQuestion(25, "I've been thinking hard about what steps to take.", CopeSubscale.Planning),
        new CopeQuestion(26, "I've been blaming myself for things that happened.", CopeSubscale.SelfBlame),
        new CopeQuestion(27, "I've been praying or meditating.", CopeSubscale.Religion),
        new CopeQuestion(28, "I've been making fun of the situation.", CopeSubscale.Humor),
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
