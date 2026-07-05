using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MeditationSessionManager : MonoBehaviour
{
    public enum SessionState { Waiting, Countdown, Playing, Finished }
    public SessionState currentState = SessionState.Waiting;

    [Header("UI References")]
    public MeditationHUD meditationHUD;
    public ScoreManager scoreManager; 

    [Header("Settings")]
    public float targetSpawnInterval = 2f;
    
    // We can use a combined list of GameObjects that are either TargetHit or ObstacleWall
    public List<GameObject> spawnSequence; 

    private int currentIndex = 0;
    private int correctHits = 0;
    private int totalEvents = 0;
    
    // Combo
    private int currentCombo = 0;
    private int maxCombo = 0;

    void Start()
    {
        if(spawnSequence != null)
        {
            totalEvents = spawnSequence.Count;
            foreach(var obj in spawnSequence)
            {
                if (obj == null) continue;
                obj.SetActive(false);
                
                var target = obj.GetComponent<TargetHit>();
                if(target != null) target.OnTargetResolved += HandleEventResolved;
                
                var wall = obj.GetComponent<ObstacleWall>();
                if(wall != null) wall.OnWallResolved += HandleEventResolved;
            }
        }
    }

    public void StartSession()
    {
        if (currentState != SessionState.Waiting) return;
        StartCoroutine(SessionRoutine());
    }

    private IEnumerator SessionRoutine()
    {
        currentState = SessionState.Countdown;
        
        if (meditationHUD != null)
        {
            yield return meditationHUD.PlayCountdown();
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }

        currentState = SessionState.Playing;
        currentIndex = 0;
        correctHits = 0;
        currentCombo = 0;

        while(currentIndex < spawnSequence.Count)
        {
            GameObject nextObj = spawnSequence[currentIndex];
            if (nextObj != null)
            {
                nextObj.SetActive(true);
                
                var target = nextObj.GetComponent<TargetHit>();
                if(target != null) target.ActivateTarget();
                
                var wall = nextObj.GetComponent<ObstacleWall>();
                if(wall != null) wall.ActivateObstacle();
            }

            yield return new WaitForSeconds(targetSpawnInterval);
            currentIndex++;
        }

        yield return new WaitForSeconds(3f);
        FinishSession();
    }

    private void HandleEventResolved(bool isSuccess)
    {
        if (isSuccess)
        {
            correctHits++;
            currentCombo++;
            if (currentCombo > maxCombo) maxCombo = currentCombo;
        }
        else
        {
            currentCombo = 0; // Combo Miss!
        }
        
        if (meditationHUD != null)
        {
            float accuracy = (totalEvents > 0) ? ((float)correctHits / totalEvents) * 100f : 0f;
            meditationHUD.UpdateAccuracy(accuracy);
            meditationHUD.UpdateCombo(currentCombo);
            meditationHUD.ShowGrade(isSuccess);
        }
    }

    private void FinishSession()
    {
        currentState = SessionState.Finished;
        
        float accuracy = (totalEvents > 0) ? ((float)correctHits / totalEvents) : 0f;
        int coinsEarned = Mathf.RoundToInt(accuracy * 100f) + (maxCombo * 2); 

        Debug.Log($"Session Finished! Accuracy: {accuracy*100}%. Max Combo: {maxCombo}. Earned {coinsEarned} coins.");
        
        if (meditationHUD != null) meditationHUD.ShowEndScreen(accuracy * 100f, coinsEarned, maxCombo);
    }
}
