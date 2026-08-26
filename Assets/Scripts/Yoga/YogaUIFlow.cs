using UnityEngine;
using TMPro;

/// <summary>
/// Presentation layer for the Yoga pose panel. Owns NO flow decisions: it reads
/// YogaManager.CalibrationState and renders it (step label, instruction copy,
/// which calibrate button is offered). The rule for what a pose *requires* --
/// open only, or open + mid -- lives in YogaManager.
///
/// Start button visibility stays on YogaManager: "is this pose startable" is a
/// flow gate, not presentation, and StartPose() hard-refuses regardless. This
/// component deliberately does not touch it, so there is exactly one owner.
/// </summary>
public class YogaUIFlow : MonoBehaviour
{
    public UIFade fadeUI;

    public CanvasGroup poseSelection;
    public CanvasGroup description;

    [Header("Calibration Step Presentation")]
    public YogaManager yogaManager;

    [Tooltip("Optional. Shows the current step (\"Set Starting Pose\", \"Set Mid Pose\", \"You're Ready!\") " +
             "and what to do. Leave empty to drive button visibility only -- the step copy is then silent.")]
    public TMP_Text instructionText;

    [Header("Buttons (Start is owned by YogaManager)")]
    public GameObject demoButton;
    public GameObject calibrateButton;
    public GameObject calibrateMidButton;

    [Tooltip("Shown ONLY during Demo, replacing the whole normal row. Returns to the pose card.")]
    public GameObject backButton;

    private void OnEnable()
    {
        if (yogaManager != null)
        {
            yogaManager.CalibrationStateChanged += Render;
            yogaManager.DemoStateChanged += OnDemoStateChanged;
            Render(yogaManager.calibrationState); // paint the current state, not whatever the scene was saved showing
        }
    }

    private void OnDisable()
    {
        if (yogaManager != null)
        {
            yogaManager.CalibrationStateChanged -= Render;
            yogaManager.DemoStateChanged -= OnDemoStateChanged;
        }
    }

    public void SelectPose()
    {
        fadeUI.SwitchUI(poseSelection, description);
    }

    public void StartPose()
    {
        fadeUI.HideUI(description);
    }

    /// <summary>
    /// Demo takes over the whole row: every normal button hides and only Back is
    /// offered, so there is no way to wander into Calibrate or Start from behind
    /// the demo card. Leaving it re-renders whatever calibration step we were on.
    /// </summary>
    private void OnDemoStateChanged(bool demoActive)
    {
        if (demoActive)
        {
            Show(demoButton, false);
            Show(calibrateButton, false);
            Show(calibrateMidButton, false);
            Show(backButton, true);
        }
        else
        {
            Show(backButton, false);
            if (yogaManager != null) Render(yogaManager.calibrationState);
        }
    }

    public void Render(YogaManager.CalibrationState state)
    {
        // Demo owns the row while it is up; a calibration-state change arriving
        // mid-demo must not quietly put Calibrate/Start back on top of it.
        if (yogaManager != null && yogaManager.isDemoPlaying) return;

        Show(backButton, false);

        bool hasMid = yogaManager != null
                   && yogaManager.selectedPose != null
                   && yogaManager.selectedPose.HasGradableMidPose;

        switch (state)
        {
            case YogaManager.CalibrationState.AwaitingOpen:
                SetText(hasMid ? "Set Starting Pose" : "Set Your Pose",
                        hasMid ? "Get into the starting position shown in the demo."
                               : "Get into the pose shown in the demo.");
                Show(demoButton, true);
                Show(calibrateButton, true);
                Show(calibrateMidButton, false);
                break;

            case YogaManager.CalibrationState.AwaitingMid:
                SetText("Set Mid Pose", "Move into the middle position shown in the demo.");
                Show(demoButton, true);
                Show(calibrateButton, false);
                Show(calibrateMidButton, true);
                break;

            case YogaManager.CalibrationState.Complete:
                SetText("You're Ready!", "Press Start to begin, or Calibrate again to redo your pose.");
                Show(demoButton, true);
                // Calibrate stays offered so a bad capture can be redone without
                // reselecting the pose. Re-running it lands straight back on
                // Complete, because the mid calibration is already saved.
                Show(calibrateButton, true);
                Show(calibrateMidButton, false);
                break;
        }
    }

    private void SetText(string heading, string body)
    {
        if (instructionText == null) return;
        instructionText.text = heading + "\n" + body;
    }

    // Only touches the object when the state actually differs -- ButtonRowLayout
    // polls activeSelf every Update and repacks the row on any change, so
    // redundant SetActive calls would make it re-lay-out for nothing.
    private static void Show(GameObject go, bool visible)
    {
        if (go != null && go.activeSelf != visible)
            go.SetActive(visible);
    }
}
