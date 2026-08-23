using System;

// Local-only persistence record (no cloud sync — see BRIEF_COPE_CONTEXT.md guardrails).
[Serializable]
public class BriefCopeResult
{
    public long timestamp;
    public string mode;   // GameMode.ToString()
    public bool skipped;  // true if the player skipped the survey entirely
    // CopeBucket.ToString() ("Approach"/"Avoidant"/"Context") - the raw dominant
    // coping bucket from subscale scoring, independent of `mode`. Empty if skipped
    // or if this result was saved before this field existed (JsonUtility leaves
    // missing string fields as "" on old saved PlayerPrefs data, not null).
    public string dominantCopingStyle;
}
