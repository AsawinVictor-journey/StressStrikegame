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

        StartCoroutine(StartPoseRoutine());
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

    IEnumerator CompleteRoutine()
    {   
        yogaTracker.StopTracking();
        finalScore = yogaTracker.accuracy;

        if (breathingCoroutine != null)
        {
            StopCoroutine(breathingCoroutine);
        }

        breathingCircle.localScale = Vector3.one;
        instructorAnimator.CrossFade(
            "rig|Idle",
            0.3f
        );

        uiFade.HideUI(descriptionGroup);
        uiFade.HideUI(timerGroup);

        yield return null;
    }
}