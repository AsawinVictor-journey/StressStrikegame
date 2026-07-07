using UnityEngine;
using TMPro;

namespace StressStrike
{

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Score Data")]
    public int currentScore = 0;
    public int highScore = 0;
    private const string HIGH_SCORE_KEY = "Player_HighScore";
    private const int SCORE_TO_COIN_RATE = 10;

    [Header("Combo Settings")]
    [SerializeField] private float _comboTimeoutWindow = 1.5f;
    [SerializeField] private float _comboMultiplierStep = 0.1f;
    [SerializeField] private float _maxComboMultiplier = 3f;

    private int _comboCount = 0;
    private float _lastHitTime = -999f;

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

        float multiplier = Mathf.Min(1f + (_comboCount - 1) * _comboMultiplierStep, _maxComboMultiplier);
        int points = Mathf.RoundToInt(power * multiplier);
        currentScore += points;

        UpdateScoreUI();
    }

    public void ResetCombo()
    {
        _comboCount = 0;
        UpdateScoreUI();
    }

    public void FinalizeMatch(out int finalScore, out int coinsAwarded, out bool isNewHighScore)
    {
        finalScore = currentScore;
        coinsAwarded = Mathf.RoundToInt(currentScore / (float)SCORE_TO_COIN_RATE);

        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.AddCoins(coinsAwarded);
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
