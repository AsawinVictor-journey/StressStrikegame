using UnityEngine;
using TMPro;

/// <summary>
/// Coach Byte's main-menu greeting. Lives on the AICoachByte canvas in
/// MainMenuScene and writes into its speech-bubble text.
///
/// Runs from OnEnable, not Start, ON PURPOSE. Start fires once per object
/// lifetime, which meant the greeting was generated a single time and then sat
/// there unchanged - including after finishing a match and walking back into the
/// menu, exactly the moment there is something new worth saying. OnEnable fires
/// every time the canvas is shown, so the line is regenerated on each visit and
/// can react to the round that just happened.
///
/// Everything about WHAT it says lives in CoachByteContext (what is known) and
/// CoachBytePromptBuilder (what is worth mentioning). This component only owns
/// when to speak and where the text goes.
/// </summary>
public class CoachByteMenuGreeting : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The TMP text inside Coach Byte's chat bubble. Keep messages short - " +
             "the bubble fits roughly 14 words, which CoachByteMessenger enforces.")]
    [SerializeField] private TMP_Text greetingText;

    [Header("AI")]
    [SerializeField] private string geminiModel = "gemini-3.5-flash-lite";

    [Tooltip("Smallest gap between two generations, in seconds. This is only a guard " +
             "against the canvas being toggled several times in quick succession (rig " +
             "switching, a panel opening and closing) firing a burst of identical " +
             "requests. Set to 0 to regenerate on every single enable.")]
    [SerializeField] private float minSecondsBetweenRegenerations = 5f;

    // Static so the cooldown survives this object being disabled and re-enabled,
    // and even the menu scene being reloaded - which is precisely the case it
    // exists to cover.
    private static float _lastGeneratedRealtime = -999f;

    private void OnEnable()
    {
        if (greetingText == null)
        {
            Debug.LogWarning("[CoachByte] greetingText is not assigned - nothing to write the greeting into.", this);
            return;
        }

        // Unscaled: a paused or slowed menu must not stretch this cooldown.
        if (minSecondsBetweenRegenerations > 0f &&
            Time.unscaledTime - _lastGeneratedRealtime < minSecondsBetweenRegenerations)
        {
            return; // leave the existing line up rather than replacing it with an identical one
        }

        _lastGeneratedRealtime = Time.unscaledTime;

        var ctx = CoachByteContext.Gather(CoachBytePromptBuilder.MainMenuGreeting);
        CoachByteMessenger.Speak(this, CoachBytePromptBuilder.MainMenuGreeting, ctx, greetingText, geminiModel);
    }

    /// <summary>
    /// Forces a fresh greeting, ignoring the cooldown. For a UI button that wants
    /// to nudge Coach Byte into saying something new on demand.
    /// </summary>
    public void RegenerateNow()
    {
        if (greetingText == null) return;

        _lastGeneratedRealtime = Time.unscaledTime;

        var ctx = CoachByteContext.Gather(CoachBytePromptBuilder.MainMenuGreeting);
        CoachByteMessenger.Speak(this, CoachBytePromptBuilder.MainMenuGreeting, ctx, greetingText, geminiModel);
    }
}
