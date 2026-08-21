using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Per-session AI check-in: player taps a mood chip and/or types free text, then
// a local llama.cpp server picks a mode. Brief-COPE's last saved result is the
// fallback whenever the AI call fails, times out, or the player skips.
public class CheckInManager : MonoBehaviour
{
    private const string BriefCopePrefsKey = "BriefCope_LastResult"; // shared with BriefCopeSurveyController

    // Set once per app run, the first time a check-in resolves (decided or skipped),
    // so BriefCopeSurveyController only routes into the check-in scene once per session.
    public static bool HasCheckedInThisSession { get; private set; }

    [Header("Mood chips")]
    [SerializeField] private Button angryChip;
    [SerializeField] private Button anxiousChip;
    [SerializeField] private Button tiredChip;
    [SerializeField] private Button calmChip;
    [SerializeField] private Color chipIdleColor = Color.white;
    [SerializeField] private Color chipSelectedColor = new Color(1f, 0.35f, 0.37f);

    [Header("Free text")]
    [SerializeField] private TMP_InputField freeTextInput;
    [SerializeField] private Button submitButton;

    [Header("Skip")]
    [SerializeField] private Button skipButton;
    [Tooltip("The overlay canvas this check-in UI lives on. Skip hides this " +
             "directly instead of picking a fallback mode - skipping means the " +
             "player opted out of the check-in, not \"pick something for me\".")]
    [SerializeField] private GameObject checkInCanvas;

    [Header("Input UI")]
    [Tooltip("Container holding the mood chips, free text, submit/skip buttons, " +
             "backdrop, etc. - everything except the result panel. Hidden once a " +
             "mode is decided so only the result panel shows.")]
    [SerializeField] private GameObject checkInBody;

    [Header("Status (optional)")]
    [SerializeField] private TMP_Text statusText;

    [Header("AI Server (llama.cpp, local)")]
    [SerializeField] private float requestTimeoutSeconds = 10f;

    [Header("Fallback")]
    [Tooltip("Populated automatically from the player's saved Brief-COPE result " +
             "(PlayerPrefs) on Awake. Used whenever the AI call fails, times out, or is skipped.")]
    public BriefCopeResult fallbackProfile;

    public event Action<string, string> onModeDecided;

    private string selectedMood;
    private bool requestInFlight;

    private void Awake()
    {
        fallbackProfile = LoadFallbackProfile();
    }

    private void Start()
    {
        if (angryChip != null) angryChip.onClick.AddListener(() => SelectMood("angry"));
        if (anxiousChip != null) anxiousChip.onClick.AddListener(() => SelectMood("anxious"));
        if (tiredChip != null) tiredChip.onClick.AddListener(() => SelectMood("tired"));
        if (calmChip != null) calmChip.onClick.AddListener(() => SelectMood("calm"));
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
        if (skipButton != null) skipButton.onClick.AddListener(Skip);
    }

    private static BriefCopeResult LoadFallbackProfile()
    {
        string json = PlayerPrefs.GetString(BriefCopePrefsKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<BriefCopeResult>(json); }
        catch (Exception e)
        {
            Debug.LogWarning($"CheckInManager: failed to parse saved Brief-COPE result: {e.Message}");
            return null;
        }
    }

    public void SelectMood(string mood)
    {
        selectedMood = mood;
        SetChipTint(angryChip, mood == "angry");
        SetChipTint(anxiousChip, mood == "anxious");
        SetChipTint(tiredChip, mood == "tired");
        SetChipTint(calmChip, mood == "calm");
    }

    private void SetChipTint(Button chip, bool selected)
    {
        if (chip == null) return;
        var img = chip.targetGraphic as Image;
        if (img != null) img.color = selected ? chipSelectedColor : chipIdleColor;
    }

    public void Submit()
    {
        if (requestInFlight) return;

        string freeText = freeTextInput != null ? freeTextInput.text : "";
        if (string.IsNullOrEmpty(selectedMood) && string.IsNullOrWhiteSpace(freeText))
        {
            // Nothing entered - treat it as a skip instead of firing an empty request.
            Skip();
            return;
        }

        StartCoroutine(RunCheckIn(selectedMood, freeText));
    }

