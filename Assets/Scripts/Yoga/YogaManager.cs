using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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
    Coroutine poseLoopCoroutine;

    [Tooltip("How many times the instructor cycles out to the mid pose and back.")]
    public int midPoseCycles = 3;

    [Header("Cycle Timing")]
    // Tuned against the reference footage (yoga spread.mp4), which runs a ~9.3s
    // cycle spending ~5.8s spread and ~3.5s closed. Driving these by hand
    // instead of by clip length is what keeps the instructor in step with the
    // video -- the raw clips are ~2s each, which reads far too fast.
    // The hold values are shorter than the on-screen spans because the crossfade
    // either side bleeds into them; these are the numbers that measured correct.
    [Tooltip("Seconds held in the open pose before closing.")]
    public float openHoldDuration = 5.27f;

    [Tooltip("Seconds held in the mid (closed) pose before unwinding.")]
    public float closedHoldDuration = 1.81f;

    [Tooltip("Seconds to travel from the open pose to the closed pose.")]
    public float toClosedDuration = 1.17f;

    [Tooltip("Seconds to travel back from the closed pose to the open pose.")]
    public float toOpenDuration = 1.05f;

    [Tooltip("Seconds of blend between the pose clips and the transition clips. " +
             "Too short and the instructor snaps out of the held pose.")]
    public float blendDuration = 0.35f;


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
    public TMP_Text alignmentText;
    public TMP_Text steadinessText;
    public TMP_Text resultScoreText;
    public TMP_Text resultAccuracyText;
    public TMP_Text resultCoinsEarnedText;
    public TMP_Text resultLevelText;
    public Image resultLevelBarFill;
    public GameObject levelUpBanner;

    [Header("Result Screen Flow")]
    [Tooltip("How long the result panel stays up before fading into the mode menu.")]
    public float resultScreenDuration = 7f;
    public string menuSceneName = "Yoga Menu";

    // Yoga has no numeric "Score" — finalCalmScore (0-100, alignment/steadiness weighted) is
    // used as both the accuracy-based Score-equivalent for the coin formula and, divided by
    // 100, as performance_normalized for the XP formula.
    //
    // "Accuracy" on the result screen is finalAlignment (yogaTracker.alignment, the average
    // pose-match reading over the whole hold) — a genuinely separate input that gets blended
    // with finalSteadiness into finalCalmScore, NOT the same number as the Score. Do not wire
    // this to yogaTracker.accuracy: that field is the live, continuously-smoothed instantaneous
    // reading (it just freezes wherever it was the instant tracking stopped), not a real
    // session metric, and reads close enough to finalAlignment to look like a fabricated
    // duplicate without actually being the correct averaged value.
    //
    // There's also no per-hit "intensity" concept in this mode (no punches/velocity to sum),
    // so intensityUnits is fixed at 0 for the coin formula here rather than guessing at a
    // stand-in signal.
    const float YogaIntensityUnits = 0f;

    [Header("Result Scoring")]
    [Range(0f, 1f)] public float alignmentWeight = 0.6f;
    [Range(0f, 1f)] public float steadinessWeight = 0.4f;

    public float finalAlignment;
    public float finalSteadiness;
    public float finalCalmScore;

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
        countdownText.text = "Get Ready!";
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

        // Cycle the instructor between the pose and its mid pose for as long as
        // HoldPose() keeps the timer running.
        poseLoopCoroutine = StartCoroutine(PoseLoopRoutine());

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

    // One full out-and-back cycle at the current inspector settings.
    public float CycleDuration
    {
        get { return openHoldDuration + toClosedDuration + closedHoldDuration + toOpenDuration; }
    }

    // A pose may carry its own rhythm; anything it leaves at 0 falls back to the
    // values above. Poses move at very different speeds, so one global tempo
    // cannot serve all of them.
    static float Resolve(float perPose, float fallback)
    {
        return perPose > 0f ? perPose : fallback;
    }

    float OpenHold { get { return Resolve(selectedPose.openHoldDuration, openHoldDuration); } }
    float ClosedHold { get { return Resolve(selectedPose.closedHoldDuration, closedHoldDuration); } }
    float ToClosed { get { return Resolve(selectedPose.toClosedDuration, toClosedDuration); } }
    float ToOpen { get { return Resolve(selectedPose.toOpenDuration, toOpenDuration); } }
    float Blend { get { return Resolve(selectedPose.blendDuration, blendDuration); } }
    int Cycles { get { return selectedPose.midPoseCycles > 0 ? selectedPose.midPoseCycles : midPoseCycles; } }

    void OnValidate()
    {
        // A zero or negative duration would divide by ~0 in PlayForward and
        // spin forever in PlayReversed.
        openHoldDuration = Mathf.Max(0.05f, openHoldDuration);
        closedHoldDuration = Mathf.Max(0.05f, closedHoldDuration);
        toClosedDuration = Mathf.Max(0.05f, toClosedDuration);
        toOpenDuration = Mathf.Max(0.05f, toOpenDuration);
        midPoseCycles = Mathf.Max(0, midPoseCycles);

        // The hold timer ends the pose no matter where the cycle has got to, so
        // warn rather than let the last rep silently get cut off.
        float needed = CycleDuration * midPoseCycles;
        if (needed > holdTime)
            Debug.LogWarning(
                "[YogaManager] " + midPoseCycles + " cycles need " + needed.ToString("F1") +
                "s but holdTime is " + holdTime.ToString("F1") + "s - the last rep will be cut short. " +
                "Raise holdTime to at least " + Mathf.Ceil(needed) + " or shorten the cycle.", this);
    }

    IEnumerator PoseLoopRoutine()
    {
        // No mid pose means the pose clip just loops on its own — nothing to drive.
        if (selectedPose.MidPoseAnimation == null)
            yield break;

        // Played forwards to reach the mid pose, then backwards to come home.
        AnimationClip midTransition = selectedPose.reverseTransitionAnimation;

        // The pose clips loop, so holding longer than the clip just keeps them
        // breathing rather than freezing on the last frame.
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            yield return new WaitForSeconds(OpenHold);

            if (midTransition != null)
                yield return StartCoroutine(PlayForward(midTransition, ToClosed));

            instructorAnimator.CrossFadeInFixedTime(selectedPose.MidPoseAnimation.name, Blend);
            yield return new WaitForSeconds(ClosedHold);

            // Same clip in reverse instead of a separately authored return clip.
            if (midTransition != null)
                yield return StartCoroutine(
                    PlayReversed(midTransition, ToOpen, selectedPose.poseAnimation.name));
            else
                instructorAnimator.CrossFadeInFixedTime(selectedPose.poseAnimation.name, Blend);
        }

        poseLoopCoroutine = null;
    }

    // Plays a state forwards, stretched or compressed to last exactly
    // 'duration' seconds regardless of how long the clip itself is.
    IEnumerator PlayForward(AnimationClip clip, float duration)
    {
        float previousSpeed = instructorAnimator.speed;

        instructorAnimator.speed = clip.length / Mathf.Max(duration, 0.01f);

        // CrossFadeInFixedTime, not CrossFade: CrossFade's duration is normalized
        // to the destination clip and then scaled by the speed set above, which
        // collapsed the blend to ~0.18s and made the instructor snap out of the
        // held pose. A fixed-time blend stays the length it says it is.
        instructorAnimator.CrossFadeInFixedTime(clip.name, Mathf.Min(Blend, duration * 0.5f));
        yield return new WaitForSeconds(duration);

        instructorAnimator.speed = previousSpeed;
    }

    // Plays a state backwards by scrubbing normalizedTime from 1 down towards 0
    // over 'duration' seconds, then blends into 'blendToState' for the tail.
    //
    // Two things this works around:
    //  - Setting instructorAnimator.speed = -1f does nothing useful: Unity clamps
    //    a negative Animator.speed to 0, freezing the pose on its last frame for
    //    the length of the clip instead of unwinding it.
    //  - The transition clip's first frame is not identical to the held pose
    //    (measured ~0.19m narrower). Scrubbing all the way to 0 therefore made
    //    the arms reach open, retract, then pop back out. Handing the last
    //    'blend' seconds over to a crossfade keeps the motion monotonic.
    IEnumerator PlayReversed(AnimationClip clip, float duration, string blendToState)
    {
        float previousSpeed = instructorAnimator.speed;

        // The animator must not advance on its own while we drive the time.
        instructorAnimator.speed = 0f;

        float blend = Mathf.Min(Blend, duration * 0.4f);
        for (float remaining = duration; remaining > blend; remaining -= Time.deltaTime)
        {
            instructorAnimator.Play(clip.name, 0, Mathf.Clamp01(remaining / duration));
            yield return null;
        }

        instructorAnimator.speed = previousSpeed;
        instructorAnimator.CrossFadeInFixedTime(blendToState, blend);
        yield return new WaitForSeconds(blend);
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

        if (poseLoopCoroutine != null)
        {
            StopCoroutine(poseLoopCoroutine);
            poseLoopCoroutine = null;
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

    PlayerProgression.SessionRewardResult sessionReward;

    void CalculateResult()
    {
        finalAlignment = yogaTracker.alignment;
        finalSteadiness = yogaTracker.steadiness;

        finalCalmScore =
            finalAlignment * alignmentWeight +
            finalSteadiness * steadinessWeight;

        finalScore = finalCalmScore;

        sessionReward = default;
        if (PlayerProgression.Instance != null)
        {
            float performanceNormalized = finalCalmScore / 100f;
            float durationMinutes = holdTime / 60f;

            int xp = PlayerProgression.CalculateXP(performanceNormalized, durationMinutes);
            int coins = PlayerProgression.Instance.CalculateCoins(
                Mathf.RoundToInt(finalCalmScore), YogaIntensityUnits);

            sessionReward = PlayerProgression.Instance.AddSessionResult(xp, coins);
        }
    }

    public void ShowResult()
    {
        if (alignmentText != null)
            alignmentText.text = Mathf.RoundToInt(finalAlignment) + "%";

        if (steadinessText != null)
            steadinessText.text = Mathf.RoundToInt(finalSteadiness) + "%";

        if (resultScoreText != null)
            resultScoreText.text = Mathf.RoundToInt(finalScore).ToString();

        if (resultAccuracyText != null)
            resultAccuracyText.text = Mathf.RoundToInt(finalAlignment) + "%";

        if (resultCoinsEarnedText != null)
            resultCoinsEarnedText.text = "+" + sessionReward.CoinsAwarded;

        if (resultLevelText != null)
            resultLevelText.text = sessionReward.Level.ToString();

        if (resultLevelBarFill != null)
        {
            resultLevelBarFill.fillAmount = sessionReward.PreviousLevelProgress01;
            StartCoroutine(AnimateLevelBar(resultLevelBarFill,
                sessionReward.PreviousLevelProgress01, sessionReward.LevelProgress01, sessionReward.LeveledUp));
        }

        if (levelUpBanner != null)
            levelUpBanner.SetActive(sessionReward.LeveledUp);

        if (resultGroup != null)
            uiFade.ShowUI(resultGroup);

        StartCoroutine(ReturnToMenu());
    }

    // Animates the level bar filling up when the result panel appears, rather than snapping
    // straight to the post-session value. On a level-up it fills to full, snaps back to empty,
    // then continues filling into the new level, so a level-up reads as a level-up rather than
    // the bar just landing on a lower-looking number.
    IEnumerator AnimateLevelBar(Image bar, float fromProgress, float toProgress, bool leveledUp)
    {
        const float fillDuration = 0.5f;

        if (leveledUp)
        {
            yield return LerpFill(bar, fromProgress, 1f, fillDuration);
            bar.fillAmount = 0f;
            yield return LerpFill(bar, 0f, toProgress, fillDuration);
        }
        else
        {
            yield return LerpFill(bar, fromProgress, toProgress, fillDuration);
        }
    }

    IEnumerator LerpFill(Image bar, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            bar.fillAmount = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        bar.fillAmount = to;
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