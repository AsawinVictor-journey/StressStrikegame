using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MeditationSessionManager : MonoBehaviour
{
    public enum SessionState { Waiting, Countdown, Playing, Finished }
    public SessionState currentState = SessionState.Waiting;

    [Header("UI References")]
    public MeditationHUD meditationHUD;
    public ScoreManager scoreManager; // Assume existing ScoreManager handles general scoring/coins

    [Header("Settings")]
    public float targetSpawnInterval = 2f;
    public List<TargetHit> targetSequence; // Drag targets from scene here

    private int currentTargetIndex = 0;
    private int correctHits = 0;
    private int totalTargets = 0;

    void Start()
    {
        if(targetSequence != null)
        {
            totalTargets = targetSequence.Count;
            // Ensure all are hidden at start
            foreach(var target in targetSequence)
            {
                target.gameObject.SetActive(false);
                target.OnTargetResolved += HandleTargetResolved;
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
        
        // 3, 2, 1, Go!
        if (meditationHUD != null)
        {
            yield return meditationHUD.PlayCountdown();
        }
        else
        {
            yield return new WaitForSeconds(3f); // Fallback
        }

        currentState = SessionState.Playing;
        currentTargetIndex = 0;
        correctHits = 0;

        while(currentTargetIndex < targetSequence.Count)
        {
            TargetHit nextTarget = targetSequence[currentTargetIndex];
            nextTarget.gameObject.SetActive(true);
            nextTarget.ActivateTarget();

            yield return new WaitForSeconds(targetSpawnInterval);
            currentTargetIndex++;
        }

        // Wait a bit for the last target to resolve if missed
        yield return new WaitForSeconds(2f);
        
        FinishSession();
    }

    private void HandleTargetResolved(bool isHit)
    {
        if (isHit) correctHits++;
        
        if (meditationHUD != null)
        {
            float accuracy = (float)correctHits / totalTargets * 100f;
            meditationHUD.UpdateAccuracy(accuracy);
        }
    }

    private void FinishSession()
    {
        currentState = SessionState.Finished;
        
        // Calculate currency based on accuracy
        float accuracy = (totalTargets > 0) ? ((float)correctHits / totalTargets) : 0f;
        int coinsEarned = Mathf.RoundToInt(accuracy * 100f); // e.g., 100% = 100 coins

        // Hook into existing CoinManager or ScoreManager here
        // CoinManager.Instance.AddCoins(coinsEarned);
        Debug.Log($"Session Finished! Accuracy: {accuracy*100}%. Earned {coinsEarned} coins.");
        
        if (meditationHUD != null) meditationHUD.ShowEndScreen(accuracy * 100f, coinsEarned);
    }
}
