using UnityEngine;
using TMPro;

public class ScoreSystem : MonoBehaviour
{
    public static ScoreSystem Instance;

    public int streak = 0;
    public int score;

    public float streakResetTime = 2f;
    private float lastHitTime;

    public TMP_Text scoreText;
    public TMP_Text streakText;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
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

        float multiplier = 1f + (streak - 1) * 0.1f;
        multiplier = Mathf.Min(multiplier, 3f);

        int baseScore = Mathf.RoundToInt(velocity * 10f);

        score += Mathf.RoundToInt(baseScore * multiplier);

        UpdateUI();
    }

    void UpdateUI()
    {
        scoreText.text = score.ToString();
        streakText.text = streak.ToString();
    }
}