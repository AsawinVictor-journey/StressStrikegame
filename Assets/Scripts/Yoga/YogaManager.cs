using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class YogaManager : MonoBehaviour
{
    [Header("Selected Pose")]
    public YogaPose selectedPose;
    
    [Header("Selected UI")]
    public Image descriptionImage;

    [Header("Countdown UI")]
    public CanvasGroup countdownGroup;
    public TMP_Text countdownText;

    [Header("Description UI")]
    public CanvasGroup descriptionGroup;
    public TMP_Text descriptionText;

    [Header("Timer UI")]
    public CanvasGroup timerGroup;
    public Image timerBar;
    public float holdTime = 30f;
    public float timer;

    [Header("Breathing")]
    public TMP_Text breathingText;
    public RectTransform breathingCircle;
    Coroutine breathingCoroutine;


    [Header("Instructor")]
    public Animator instructorAnimator;
    public int timeBeforeAnimation = 2;

    [Header("UI")]
    public UIFade uiFade;

    [Header("Score")]
    public YogaTracker yogaTracker;
    public float finalScore;

    [Header("Sun Feedback")]
    public Light sunLight;

    public Color calmColor = new Color32(244, 177, 131, 255);   // Peach
    public Color goodColor = new Color32(255, 209, 102, 255);   // Gold
    public Color perfectColor = new Color32(110, 198, 255, 255); // Sky Blue

    public float colorSmooth = 2f;

    [Header("Encouragement Feedback")]
    public CanvasGroup feedbackGroup;
    public TMP_Text feedbackText;

    [Tooltip("How many encouraging messages to show over the whole hold.")]
    public int feedbackMessageCount = 4;

    [Tooltip("How long a message stays fully visible before fading out.")]
    public float feedbackDisplayDuration = 3f;

    [Tooltip("Accuracy (0-100) above which a high-accuracy message is used instead.")]
    public float highAccuracyThreshold = 70f;

    public string[] encouragingMessages = new string[]
    {
        "You're doing great.",
        "Just breathe.",
        "Let your body settle.",
        "You're exactly where you need to be.",
        "Stay present in this moment.",
        "This is your time to relax.",
        "Let go of any tension.",
        "You're doing wonderfully.",
        "Feel your breath move through you.",
        "There's no rush, just be here.",
    };

    public string[] highAccuracyMessages = new string[]
    {
        "Beautiful posture.",
        "Nice and steady.",
        "You're glowing with calm.",
        "Wonderfully balanced.",
        "You're truly centered.",
    };

    Coroutine feedbackCoroutine;
    string lastFeedbackMessage = "";

    [Header("Result UI")]
    public CanvasGroup resultGroup;
    public TMP_Text resultBandText;
    public TMP_Text resultMessageText;
    public TMP_Text alignmentText;
    public TMP_Text steadinessText;

    [Header("Result Scoring")]
    [Range(0f, 1f)] public float alignmentWeight = 0.6f;
    [Range(0f, 1f)] public float steadinessWeight = 0.4f;

    public float centeredThreshold = 50f;
    public float balancedThreshold = 75f;
    public float radiantThreshold = 90f;

    public float finalAlignment;
    public float finalSteadiness;
    public float finalCalmScore;
    public string finalBand;

    public string[] groundingMessages = new string[]
    {
        "You showed up today.",
        "Stillness counts.",
        "You made space to breathe.",
    };

    public string[] centeredMessages = new string[]
    {
        "You found your rhythm.",
        "Time well spent.",
        "You settled in nicely.",
    };

    public string[] balancedMessages = new string[]
    {
        "Real balance today.",
        "Your calm really showed.",
        "A grounded session.",
    };

    public string[] radiantMessages = new string[]
    {
        "Beautifully calm.",
        "Truly present today.",
        "A wonderfully steady session.",
    };

    public void SelectPose(YogaPose pose)
    {
        selectedPose = pose;
        descriptionImage.sprite = selectedPose.icon;

        yogaTracker.SetTargetPose(
            selectedPose.targetArmRotation
        );
    }

    public void StartPose()
    {
        if(selectedPose == null)
            return;

        if (HeartRateYogaFlowManager.Instance != null)
        {
            HeartRateYogaFlowManager.Instance.SetState(HeartRateYogaFlowManager.FlowState.YogaGameplay);
        }

        StartCoroutine(StartPoseRoutine());
    }

    void Update()
    {
        UpdateSunColor();
    }

    void UpdateSunColor()
    {
        if (sunLight == null || yogaTracker == null)
            return;

        // Convert accuracy (0-100) to 0-1
        float t = Mathf.Clamp01(yogaTracker.accuracy / 100f);

        Color targetColor;

        // Peach -> Gold -> Sky Blue
        if (t < 0.5f)
        {
            targetColor = Color.Lerp(
                calmColor,
                goodColor,
                t * 2f
            );
        }
        else
        {
            targetColor = Color.Lerp(
                goodColor,
                perfectColor,
                (t - 0.5f) * 2f
            );
        }

        // Smoothly change the sun color
        sunLight.color = Color.Lerp(
            sunLight.color,
            targetColor,
            Time.deltaTime * colorSmooth
        );

        // Smoothly change brightness
        float targetIntensity = Mathf.Lerp(
            0.8f,
            1.4f,
            t
        );

        sunLight.intensity = Mathf.Lerp(
            sunLight.intensity,
            targetIntensity,
            Time.deltaTime * colorSmooth
        );
    }

        IEnumerator StartPoseRoutine()
    {   
        uiFade.ShowUI(countdownGroup);
        countdownText.text = "Sit tall, arms relaxed";
        yield return new WaitForSeconds(1);

        for(int i = 3; i > 0; i--)
        {
            countdownText.text = i.ToString();
            yield return new WaitForSeconds(1);
        }
        uiFade.HideUI(countdownGroup);

        // Recenter the glove to the player's current neutral pose. Pose targets
        // were captured relative to this neutral, so this is what makes the
        // accuracy line up regardless of how the BNO055's heading drifted since
        // last session. The player is still in neutral here (just finished the
        // "arms relaxed" countdown) before the instructor animation begins.
        if (yogaTracker != null)
            yogaTracker.Recenter();

        // Show description immediately
        uiFade.ShowUI(descriptionGroup);
        descriptionText.text = selectedPose.description;

        // Wait before doing anything
        yield return new WaitForSeconds(timeBeforeAnimation);

        // Play transition
        instructorAnimator.Play(selectedPose.transitionAnimation.name);

        // Wait until transition finishes
        yield return new WaitForSeconds(selectedPose.transitionAnimation.length);

        // Play the actual pose
        instructorAnimator.CrossFade(
            selectedPose.poseAnimation.name,
            0.3f
        );
        yogaTracker.StartTracking();

        uiFade.ShowUI(timerGroup);
        StartCoroutine(HoldPose());
        breathingCoroutine = StartCoroutine(BreathingRoutine());
        feedbackCoroutine = StartCoroutine(FeedbackRoutine());

        yield return new WaitForSeconds(2);
        uiFade.HideUI(descriptionGroup);

    }

        IEnumerator HoldPose()
    {
        timer = holdTime;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            timerBar.fillAmount = timer / holdTime;
            yield return null;
        }

        timerBar.fillAmount = 0f;
        StartCoroutine(CompleteRoutine());
    }

    IEnumerator BreathingRoutine()
    {
        while (true)
        {
            breathingText.text = "Inhale";
            yield return ScaleCircle(0.8f, 1.2f, 4f);

            breathingText.text = "Hold";
            yield return new WaitForSeconds(2f);

            breathingText.text = "Exhale";
            yield return ScaleCircle(1.2f, 0.8f, 4f);
        }
    }

    IEnumerator ScaleCircle(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float scale = Mathf.Lerp(from, to, elapsed / duration);
            breathingCircle.localScale = Vector3.one * scale;

            yield return null;
        }

        breathingCircle.localScale = Vector3.one * to;
    }

    IEnumerator FeedbackRoutine()
    {
        int messageCount = Mathf.Max(1, feedbackMessageCount);
        float interval = holdTime / messageCount;

        // Let the pose settle before the first message appears.
        yield return new WaitForSeconds(interval * 0.5f);

        for (int i = 0; i < messageCount; i++)
        {
            feedbackText.text = PickFeedbackMessage();

            yield return uiFade.FadeIn(feedbackGroup);
            yield return new WaitForSeconds(feedbackDisplayDuration);
            yield return uiFade.FadeOut(feedbackGroup);

            float remaining = interval - feedbackDisplayDuration;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
        }
    }

    string PickFeedbackMessage()
    {
        bool useHighAccuracy = yogaTracker != null
            && yogaTracker.accuracy >= highAccuracyThreshold
            && highAccuracyMessages.Length > 0;

        string[] pool = useHighAccuracy ? highAccuracyMessages : encouragingMessages;
        if (pool.Length == 0)
            return lastFeedbackMessage;

        string candidate = pool[Random.Range(0, pool.Length)];

        int guard = 0;
        while (candidate == lastFeedbackMessage && pool.Length > 1 && guard < 10)
        {
            candidate = pool[Random.Range(0, pool.Length)];
            guard++;
        }

        lastFeedbackMessage = candidate;
        return candidate;
    }

    IEnumerator CompleteRoutine()
    {
        yogaTracker.StopTracking();

        if (breathingCoroutine != null)
        {
            StopCoroutine(breathingCoroutine);
        }

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }
        lastFeedbackMessage = "";

        breathingCircle.localScale = Vector3.one;
        instructorAnimator.CrossFade(
            "rig|Idle",
            0.3f
        );

        uiFade.HideUI(descriptionGroup);
        uiFade.HideUI(timerGroup);
        uiFade.HideUI(feedbackGroup);

        CalculateResult();
        if (HeartRateYogaFlowManager.Instance != null)
        {
            HeartRateYogaFlowManager.Instance.SetState(HeartRateYogaFlowManager.FlowState.PostGameCalibration);
        }
        else
        {
            ShowResult();
        }

        yield return null;
    }

    void CalculateResult()
    {
        finalAlignment = yogaTracker.alignment;
        finalSteadiness = yogaTracker.steadiness;

        finalCalmScore =
            finalAlignment * alignmentWeight +
            finalSteadiness * steadinessWeight;

        finalBand = GetResultBand(finalCalmScore);
        finalScore = finalCalmScore;
    }

    string GetResultBand(float score)
    {
        if (score >= radiantThreshold) return "Radiant";
        if (score >= balancedThreshold) return "Balanced";
        if (score >= centeredThreshold) return "Centered";
        return "Grounding";
    }

    string PickResultMessage(string band)
    {
        string[] pool = band switch
        {
            "Radiant" => radiantMessages,
            "Balanced" => balancedMessages,
            "Centered" => centeredMessages,
            _ => groundingMessages,
        };

        if (pool.Length == 0)
            return "";

        return pool[Random.Range(0, pool.Length)];
    }

    void ShowResult()
    {
        if (resultBandText != null)
            resultBandText.text = finalBand;

        if (resultMessageText != null)
            resultMessageText.text = PickResultMessage(finalBand);

        if (alignmentText != null)
            alignmentText.text = Mathf.RoundToInt(finalAlignment) + "%";

        if (steadinessText != null)
            steadinessText.text = Mathf.RoundToInt(finalSteadiness) + "%";

        if (resultGroup != null)
            uiFade.ShowUI(resultGroup);
    }
}