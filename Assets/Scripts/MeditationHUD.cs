using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class MeditationHUD : MonoBehaviour
{
    [Header("UI Elements")]
    public Button startButton;
    public TextMeshProUGUI countdownText;
    public TextMeshProUGUI heartRateText;
    public TextMeshProUGUI accuracyText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI gradeText;
    public GameObject endScreenPanel;
    public TextMeshProUGUI endScreenText;

    [Header("Heart Rate Sim")]
    public float minBPM = 60f;
    public float maxBPM = 85f;
    private float currentBPM;
    
    private Coroutine gradeCoroutine;

    void Start()
    {
        if(countdownText != null) countdownText.gameObject.SetActive(false);
        if(endScreenPanel != null) endScreenPanel.SetActive(false);
        if(gradeText != null) gradeText.gameObject.SetActive(false);
        if(comboText != null) comboText.text = "Combo: 0";
        
        currentBPM = Random.Range(minBPM, maxBPM);
        StartCoroutine(SimulateHeartRate());

        if (startButton != null)
        {
            startButton.onClick.AddListener(() => {
                startButton.gameObject.SetActive(false);
                var manager = Object.FindAnyObjectByType<MeditationSessionManager>();
                if (manager != null) manager.StartSession();
            });
        }
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
            accuracyText.text = $"Accuracy: {accuracyPercentage:F1}%";
    }
    
    public void UpdateCombo(int combo)
    {
        if(comboText != null)
            comboText.text = $"Combo: {combo}";
    }

    public void ShowGrade(bool isPerfect)
    {
        if(gradeText == null) return;
        
        if(gradeCoroutine != null) StopCoroutine(gradeCoroutine);
        gradeCoroutine = StartCoroutine(GradePopup(isPerfect));
    }
    
    private IEnumerator GradePopup(bool isPerfect)
    {
        gradeText.gameObject.SetActive(true);
        gradeText.text = isPerfect ? "Perfect!" : "Miss!";
        gradeText.color = isPerfect ? Color.green : Color.red;
        
        yield return new WaitForSeconds(1.5f);
        gradeText.gameObject.SetActive(false);
    }

    public void ShowEndScreen(float accuracy, int coins, int maxCombo)
    {
        if(endScreenPanel != null)
        {
            endScreenPanel.SetActive(true);
        }
        if(endScreenText != null)
        {
            endScreenText.text = $"Finished!\nAccuracy: {accuracy:F0}%\nMax Combo: {maxCombo}\nCoins: +{coins}";
        }
    }

    private IEnumerator SimulateHeartRate()
    {
        while(true)
        {
            float variation = Random.Range(-2f, 2f);
            currentBPM = Mathf.Clamp(currentBPM + variation, minBPM, maxBPM);

            if(heartRateText != null)
                heartRateText.text = $"HR: {Mathf.RoundToInt(currentBPM)} BPM";

            yield return new WaitForSeconds(1f);
        }
    }
}
