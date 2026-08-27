using System;
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
    private const string HighScoreKey = "Player_HighScore";

    [SerializeField] private TMP_Text greetingText;
    [SerializeField] private string geminiModel = "gemini-3.5-flash-lite";

    private void Start()
    {
        // High score lives with the gameplay stats, not on the survey record -
        // ScoreManager owns this key (ScoreManager.HIGH_SCORE_KEY).
        int highScore = PlayerPrefs.GetInt(HighScoreKey, 0);

        string mode = null;
        string copingStyle = null;
        long lastTimestamp = 0;

        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var result = JsonUtility.FromJson<BriefCopeResult>(json);
                if (result != null && !result.skipped && !string.IsNullOrEmpty(result.mode))
                {
                    mode = result.mode;
                    copingStyle = result.dominantCopingStyle;
                    lastTimestamp = result.timestamp;
                }
            }
            catch
            {
                // Corrupt/old PlayerPrefs value - just skip the personalization.
            }
        }

        // Build richer context for the prompt
        string context = "";
        if (mode != null && copingStyle != null)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long daysSince = (now - lastTimestamp) / 86400;
            string copingDescription = copingStyle switch
            {
                "Approach" => "usually face problems head-on",
                "Avoidant" => "sometimes need to take a break from stress",
                "Context" => "mix different strategies depending on the situation",
                _ => "are working on managing stress"
            };

            string scoreBonus = highScore > 0
                ? $" You're on fire — {highScore} hits is your personal best!"
                : "";

            context = $"The player last chose '{mode}' and tends to {copingDescription}. " +
                     $"It's been {daysSince} days since they last played.{scoreBonus}";
        }

        string prompt = mode != null
            ? "You are Coach Byte, a friendly, upbeat AI coach in a stress-relief boxing game. " +
              context +
              "In one short, punchy sentence (max 20 words), welcome them back warmly. " +
              "No emojis, no quotation marks."
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
