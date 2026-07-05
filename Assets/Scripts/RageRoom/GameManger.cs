using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public float timer = 120f;
    public int objectsRemaining;

    bool gameEnded = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        objectsRemaining = FindObjectsOfType<DestructibleObject>().Length;
    }

    void Update()
    {
        if (gameEnded) return;

        timer -= Time.deltaTime;

        if (timer <= 0 || objectsRemaining <= 0)
        {
            EndGame();
        }
    }

    public void ObjectDestroyed()
    {
        objectsRemaining = Mathf.Max(0, objectsRemaining - 1);
    }

    void EndGame()
    {
        gameEnded = true;
        timer = 0f;

        FindFirstObjectByType<UIFade>()?.ShowResult();

        ScoreSystem.Instance?.ShowResults();

        Debug.Log("Game Ended");
    }
}