using UnityEngine;
using TMPro;

namespace StressStrike
{

/// <summary>
/// Boxing scoring. FinalizeMatch() feeds a session summary into PlayerProgression (see
/// PlayerProgression.cs for the XP/Coin/Level formula rationale). PlayerProgression.AddSessionResult
/// credits CoinManager (Assets/b-o-o-k/shop system/CoinManager.cs — the shop's actual wallet)
/// directly, so this script must NOT also call CoinManager.AddCoins() itself, or every match
/// would double-award coins.
///
/// Mode-specific inputs to the shared formulas:
///   - performanceNormalized (0-1): currentScore (which already bakes in the combo multiplier)
///     normalized against targetScoreForFullPerformance — this mode has no separate
///     accuracy stat, so score-with-combo is the closest existing "how well did they fight"
///     signal, same pattern used in Rage Room.
///   - intensityUnits: total hit power accumulated over the session (added in RegisterHit).
///   - durationMinutes: wall-clock time from this component's Start() to FinalizeMatch().
///
/// isNewHighScore already exists here (PlayerPrefs-backed, pre-existing, untouched) and is
/// reused as the Result Screen's personal-best flag for this mode.
///
/// _highestCombo is the peak _comboCount reached during the match (never reset by the combo
/// timeout, only by a new match's Awake/Start) — this is what the Result Screen's "Combo
/// Streak" field shows, matching Rage Room's highestStreak.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Score Data")]
    public int currentScore = 0;
    public int highScore = 0;
    private const string HIGH_SCORE_KEY = "Player_HighScore";

    [Tooltip("Score value treated as 100% performance for XP purposes.")]
    public int targetScoreForFullPerformance = 500;

    [Header("Combo Settings")]
    [SerializeField] private float _comboTimeoutWindow = 1.5f;
    [SerializeField] private float _comboMultiplierStep = 0.1f;
    [SerializeField] private float _maxComboMultiplier = 3f;

    private int _comboCount = 0;
    private int _highestCombo = 0;
    private float _lastHitTime = -999f;
    private float _intensityUnits = 0f;
    private float _sessionStartTime;

    [Header("Live HUD UI (Optional)")]
    public TextMeshProUGUI scoreTextUI;
    public TextMeshProUGUI comboTextUI;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        _sessionStartTime = Time.time;
        LoadHighScore();
        UpdateScoreUI();
    }

    public void RegisterHit(float power)
    {
        if (Time.time - _lastHitTime > _comboTimeoutWindow)
        {
            _comboCount = 0;
        }
        _comboCount++;
        _lastHitTime = Time.time;

        if (_comboCount > _highestCombo)
            _highestCombo = _comboCount;

        float multiplier = Mathf.Min(1f + (_comboCount - 1) * _comboMultiplierStep, _maxComboMultiplier);
        int points = Mathf.RoundToInt(power * multiplier);
        currentScore += points;
        _intensityUnits += power;

        UpdateScoreUI();
    }

    public void ResetCombo()
    {
        _comboCount = 0;
        UpdateScoreUI();
    }

    public void FinalizeMatch(out int finalScore, out int highestCombo, out bool isNewHighScore, out PlayerProgression.SessionRewardResult reward)
    {
        finalScore = currentScore;
        highestCombo = _highestCombo;

        float performanceNormalized = targetScoreForFullPerformance > 0
            ? (float)currentScore / targetScoreForFullPerformance
            : 0f;
        float durationMinutes = (Time.time - _sessionStartTime) / 60f;

        int xpAwarded = PlayerProgression.CalculateXP(performanceNormalized, durationMinutes);
        int coinsAwarded = PlayerProgression.Instance != null
            ? PlayerProgression.Instance.CalculateCoins(currentScore, _intensityUnits)
            : 0;

        if (PlayerProgression.Instance != null)
        {
            // AddSessionResult credits CoinManager itself (the shop's wallet) — don't also
            // call CoinManager.AddCoins() here, that would double-award every match.
            reward = PlayerProgression.Instance.AddSessionResult(xpAwarded, coinsAwarded);
        }
        else
        {
            // Fallback for the (unexpected) case PlayerProgression isn't in the scene: still
            // credit the wallet directly so coins aren't silently lost, and report a
            // best-effort summary for display even though no real progression happened.
            if (CoinManager.Instance != null)
                CoinManager.Instance.AddCoins(coinsAwarded);

            reward = new PlayerProgression.SessionRewardResult
            {
                XPAwarded = xpAwarded,
                CoinsAwarded = coinsAwarded,
                TotalCoins = CoinManager.Instance != null ? CoinManager.Instance.currentCoins : 0,
                Level = 0,
                LeveledUp = false,
                LevelProgress01 = 0f,
                PreviousLevelProgress01 = 0f
            };
        }

        isNewHighScore = currentScore > highScore;
        if (isNewHighScore)
        {
            highScore = currentScore;
            SaveHighScore();
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreTextUI != null)
        {
            scoreTextUI.text = currentScore.ToString();
        }
        if (comboTextUI != null)
        {
            if (_comboCount > 1)
            {
                float displayMultiplier = 1f + (_comboCount - 1) * _comboMultiplierStep;
                comboTextUI.text = $"x{displayMultiplier:0.0} COMBO";
            }
            else
            {
                comboTextUI.text = "";
            }
        }
    }

    private void SaveHighScore()
    {
        PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
        PlayerPrefs.Save();
    }

    private void LoadHighScore()
    {
        highScore = PlayerPrefs.HasKey(HIGH_SCORE_KEY) ? PlayerPrefs.GetInt(HIGH_SCORE_KEY) : 0;
    }
}
}
