using System;

// Local-only persistence record (no cloud sync — see BRIEF_COPE_CONTEXT.md guardrails).
[Serializable]
public class BriefCopeResult
{
    public long timestamp;
    public string mode;   // GameMode.ToString()
    public bool skipped;  // true if the player skipped the survey entirely
}
