using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Rage Room scoring + result screen. On ShowResults(), this feeds a session summary into
/// PlayerProgression (see PlayerProgression.cs for the XP/Coin/Level formula rationale) instead
/// of the old flat "coins = score/10" rule.
///
/// Mode-specific inputs to the shared formulas:
///   - performanceNormalized (0-1): the existing Score value, normalized against
///     targetScoreForFullPerformance (a tunable "what counts as a great session" reference,
///     same pattern used in Boxing) since this mode has no separate accuracy stat.
///   - intensityUnits: total hit velocity accumulated over the session (added in AddHit),
///     standing in for "how hard did the player hit things" for the coin formula.
///   - durationMinutes: wall-clock time from this component's Start() to ShowResults().
///
/// `streak`/`highestStreak` below are this mode's existing hit-combo mechanic (resets after
/// streakResetTime seconds of no hits, drives the score multiplier) — this is NOT the removed
/// daily-login Streak feature and is unrelated to PlayerProgression, so it stays.
///
/// No persistent personal-best exists for this mode yet (ScoreSystem is scene-local and not
/// saved anywhere), so isNewPersonalBest is not tracked here — adding that needs persistence,
/// which is out of scope for this task.
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    public static ScoreSystem Instance;

    public int streak = 0;
    public int score;
    public int highestStreak = 0;
    public int coin;

    public float streakResetTime = 2f;
    private float lastHitTime;

    [Tooltip("Score value treated as 100% performance for XP purposes.")]
    public int targetScoreForFullPerformance = 500;

    private float sessionStartTime;
    private float intensityUnits;

    public TMP_Text scoreText;
    public TMP_Text streakText;

    public GameObject resultPanel;
    public TMP_Text resultScoreText;
    public TMP_Text resultHighestStreakText;
    public TMP_Text resultCoinsEarned;
    public TMP_Text resultXPEarned;
    public TMP_Text resultLevelText;
    public Image resultLevelBarFill;
    public GameObject levelUpBanner;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        sessionStartTime = Time.time;
        UpdateUI();
    }

    void Update()
    {
        if (streak > 0 && Time.time - lastHitTime > streakResetTime)
        {
            streak = 0;
            UpdateUI();
        }
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
    }

    public void AddHit(float velocity)
    {
        if (Time.time - lastHitTime <= streakResetTime)
            streak++;
        else
            streak = 1;

        lastHitTime = Time.time;

        if (streak > highestStreak)
            highestStreak = streak;

        intensityUnits += velocity;

        float multiplier = 1f + (streak - 1) * 0.1f;
        multiplier = Mathf.Min(multiplier, 3f);

        int baseScore = Mathf.RoundToInt(velocity * 10f);
        score += Mathf.RoundToInt(baseScore * multiplier);
        UpdateUI();
    }

    public PlayerProgression.SessionRewardResult ShowResults()
    {
        PlayerProgression.SessionRewardResult reward = default;

        if (PlayerProgression.Instance != null)
        {
            float performanceNormalized = targetScoreForFullPerformance > 0
                ? (float)score / targetScoreForFullPerformance
                : 0f;
            float durationMinutes = (Time.time - sessionStartTime) / 60f;

            int xp = PlayerProgression.CalculateXP(performanceNormalized, durationMinutes);
            int coins = PlayerProgression.Instance.CalculateCoins(score, intensityUnits);

            reward = PlayerProgression.Instance.AddSessionResult(xp, coins);
        }

        coin = reward.CoinsAwarded;

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultScoreText != null)
            resultScoreText.text = score.ToString();

        if (resultHighestStreakText != null)
            resultHighestStreakText.text = highestStreak.ToString();

        if (resultCoinsEarned != null)
            resultCoinsEarned.text = "+" + coin;

        if (resultXPEarned != null)
            resultXPEarned.text = "+" + reward.XPAwarded + " XP";

        if (resultLevelText != null)
            resultLevelText.text = reward.Level.ToString();

        if (resultLevelBarFill != null)
        {
            resultLevelBarFill.fillAmount = reward.PreviousLevelProgress01;
            StartCoroutine(AnimateLevelBar(resultLevelBarFill,
                reward.PreviousLevelProgress01, reward.LevelProgress01, reward.LeveledUp));
        }

        if (levelUpBanner != null)
            levelUpBanner.SetActive(reward.LeveledUp);

        return reward;
    }

    // Animates the level bar filling up when the result panel appears, rather than snapping
    // straight to the post-session value. On a level-up it fills to full, snaps back to empty,
    // then continues filling into the new level, so a level-up reads as a level-up rather than
    // the bar just landing on a lower-looking number.
    IEnumerator AnimateLevelBar(Image bar, float fromProgress, float toProgress, bool leveledUp)
    {
        const float fillDuration = 0.5f;

        if (leveledUp)
        {
            yield return LerpFill(bar, fromProgress, 1f, fillDuration);
            bar.fillAmount = 0f;
            yield return LerpFill(bar, 0f, toProgress, fillDuration);
        }
        else
        {
            yield return LerpFill(bar, fromProgress, toProgress, fillDuration);
        }
    }

    IEnumerator LerpFill(Image bar, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            bar.fillAmount = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        bar.fillAmount = to;
    }

    void UpdateUI()
    {
        scoreText.text = score.ToString();
        streakText.text = streak.ToString();
    }
}