    public void Skip()
    {
        if (requestInFlight) return;

        // Skip means the player opted out - no mode picked, no result panel,
        // just close the check-in overlay and return to whatever's underneath it.
        HasCheckedInThisSession = true;
        Debug.Log("CheckInManager: skipped - closing check-in UI without picking a mode.");
        if (checkInCanvas != null) checkInCanvas.SetActive(false);
    }

    private IEnumerator RunCheckIn(string mood, string freeText)
    {
        requestInFlight = true;
        if (statusText != null) statusText.text = "Checking in...";

        string prompt = BuildPrompt(mood, freeText);
        string decidedMode = null;
        string aiStressType = null;

        yield return LlamaCppClient.Generate(
            prompt,
            requestTimeoutSeconds,
            onSuccess: text =>
            {
                decidedMode = ParseMode(text);
                aiStressType = ParseStressType(text);
            },
            onError: err => Debug.LogWarning($"CheckInManager: AI check-in failed ({err}); falling back to Brief-COPE result.")
        );

        requestInFlight = false;
        if (statusText != null) statusText.text = "";

        if (!string.IsNullOrEmpty(decidedMode))
        {
            string finalMode = ApplyCopeModifier(decidedMode, out bool steppedDown);
            string reason = BuildAiReason(aiStressType, decidedMode, finalMode, steppedDown);
            Decide(finalMode, reason, "ai");
        }
        else
        {
            Debug.LogWarning("CheckInManager: no usable mode from the AI response; falling back to mood/Brief-COPE heuristic.");
            string mode = ResolveFallbackMode(mood, freeText);
            Decide(mode, BuildFallbackReason(mode), "ai_fallback");
        }
    }

    // Narrows a successful AI suggestion using the player's raw dominant coping
    // bucket from their last Brief-COPE survey (BriefCopeData's Approach/Avoidant/
    // Context split from subscale scoring - see GameModeRecommendation.Recommend()),
    // not the mode it produced. Simple step-down table, not another AI call.
    // Only Avoidant steps things down; Approach/Context trust the AI as-is.
    // Leaves the ai_fallback/skip paths (ResolveFallbackMode) untouched.
    private string ApplyCopeModifier(string aiMode, out bool steppedDown)
    {
        bool avoidantDominant = fallbackProfile != null
            && !fallbackProfile.skipped
            && fallbackProfile.dominantCopingStyle == nameof(CopeBucket.Avoidant);

        steppedDown = false;
        if (!avoidantDominant) return aiMode;

        switch (aiMode)
        {
            case "boxing": steppedDown = true; return "rage_room";
            case "rage_room": steppedDown = true; return "yoga";
            default: return aiMode; // already yoga, or unrecognized
        }
    }

    // Built in code from data the success path already has - never AI-generated text.
    private static string BuildAiReason(string stressType, string aiSuggestedMode, string finalMode, bool steppedDown)
    {
        string feeling = string.IsNullOrEmpty(stressType) ? "stressed" : stressType;

        if (!steppedDown)
            return $"You mentioned feeling {feeling}, so we suggested {ModeLabel(finalMode)}.";

        return $"You mentioned feeling {feeling}, so we suggested {ModeLabel(aiSuggestedMode)}. " +
               $"Based on how you've been coping lately, we adjusted it to {ModeLabel(finalMode)} for a gentler option.";
    }

    private static string BuildFallbackReason(string mode)
    {
        return $"Based on your usual preferences, we picked {ModeLabel(mode)}.";
    }

    private static string ModeLabel(string modeKey)
    {
        return CheckInModeMapping.TryToGameMode(modeKey, out GameMode gameMode)
            ? GameModeRecommendation.ModeNameFor(gameMode)
            : modeKey;
    }

