using UnityEngine;
using TMPro;
using System.Collections;

public class MeditationHUD : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI countdownText;
    public TextMeshProUGUI heartRateText;
    public TextMeshProUGUI accuracyText;
    public GameObject endScreenPanel;

    [Header("Heart Rate Sim")]
    public float minBPM = 60f;
    public float maxBPM = 85f;
    private float currentBPM;

    void Start()
    {
        if(countdownText != null) countdownText.gameObject.SetActive(false);
        if(endScreenPanel != null) endScreenPanel.SetActive(false);
        currentBPM = Random.Range(minBPM, maxBPM);
        StartCoroutine(SimulateHeartRate());
    }

    public IEnumerator PlayCountdown()
    {
        if (countdownText == null) yield break;

        countdownText.gameObject.SetActive(true);
        
        countdownText.text = "3";
        yield return new WaitForSeconds(1f);
        
        countdownText.text = "2";
        yield return new WaitForSeconds(1f);
        
        countdownText.text = "1";
        yield return new WaitForSeconds(1f);

        countdownText.text = "GO!";
        yield return new WaitForSeconds(1f);
        
        countdownText.gameObject.SetActive(false);
    }

    public void UpdateAccuracy(float accuracyPercentage)
    {
        if(accuracyText != null)
        {
            accuracyText.text = $"Accuracy: {accuracyPercentage:F1}%";
        }
    }

    public void ShowEndScreen(float accuracy, int coins)
    {
        if(endScreenPanel != null)
        {
            endScreenPanel.SetActive(true);
            // Assuming endScreenPanel has children text fields to display these
        }
        if(countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = $"Finished!\nAccuracy: {accuracy:F0}%\nCoins: +{coins}";
        }
    }

    private IEnumerator SimulateHeartRate()
    {
        while(true)
        {
            // Simulate natural HR fluctuation
            float variation = Random.Range(-2f, 2f);
            currentBPM = Mathf.Clamp(currentBPM + variation, minBPM, maxBPM);

            if(heartRateText != null)
            {
                heartRateText.text = $"HR: {Mathf.RoundToInt(currentBPM)} BPM";
            }

            yield return new WaitForSeconds(1f);
        }
    }
}
