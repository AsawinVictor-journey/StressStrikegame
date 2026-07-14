using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public float timer = 120f;
    public int objectsRemaining;

    bool gameEnded = false;
    public TMP_Text timerText;
    public string menuSceneName = "Rage Room Menu";
    public float resultScreenDuration = 6f;

    public CanvasGroup resultUI;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        objectsRemaining = FindObjectsOfType<DestructibleObject>().Length;
        UpdateTimerUI();
    }
    void Update()
    {
        if (gameEnded) return;

        timer -= Time.deltaTime;

        UpdateTimerUI();

        if (timer <= 0 || objectsRemaining <= 0)
        {
            EndGame();
        }
    }

    void UpdateTimerUI()
    {
        timerText.text = Mathf.CeilToInt(timer).ToString();
    }

    public void ObjectDestroyed()
    {
        objectsRemaining = Mathf.Max(0, objectsRemaining - 1);
    }

    void EndGame()
    {
        gameEnded = true;
        timer = 0f;

        UpdateTimerUI();

        FindFirstObjectByType<UIFade>()?.ShowUI(resultUI);
        ScoreSystem.Instance?.ShowResults();

        StartCoroutine(ReturnToMenu());

        Debug.Log("Game Ended");
    }

    IEnumerator ReturnToMenu()
    {
        yield return new WaitForSecondsRealtime(resultScreenDuration);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(menuSceneName);
        else
            SceneManager.LoadScene(menuSceneName);
    }
}