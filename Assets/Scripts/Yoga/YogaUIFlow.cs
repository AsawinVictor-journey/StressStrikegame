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

    [Tooltip("Main screen only. Wire onClick to YogaManager.BeginSetup().")]
    public GameObject nextButton;

    [Tooltip("The single \"Set Pose\" button. Wire onClick to YogaManager.SetPoseButtonClicked(), " +
             "which picks the open or mid capture from the current step.")]
    public GameObject calibrateButton;

    [Tooltip("Legacy separate Calibrate-Mid button. The mid capture now chains automatically " +
             "off the single Set Pose button, so this is never shown -- kept only so an existing " +
             "scene reference does not break, and force-hidden on every render.")]
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
            Show(nextButton, false);
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

        // Never offered any more: the mid capture chains automatically off the single
        // Set Pose button, so a separate Calibrate-Mid would be a second way to do the
        // same thing -- and a way to restart the countdown on top of a running chain.
        Show(calibrateMidButton, false);

        // Back belongs to every setup step (it returns to the Demo/Next card) but not to
        // the main screen, where there is nothing behind it to go back to.
        Show(backButton, state != YogaManager.CalibrationState.Idle);

        // (The open-step copy used to branch on whether the pose has a mid capture. That
        // copy is gone -- the card art carries it -- so there is nothing left to branch on.
        // If live copy is ever reintroduced here, read "does this pose need a mid" from
        // YogaManager.SelectedPoseNeedsMid, NOT selectedPose.HasGradableMidPose: those two
        // can disagree, and the copy must promise exactly the captures the player gets.)

        switch (state)
        {
            // Main pose screen. The pose's own description card carries its copy baked
            // in, so the live text is cleared rather than left showing a stale step.
            case YogaManager.CalibrationState.Idle:
                SetText(null, null);
                Show(demoButton, true);
                Show(nextButton, true);
                Show(calibrateButton, false);
                break;

            // One button either way. Only the instruction line differs, so a pose with a
            // mid capture warns that this first one is the STARTING pose.
            // No live copy: the setup card's own ART already carries this instruction
            // ("Get into the pose shown in the demo..."), baked into the Figma sprite.
            // Drawing our own version on top produced two overlapping paragraphs.
            case YogaManager.CalibrationState.AwaitingOpen:
                SetText(null, null);
                Show(demoButton, false);
                Show(nextButton, false);
                Show(calibrateButton, true);
                break;

            // Same title, same text area, no new button: the mid capture is part of the
            // same Set Pose press and is already counting down. Offering a button here
            // would read as a second step AND could restart the running chain.
            // The ONLY step that draws live copy. Nothing in the card art covers the mid
            // capture, so without this the player is given no cue to move at all. No
            // heading -- a bare line reads as a prompt rather than competing with the
            // card's own baked-in title.
            case YogaManager.CalibrationState.AwaitingMid:
                // No live copy here any more. The prompt now takes its turn on the
                // COUNTDOWN text (YogaManager.midPosePrompt) so the whole sequence reads
                // on one line; drawing it here as well put it over the card's paragraph.
                SetText(null, null);
                Show(demoButton, false);
                Show(nextButton, false);
                Show(calibrateButton, false);
                break;

            // Set Pose stays offered so a bad capture can be redone without reselecting
            // the pose. Start itself is owned by YogaManager.SetCalibrationState.
            // Cleared, not "You're Ready!": Start appearing IS the ready signal, and the
            // banner sat on top of the card's baked paragraph.
            case YogaManager.CalibrationState.Complete:
                SetText(null, null);
                Show(demoButton, false);
                Show(nextButton, false);
                Show(calibrateButton, true);
                break;
        }
    }

    private void SetText(string heading, string body)
    {
        if (instructionText == null) return;

        // One TMP object renders both lines -- the smallest change that gets a title and
        // a body, rather than adding a second text object and keeping two in sync. The
        // heading is scaled up with rich text because the whole field is already bold,
        // so <b> alone would not distinguish it.
        bool hasHeading = !string.IsNullOrEmpty(heading);
        bool hasBody = !string.IsNullOrEmpty(body);

        if (!hasHeading && !hasBody) instructionText.text = string.Empty;
        else if (!hasHeading) instructionText.text = body;
        else if (!hasBody) instructionText.text = "<size=115%>" + heading + "</size>";
        else instructionText.text = "<size=115%>" + heading + "</size>\n\n" + body;
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
