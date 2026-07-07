using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System;

public class FeedbackManager : MonoBehaviour
{
    [SerializeField] private InputField workHappeningUI;
    [SerializeField] private Slider stressUI;
    [SerializeField] private InputField ventUI;
    [SerializeField] private InputField jobUI;
    [SerializeField] private TMP_Dropdown blockerUI;
    [SerializeField] private Button submitBtn;
    [SerializeField] private Button skipBtn;
    [SerializeField] private OpenAIService aiService;
    [SerializeField] private CanvasGroup loadingIndicator;
    [SerializeField] private string sceneToLoadAfter;

    private FeedbackRecord currentRecord;
    private AIAdvice currentAdvice;

    private void Start()
    {
        if (submitBtn != null) submitBtn.onClick.AddListener(OnSubmit);
        if (skipBtn != null) skipBtn.onClick.AddListener(OnSkip);

        if (blockerUI != null)
        {
            blockerUI.ClearOptions();
            blockerUI.AddOptions(new System.Collections.Generic.List<string>
            {
                "None",
                "Lack of Motivation",
                "No Time",
                "Injury"
            });
        }

        if (loadingIndicator != null) loadingIndicator.gameObject.SetActive(false);
    }

    private void OnSubmit()
    {
        if (!ValidateForm()) return;

        currentRecord = new FeedbackRecord
        {
            playerId = SystemInfo.deviceUniqueIdentifier,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            workHappening = workHappeningUI.text,
            workStress = (int)stressUI.value,
            ventText = ventUI.text,
            jobType = jobUI.text,
            blocker = (ExerciseBlocker)blockerUI.value,
            caloriesBurned = 0f,
            hrv = 0f
        };

        SetUIInteractable(false);
        if (loadingIndicator != null) loadingIndicator.gameObject.SetActive(true);

        StartCoroutine(SendToAI(currentRecord));
    }

    private void OnSkip()
    {
        LoadTargetScene();
    }

    private IEnumerator SendToAI(FeedbackRecord rec)
    {
        string prompt = BuildPrompt(rec);

        yield return aiService.StartCoroutine(aiService.Ask(prompt,
            (json) => OnAISuccess(json, rec),
            (error) => OnAIError(error)
        ));
    }

    private void OnAISuccess(string json, FeedbackRecord rec)
    {
        try
        {
            currentAdvice = JsonUtility.FromJson<AIAdvice>(json);
            SaveRecord(rec);
            ShowOutline(currentAdvice);
            LoadTargetScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse AI response: {e}");
            OnAIError(e.Message);
        }
    }

    private void OnAIError(string error)
    {
        Debug.LogError($"AI Service Error: {error}");
        SetUIInteractable(true);
        if (loadingIndicator != null) loadingIndicator.gameObject.SetActive(false);
    }

    private string BuildPrompt(FeedbackRecord r)
    {
        return $@"You are a health coach for a stress relief boxing game. The player has provided:
- Work situation: {r.workHappening}
- Stress level: {r.workStress}/10
- What to vent about: {r.ventText}
- Job type: {r.jobType}
- Exercise blocker: {r.blocker}

Respond with ONLY valid JSON (no markdown, no explanation, just the JSON object):
{{""focus"": ""One motivational sentence for their session"", ""outline"": [""activity1"", ""activity2""]}}

The outline array should have 1-3 activities matching their stress level and blocker (e.g., ""heavy_bag"" for high stress, ""speed_bag"" for motivation issues).";
    }

    private void ShowOutline(AIAdvice advice)
    {
        Debug.Log($"AI Recommendation - Focus: {advice.focus}");
        if (advice.outline != null)
        {
            foreach (var outline in advice.outline)
            {
                Debug.Log($"Highlight: {outline}");
            }
        }
    }

    private void SaveRecord(FeedbackRecord r)
    {
        string path = Application.persistentDataPath + "/feedback.json";
        string json = JsonUtility.ToJson(r);
        System.IO.File.AppendAllText(path, json + "\n");
        Debug.Log($"Feedback saved to {path}");
    }

    private void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(sceneToLoadAfter))
        {
            SceneManager.LoadScene(sceneToLoadAfter);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(workHappeningUI.text))
        {
            ShowError("Please tell us what happened at work today.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(jobUI.text))
        {
            ShowError("Please tell us your job type.");
            return false;
        }
        return true;
    }

    private void ShowError(string message)
    {
        Debug.LogWarning(message);
    }

    private void SetUIInteractable(bool interactable)
    {
        if (submitBtn != null) submitBtn.interactable = interactable;
        if (skipBtn != null) skipBtn.interactable = interactable;
        if (workHappeningUI != null) workHappeningUI.interactable = interactable;
        if (ventUI != null) ventUI.interactable = interactable;
        if (jobUI != null) jobUI.interactable = interactable;
    }
}

[System.Serializable]
public class AIAdvice
{
    public string focus;
    public string[] outline;
}