    private static string BuildPrompt(string mood, string freeText)
    {
        var sb = new StringBuilder();
        sb.Append("You are a mode-picker for a stress-relief game with exactly three modes: ");
        sb.Append("'boxing' (structured sparring, channel focused energy), ");
        sb.Append("'rage_room' (smash objects, pure catharsis, no fail state), ");
        sb.Append("'yoga' (guided breathing, calm, no pressure). ");
        sb.Append("A player is checking in before playing. ");
        if (!string.IsNullOrEmpty(mood)) sb.Append($"They tapped the mood chip '{mood}'. ");
        if (!string.IsNullOrWhiteSpace(freeText)) sb.Append($"They wrote: \"{freeText}\". ");
        sb.Append("Reply in exactly this format: \"mode: <boxing|rage_room|yoga>; feeling: <one word>\". " +
                   "<one word> is a short label for how they're feeling (e.g. angry, anxious, tired, calm, overwhelmed). No other text.");
        return sb.ToString();
    }

    private static string ParseMode(string aiText)
    {
        if (string.IsNullOrWhiteSpace(aiText)) return null;
        string lower = aiText.ToLowerInvariant();
        if (lower.Contains("rage_room") || lower.Contains("rage room")) return "rage_room";
        if (lower.Contains("boxing")) return "boxing";
        if (lower.Contains("yoga")) return "yoga";
        return null;
    }

    // Best-effort - small local models don't always follow the format exactly,
    // so a missing/unparseable "feeling:" just falls back to "stressed" in BuildAiReason.
    private static string ParseStressType(string aiText)
    {
        if (string.IsNullOrWhiteSpace(aiText)) return null;
        int idx = aiText.IndexOf("feeling:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        string tail = aiText.Substring(idx + "feeling:".Length).Trim();
        string word = tail.Split(new[] { ' ', ';', ',', '.', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrEmpty(word) ? null : word.ToLowerInvariant();
    }

    // Used when the AI call fails after the player picked a chip or typed
    // something - the tapped chip (or a keyword match in their free text) should
    // still count for something, rather than always collapsing to the same mode.
    private string ResolveFallbackMode(string mood, string freeText)
    {
        string moodMode = ModeForMood(mood) ?? ModeForKeywords(freeText);
        if (moodMode != null) return moodMode;

        if (fallbackProfile != null && !fallbackProfile.skipped && !string.IsNullOrEmpty(fallbackProfile.mode))
        {
            switch (fallbackProfile.mode)
            {
                case "Boxing": return "boxing";
                case "RageRoom": return "rage_room";
                case "Meditate": return "yoga";
            }
        }

        // No mood, no free text, no usable Brief-COPE result - lowest intensity
        // is the safer choice when we have zero information about how someone's feeling.
        return "yoga";
    }

    private static string ModeForMood(string mood)
    {
        switch (mood)
        {
            case "angry": return "rage_room";
            case "anxious": return "yoga";
            case "tired": return "yoga";
            case "calm": return "boxing";
            default: return null;
        }
    }

    private static string ModeForKeywords(string freeText)
    {
        if (string.IsNullOrWhiteSpace(freeText)) return null;
        string lower = freeText.ToLowerInvariant();

        if (ContainsAny(lower, "angry", "mad", "furious", "rage", "pissed")) return "rage_room";
        if (ContainsAny(lower, "anxious", "nervous", "worried", "panic", "stressed", "overwhelmed")) return "yoga";
        if (ContainsAny(lower, "tired", "exhausted", "sleepy", "drained")) return "yoga";
        if (ContainsAny(lower, "calm", "relaxed", "focused", "fine", "good")) return "boxing";

        return null;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle)) return true;
        }
        return false;
    }

    private void Decide(string mode, string reason, string source)
    {
        HasCheckedInThisSession = true;
        Debug.Log($"CheckInManager: decided '{mode}' (source: {source}) - {reason}");
        if (checkInBody != null) checkInBody.SetActive(false);
        onModeDecided?.Invoke(mode, reason);
    }
}
