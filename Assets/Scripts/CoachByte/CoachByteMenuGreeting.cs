using UnityEngine;
using TMPro;

// Drop this on a GameObject near CoachByteWordmark in the main menu and assign
// greetingText. On menu load it asks Gemini (via the StressStrike backend
// proxy - see GeminiClient) for a short, personalized greeting referencing
// the player's last Brief-COPE recommendation (if any), and shows it.
// Silently does nothing if the backend isn't reachable.
public class CoachByteMenuGreeting : MonoBehaviour
{
    private const string PrefsKey = "BriefCope_LastResult";

    [SerializeField] private TMP_Text greetingText;
    [SerializeField] private string geminiModel = "gemini-3.5-flash-lite";

    private void Start()
    {
        string mode = null;
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var result = JsonUtility.FromJson<BriefCopeResult>(json);
                if (result != null && !result.skipped && !string.IsNullOrEmpty(result.mode))
                    mode = result.mode;
            }
            catch
            {
                // Corrupt/old PlayerPrefs value - just skip the mode callout.
            }
        }

        string prompt = mode != null
            ? "You are Coach Byte, a friendly, upbeat AI coach in a stress-relief boxing game. " +
              $"In one short, punchy sentence (max 20 words), welcome the player back to the main menu and " +
              $"casually reference that you last recommended the '{mode}' mode. No emojis, no quotation marks."
            : "You are Coach Byte, a friendly, upbeat AI coach in a stress-relief boxing game. " +
              "In one short, punchy sentence (max 20 words), welcome the player to the main menu. " +
              "No emojis, no quotation marks.";

        StartCoroutine(GeminiClient.Generate(
            geminiModel,
            prompt,
            onSuccess: text =>
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                if (greetingText != null) greetingText.text = text;
                CoachByteHistory.Append("MainMenuGreeting", mode, text);
            },
            onError: err => Debug.LogWarning("[CoachByte] " + err)
        ));
    }
}
