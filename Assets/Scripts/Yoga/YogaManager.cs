using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Video;
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
    public MediaPipePoseTracker yogaTracker;
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

    // The Calibrate-Mid button's visibility now belongs to YogaUIFlow, which
    // shows it only during the AwaitingMid step. It used to be toggled here from
    // pose data alone, which meant two owners fighting over the same object once
    // the step-based UI landed -- and "does this pose have a mid state" is a
    // weaker condition than "we are asking for the mid capture right now".

    [Tooltip("Gated by calibration: hidden until the selected pose has every calibration it needs. " +
             "MUST be assigned -- StartPose() refuses to run uncalibrated either way, so leaving this " +
             "empty gives a button that stays visible but does nothing except log a warning.")]
    public GameObject startButton;

    /// <summary>
    /// Which calibration step the selected pose is waiting on. Owned here rather
    /// than in YogaUIFlow: the rule for what a pose *requires* (open only, or
    /// open + mid) is flow logic. YogaUIFlow only renders whatever this says.
    /// </summary>
    public enum CalibrationState
    {
        /// <summary>
        /// Main pose screen: Demo + Next only, no setup entered yet. Saved-calibration
        /// seeding deliberately does NOT run for this state, so the main screen looks
        /// identical whether or not this pose was calibrated in an earlier session.
        /// </summary>
        Idle,
        AwaitingOpen,
        AwaitingMid,
        Complete
    }

    public CalibrationState calibrationState { get; private set; }

    /// <summary>Raised whenever calibrationState changes, so the UI can re-render without polling.</summary>
    public event System.Action<CalibrationState> CalibrationStateChanged;

    /// <summary>True once the selected pose has every calibration it needs. Gates the Start button.</summary>
    public bool IsStartReady { get { return calibrationState == CalibrationState.Complete; } }

    /// <summary>
    /// Whether this pose's setup includes a second (mid) capture. Read from the tracker --
    /// the same source OnCalibrationFinished branches on -- so the instruction wording can
    /// never promise a different number of captures than the player actually gets.
    /// </summary>
    public bool SelectedPoseNeedsMid
    {
        get { return yogaTracker != null && yogaTracker.RequiresMidCalibration; }
    }

    [Header("MediaPipe Screen")]
    [Tooltip("'Main Canvas/Container Panel/Body/Annotatable Screen' -- the live camera feed with the " +
             "pose skeleton drawn over it. Hidden on the pose card, shown from Set Pose onward so the " +
             "player can see themselves while matching the pose.")]
    public Transform mediaPipeScreen;

    // LOCAL position, not world: this is the number the Inspector shows on the
    // Annotatable Screen's Transform, so what you read there is what you paste here.
    [Tooltip("Local position while calibrating -- small, tucked into the card's hollow right-hand area. " +
             "Copy this straight off the Annotatable Screen's Transform in the Inspector.")]
    public Vector3 mediaPipeCalibrateLocalPosition = new Vector3(-1088f, 1509f, 0f);

    public Vector3 mediaPipeCalibrateScale = new Vector3(0.7572f, 0.7572f, 0.7572f);

    [Tooltip("Local position during the exercise -- the full-size screen. Both placements are stored " +
             "explicitly rather than captured at Awake, so saving the scene while the screen happens to " +
             "be parked in the calibrate spot cannot silently redefine 'home'.")]
    public Vector3 mediaPipeGameplayLocalPosition = Vector3.zero;

    public Vector3 mediaPipeGameplayScale = Vector3.one;

    [Tooltip("The calibrate-only copy of the webcam picture, in its own Screen-Space-OVERLAY canvas " +
             "so it draws ABOVE the pose card. Carries the picture only, no pose skeleton -- the " +
             "skeleton is LineRenderer-based and cannot render in an Overlay canvas.")]
    public GameObject calibrateFeed;

    [Tooltip("Anchored position (canvas reference-resolution units, NOT screen pixels) of the " +
             "CalibrateFeed RawImage while calibrating. Applied every time the feed is shown, so a " +
             "play-mode nudge cannot be lost on exiting play mode -- change it here, not on the " +
             "RectTransform. " +
             "MUST be anchoredPosition, not world position: for a Screen-Space-Overlay canvas, " +
             "RectTransform.position is raw DEVICE PIXELS, which only lines up with the card at the " +
             "exact window resolution it was read at. Real Fullscreen (FullscreenGameView.Toggle in " +
             "FullScreen.cs) opens at the desktop's native resolution (e.g. 2560x1600), not the Editor " +
             "Game view's (e.g. 1920x1080) -- so a world-position fix drifted to a different spot the " +
             "moment the window size changed. anchoredPosition is defined in CanvasScaler's reference-" +
             "resolution units, which is exactly what makes it resolution-independent.")]
    public Vector2 calibrateFeedAnchoredPosition = new Vector2(331f, 15f);

    [Tooltip("Uniform scale of the CalibrateFeed RawImage while calibrating. localScale is already " +
             "resolution-independent (CanvasScaler scales the whole canvas uniformly), so this one was " +
             "never the problem.")]
    public float calibrateFeedScale = 0.7849095463752747f;

    private RectTransform _calibrateFeedRect;

    private void ShowCalibrateFeed(bool visible)
    {
        if (calibrateFeed == null) return;

        calibrateFeed.SetActive(visible);
        if (!visible) return;

        // Looked up by name rather than serialised: this runs while the scene may be in
        // play mode, where a new inspector reference could not be saved anyway.
        if (_calibrateFeedRect == null)
            _calibrateFeedRect = calibrateFeed.transform.Find("CalibrateFeed") as RectTransform;

        if (_calibrateFeedRect == null)
        {
            Debug.LogWarning("[YogaManager] 'CalibrateFeed' not found under " + calibrateFeed.name
                + " -- the feed will show at whatever placement the scene last saved.", this);
            return;
        }

        // Re-applied on every show, so the placement is defined in one place and cannot
        // drift from whatever the RectTransform happens to hold. anchoredPosition (not
        // .position) so it stays correct at any window/desktop resolution -- see the
        // tooltip on calibrateFeedAnchoredPosition for why that distinction matters.
        _calibrateFeedRect.anchoredPosition = calibrateFeedAnchoredPosition;
        _calibrateFeedRect.localScale = Vector3.one * calibrateFeedScale;
    }

    [Tooltip("The 'Main Canvas' Canvas component -- the whole MediaPipe UI, including 'Container Panel', " +
             "which is an OPAQUE dark grey Image. Hiding only the Annotatable Screen leaves that grey " +
             "block on screen, so the whole canvas is switched instead.")]
    public Canvas mediaPipeCanvas;

    // Toggles Canvas.enabled rather than deactivating GameObjects. MediaPipe assigns the
    // webcam texture from a component ON the Annotatable Screen, so deactivating it stops
    // the frames -- and CalibrateFeedMirror would then have nothing to copy. Disabling the
    // Canvas stops all rendering while every script underneath keeps running.
    private void SetMediaPipeCanvasVisible(bool visible)
    {
        if (mediaPipeCanvas != null) mediaPipeCanvas.enabled = visible;
    }

    private void PlaceMediaPipeScreen(Vector3 localPosition, Vector3 scale)
    {
        if (mediaPipeScreen != null)
        {
            mediaPipeScreen.localPosition = localPosition;
            mediaPipeScreen.localScale = scale;
            // Kept active on purpose -- see SetMediaPipeCanvasVisible.
            if (!mediaPipeScreen.gameObject.activeSelf) mediaPipeScreen.gameObject.SetActive(true);
        }
        SetMediaPipeCanvasVisible(true);
    }

    private void HideMediaPipeScreen()
    {
        SetMediaPipeCanvasVisible(false);

        // Never deactivated: the feed has to keep decoding so the calibrate mirror has a
        // live texture, and so tracking is already warmed up by the time Start is pressed.
        if (mediaPipeScreen != null && !mediaPipeScreen.gameObject.activeSelf)
            mediaPipeScreen.gameObject.SetActive(true);
    }

    // Hidden from the first frame: neither the pose card nor the demo should show a
    // live camera feed, and the scene is saved with the screen enabled for editing.
    private void Start()
    {
        HideMediaPipeScreen();
        ShowCalibrateFeed(false);
    }

    private void OnEnable()
    {
        if (yogaTracker != null) yogaTracker.CalibrationFinished += OnCalibrationFinished;
    }

    private void OnDisable()
    {
        if (yogaTracker != null) yogaTracker.CalibrationFinished -= OnCalibrationFinished;
    }

    // Advances only on a SUCCESSFUL capture, and only once the countdown has
    // actually finished. CalibrateButtonClicked returns the instant its coroutine
    // starts, so advancing on the button click itself would move on ~3s early and
    // would also step past a failed ("get into frame") capture as if it had worked.
    private void OnCalibrationFinished(bool wasMid, bool succeeded)
    {
        if (!succeeded) return;

        if (wasMid)
        {
            _fullCaptureRequested = false;   // sequence finished

            // Mid captured: done unless open somehow still isn't set (e.g. calibrated
            // out of order), in which case fall back rather than unlock Start early.
            SetCalibrationState(yogaTracker.HasSavedCalibration(false)
                ? CalibrationState.Complete
                : CalibrationState.AwaitingOpen);
        }
        else
        {
            // Two different questions, deliberately not the same condition:
            //
            //  - Entering setup (Next): skip captures already saved, so a returning
            //    player is not marched through work they did in an earlier session.
            //  - Pressing Set Pose: the player is explicitly re-doing setup, so the
            //    whole sequence runs -- including the mid capture even when one is
            //    already saved. Without this, a pose whose mid was captured once can
            //    never be re-captured, and the second countdown silently never fires.
            bool needsMid = yogaTracker.RequiresMidCalibration
                         && (_fullCaptureRequested || !yogaTracker.HasSavedCalibration(true));

            if (!needsMid) _fullCaptureRequested = false;   // single-capture pose: done here

            SetCalibrationState(needsMid ? CalibrationState.AwaitingMid : CalibrationState.Complete);

            // A single "Set Pose" press has to cover BOTH captures, so the mid capture
            // is chained from here instead of waiting for a second button. Keyed off
            // the same needsMid computed above rather than re-deriving it, so the chain
            // can never disagree with the state we just set.
            if (needsMid) StartMidCaptureChain();
        }
    }

    [Tooltip("Pause between the open capture's \"Done!\" and the mid capture's 3-2-1. The step copy " +
             "(\"Move into the middle position and hold still.\") is already on screen for this whole " +
             "gap, so this is the time the player actually has to MOVE into the mid pose -- not just " +
             "time to read the checkmark. The tracker's own \"Done!\" dwell is ~2s, so anything at or " +
             "below 2 gives the player no moving time at all once the checkmark clears.")]
    public float midChainDelaySeconds = 4f;

    [Tooltip("Shown on the COUNTDOWN text (not the step label) during the gap between the two " +
             "captures, so setup reads as one continuous sequence on a single line. Keep it SHORT: " +
             "that field is 800x60 and set to Overflow, so a longer line spills outside the box " +
             "instead of shrinking -- the original wording needed 1008x81 and visibly overflowed.")]
    public string midPosePrompt = "Move into the mid pose";

    [Tooltip("How long the open capture's \"Done!\" and checkmark stay up before the mid prompt " +
             "replaces them. Below about 2s the tracker's own dwell is still running and the " +
             "prompt lands on top of the checkmark.")]
    public float midDoneHoldSeconds = 2.5f;

    private Coroutine _midChainCoroutine;

    private void StartMidCaptureChain()
    {
        CancelMidCaptureChain();
        _midChainCoroutine = StartCoroutine(MidCaptureChainRoutine());
    }

    private void CancelMidCaptureChain()
    {
        if (_midChainCoroutine == null) return;
        StopCoroutine(_midChainCoroutine);
        _midChainCoroutine = null;
    }

    // Deliberately just presses the existing Calibrate-Mid entry point after a pause.
    // The countdown, the capture, the "Done!"/checkmark and the CalibrationFinished
    // event are all untouched -- this removes the button press, nothing else.
    private IEnumerator MidCaptureChainRoutine()
    {
        // Two distinct phases sharing one text field, so setup reads as a single
        // continuous line:  3-2-1 -> Done!+check -> move prompt -> 3-2-1 -> Done!+check
        //
        // Phase 1 writes NOTHING. The open capture's "Done!" and its checkmark are
        // already on screen and need their moment; the previous version wrote the
        // prompt at t=0, which erased "Done!" instantly and left the checkmark
        // sitting on top of the prompt.
        var promptText = yogaTracker != null ? yogaTracker.accuracyText : null;

        yield return new WaitForSecondsRealtime(midDoneHoldSeconds);

        // Phase 2: the prompt. Checkmark forced off first -- it belongs to the capture
        // that just succeeded, not to the instruction to move.
        if (yogaTracker != null && yogaTracker.accuracyCheckmark != null)
            yogaTracker.accuracyCheckmark.SetActive(false);

        // Held every frame rather than written once: the tracker's own "Done!" dwell
        // clears this field, and Update() can write to it too, so a single write
        // would be wiped before the player ever read it.
        float elapsed = 0f;
        while (elapsed < midChainDelaySeconds)
        {
            if (promptText != null && promptText.text != midPosePrompt)
                promptText.text = midPosePrompt;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _midChainCoroutine = null;

        // Re-checked rather than trusting a 2s-old decision: the player may have
        // opened the demo, reselected a pose, or left the panel while we waited.
        if (yogaTracker == null || isDemoPlaying) yield break;
        if (calibrationState != CalibrationState.AwaitingMid) yield break;

        yogaTracker.CalibrateMidButtonClicked();
    }

    private void SetCalibrationState(CalibrationState next)
    {
        calibrationState = next;

        if (startButton != null)
            startButton.SetActive(IsStartReady);

        var changed = CalibrationStateChanged;
        if (changed != null) changed(next);
    }

    // Seeds the step from what is ALREADY saved for this pose. Calibrations live in
    // PlayerPrefs and outlive the session, so a returning player who calibrated
    // yesterday must not be marched through the whole sequence again -- by this
    // point SetTargetPose has already restored those saved values into the targets.
    private void ResetCalibrationStateForSelectedPose()
    {
        if (yogaTracker == null || selectedPose == null)
        {
            SetCalibrationState(CalibrationState.AwaitingOpen);
            return;
        }

        bool hasOpen = yogaTracker.HasSavedCalibration(false);
        bool needsMid = yogaTracker.RequiresMidCalibration;
        bool hasMid = needsMid && yogaTracker.HasSavedCalibration(true);

        if (!hasOpen)
            SetCalibrationState(CalibrationState.AwaitingOpen);
        else if (needsMid && !hasMid)
            SetCalibrationState(CalibrationState.AwaitingMid);
        else
            SetCalibrationState(CalibrationState.Complete);
    }

    public void SelectPose(YogaPose pose)
    {
        selectedPose = pose;

        // A pending mid capture, the reference clip, and the live feed all belong to
        // the pose we are leaving.
        CancelMidCaptureChain();
        _fullCaptureRequested = false;
        StopDemoVideo();
        HideMediaPipeScreen();
        ShowCalibrateFeed(false);

        if (pose == null)
        {
            if (yogaTracker != null) yogaTracker.SetTargetPose(null);
            SetCalibrationState(CalibrationState.Idle);
            return;
        }

        if (descriptionImage != null)
            descriptionImage.sprite = pose.icon;

        if (yogaTracker != null)
            yogaTracker.SetTargetPose(pose);

        // Lands on the main screen (Demo + Next) rather than seeding the calibration
        // step here. The seeding moved into BeginSetup so that Next is always the way
        // into setup -- including for a pose that is already fully calibrated, which
        // simply passes straight through to Complete.
        SetCalibrationState(CalibrationState.Idle);
    }

    /// <summary>
    /// Next button on the main pose screen. Enters setup and seeds the step from what
    /// is ALREADY saved for this pose, so a player who calibrated in an earlier session
    /// lands directly on Complete instead of being marched through the captures again.
    /// Reuses ResetCalibrationStateForSelectedPose rather than re-deriving
    /// "already calibrated" a second time.
    /// </summary>
    public void BeginSetup()
    {
        if (isDemoPlaying) return;

        _fullCaptureRequested = false;

        // Swap to the setup card: same wide layout, but an empty text box (the copy is
        // drawn live by YogaUIFlow) and a hollow right-hand area for the clip. A pose
        // with no setup art yet keeps whatever card is showing, so a missing sprite
        // degrades to "old card, correct buttons" instead of a blank panel.
        if (descriptionImage != null && selectedPose != null && selectedPose.setupImage != null)
            descriptionImage.sprite = selectedPose.setupImage;

        // The card's hollow area shows the LIVE camera feed here, not the demo clip:
        // calibration is about seeing yourself, so the player can check they are in
        // frame and in position before the capture fires.
        StopDemoVideo();

        // The overlay copy, not the real screen: the real one lives in a camera-based
        // canvas that Unity always draws BENEATH the pose card, so it could never be
        // seen here. It stays hidden until Start, when the card is gone anyway.
        ShowCalibrateFeed(true);

        ResetCalibrationStateForSelectedPose();
    }

    /// <summary>
    /// Back while on the setup screen: returns to the main pose card (Demo + Next),
    /// drops any pending automatic mid capture, and stops the reference clip.
    /// Calibration already captured is NOT discarded -- it lives in PlayerPrefs, so
    /// pressing Next again lands on whatever step is genuinely still outstanding.
    /// </summary>
    public void CancelSetup()
    {
        CancelMidCaptureChain();
        _fullCaptureRequested = false;
        StopDemoVideo();
        HideMediaPipeScreen();
        ShowCalibrateFeed(false);   // back on the pose card: no live feed there

        if (descriptionImage != null && selectedPose != null && selectedPose.icon != null)
            descriptionImage.sprite = selectedPose.icon;

        SetCalibrationState(CalibrationState.Idle);
    }

    /// <summary>
    /// The single Back button. Demo and setup both use it, so it has to know which one
    /// it is closing. Wire the Back button's onClick to THIS rather than to HideDemo.
    /// </summary>
    public void BackButtonClicked()
    {
        if (isDemoPlaying) { HideDemo(); return; }
        if (calibrationState != CalibrationState.Idle) CancelSetup();
    }

    /// <summary>
    /// The single player-facing "Set Pose" button. Runs whichever capture the current
    /// step needs, so one button covers the whole sequence -- and so a chain that was
    /// interrupted (panel disabled, demo opened) can still be finished by hand instead
    /// of stranding the player on a step with no way forward.
    /// Wire the Set Pose button's onClick to THIS, not to the tracker directly.
    /// </summary>
    // Set by a deliberate Set Pose press on the OPEN capture, so OnCalibrationFinished
    // knows to chain the mid capture even when a mid is already saved. Cleared once the
    // sequence ends, so it can never leak into a later entry into setup.
    private bool _fullCaptureRequested;

    public void SetPoseButtonClicked()
    {
        if (yogaTracker == null || isDemoPlaying) return;

        CancelMidCaptureChain(); // a manual press supersedes a pending automatic one

        if (calibrationState == CalibrationState.AwaitingMid)
        {
            // Already mid-sequence (or recovering an interrupted chain) -- capture the
            // mid, do not restart the whole thing.
            yogaTracker.CalibrateMidButtonClicked();
        }
        else
        {
            _fullCaptureRequested = true;
            yogaTracker.CalibrateButtonClicked();
        }
    }

    // ---------------- DEMO ----------------
    //
    // Demo swaps the description card for that pose's wider demo art (portrait
    // moved top-right) and lets the instructor perform the movement behind it,
    // with only a Back button offered. Deliberately NOT a separate panel: the
    // art is a drop-in replacement for the same Image, so reusing the existing
    // description object keeps one layout to maintain instead of two that drift.
    //
    // Tracking is never started here -- Demo is "watch", not "do".

    [Header("Demo Video")]
    [Tooltip("RawImage sitting in the demo card's empty right-hand area. Hidden unless the pose has a demoVideo.")]
    public RawImage demoVideoDisplay;
    public VideoPlayer demoVideoPlayer;

    public bool isDemoPlaying { get; private set; }

    /// <summary>Raised when demo mode opens (true) or closes (false), so the UI can swap its button row.</summary>
    public event System.Action<bool> DemoStateChanged;

    private Coroutine demoCoroutine;
    private Sprite _descriptionSpriteBeforeDemo;

    public void ShowDemo()
    {
        if (selectedPose == null) return;
        if (isDemoPlaying) return;

        if (selectedPose.demoImage == null)
        {
            Debug.LogWarning($"[YogaManager] '{selectedPose.name}' has no Demo Image assigned -- nothing to show.", selectedPose);
            return;
        }

        isDemoPlaying = true;

        if (descriptionImage != null)
        {
            _descriptionSpriteBeforeDemo = descriptionImage.sprite;
            descriptionImage.sprite = selectedPose.demoImage;
        }

        // Start is owned here, so hide it here -- YogaUIFlow handles the rest of the row.
        if (startButton != null) startButton.SetActive(false);

        StartDemoVideo();

        var changed = DemoStateChanged;
        if (changed != null) changed(true);

        demoCoroutine = StartCoroutine(DemoRoutine());
    }

    /// <summary>Back button. Stops the demo and restores the normal description card and button row.</summary>
    public void HideDemo()
    {
        if (!isDemoPlaying) return;
        isDemoPlaying = false;

        if (demoCoroutine != null) { StopCoroutine(demoCoroutine); demoCoroutine = null; }

        StopDemoVideo();

        if (descriptionImage != null && _descriptionSpriteBeforeDemo != null)
            descriptionImage.sprite = _descriptionSpriteBeforeDemo;

        // Restore Start only if the pose actually earned it; demo must not unlock it.
        if (startButton != null) startButton.SetActive(IsStartReady);

        // The mid chain refuses to fire while the demo is up, so if we were mid-setup
        // when the demo opened, re-arm it here. Without this the player returns to a
        // step that shows no button and waits for a capture that will never run.
        if (calibrationState == CalibrationState.AwaitingMid) StartMidCaptureChain();

        var changed = DemoStateChanged;
        if (changed != null) changed(false);
    }

    // Runs the instructor through the movement once. Reuses PlayForward/PlayReversed
    // and the same per-pose timings gameplay uses, so retuning a pose retunes its
    // demo too -- a hand-copied sequence here would drift the moment either changed.
    // APIOnly rather than a RenderTexture asset: the clips differ in resolution
    // (720p and 1080p so far) and a RenderTexture is fixed-size, so it would
    // letterbox or crop whichever clips did not match whatever size we authored.
    // APIOnly hands us the decoder's own texture at the clip's native size.
    private void StartDemoVideo()
    {
        if (demoVideoPlayer == null || demoVideoDisplay == null) return;

        var clip = selectedPose != null ? selectedPose.demoVideo : null;
        if (clip == null)
        {
            demoVideoDisplay.gameObject.SetActive(false);
            return;
        }

        // Activate BEFORE Prepare(). The VideoPlayer lives on this same GameObject,
        // and Prepare() on an inactive object silently does nothing -- prepareCompleted
        // never fires, the texture never arrives, and the RawImage sits there
        // rendering its own solid white.
        demoVideoDisplay.gameObject.SetActive(true);

        // Transparent until the first frame exists, otherwise the card shows a
        // white block for however long decoding takes.
        demoVideoDisplay.texture = null;
        demoVideoDisplay.color = Color.clear;

        demoVideoPlayer.clip = clip;
        demoVideoPlayer.isLooping = true;          // demo repeats until Back
        demoVideoPlayer.renderMode = VideoRenderMode.APIOnly;
        demoVideoPlayer.prepareCompleted -= OnDemoVideoPrepared;
        demoVideoPlayer.prepareCompleted += OnDemoVideoPrepared;
        demoVideoPlayer.errorReceived -= OnDemoVideoError;
        demoVideoPlayer.errorReceived += OnDemoVideoError;
        demoVideoPlayer.Prepare();
    }

    // Without this a decode failure is indistinguishable from "still loading" --
    // both just leave the card blank.
    private void OnDemoVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError($"[YogaManager] Demo video failed for '{(selectedPose == null ? "?" : selectedPose.name)}': {message}", this);
        if (demoVideoDisplay != null) demoVideoDisplay.gameObject.SetActive(false);
    }

    // The decoder texture only exists once prepared -- assigning before this
    // point leaves the RawImage showing nothing.
    private void OnDemoVideoPrepared(VideoPlayer vp)
    {
        if (demoVideoDisplay != null)
        {
            demoVideoDisplay.texture = vp.texture;
            demoVideoDisplay.color = Color.white;   // reveal now that there is a frame
        }
        vp.Play();
    }

    private void StopDemoVideo()
    {
        if (demoVideoPlayer != null)
        {
            demoVideoPlayer.prepareCompleted -= OnDemoVideoPrepared;
            demoVideoPlayer.errorReceived -= OnDemoVideoError;
            demoVideoPlayer.Stop();
        }
        if (demoVideoDisplay != null)
        {
            demoVideoDisplay.texture = null;
            demoVideoDisplay.gameObject.SetActive(false);
        }
    }

    IEnumerator DemoRoutine()
    {
        if (instructorAnimator == null || selectedPose == null) yield break;

        // A real-person clip is the demo when one exists; running the instructor
        // underneath as well would show two different demonstrations at once.
        if (selectedPose.demoVideo != null) yield break;

        if (selectedPose.transitionAnimation != null)
        {
            instructorAnimator.Play(selectedPose.transitionAnimation.name);
            yield return new WaitForSeconds(selectedPose.transitionAnimation.length);
        }

        if (selectedPose.poseAnimation != null)
            instructorAnimator.CrossFade(selectedPose.poseAnimation.name, 0.3f);

        // A pose with a genuine second state gets one full out-and-back so the
        // player sees the whole movement, not just the end position.
        if (selectedPose.MidPoseAnimation != null)
        {
            AnimationClip midTransition = selectedPose.reverseTransitionAnimation;

            yield return new WaitForSeconds(OpenHold);
            if (midTransition != null) yield return StartCoroutine(PlayForward(midTransition, ToClosed));

            instructorAnimator.CrossFadeInFixedTime(selectedPose.MidPoseAnimation.name, Blend);
            yield return new WaitForSeconds(ClosedHold);

            if (midTransition != null)
                yield return StartCoroutine(PlayReversed(midTransition, ToOpen, selectedPose.poseAnimation.name));
            else
                instructorAnimator.CrossFadeInFixedTime(selectedPose.poseAnimation.name, Blend);
        }
        else
        {
            yield return new WaitForSeconds(OpenHold);
        }

        demoCoroutine = null;
        // Deliberately does NOT auto-close: the player decides when to leave via Back.
    }

    public void StartPose()
    {
        if(selectedPose == null)
            return;

        // Belt-and-braces alongside hiding the button: a pose started before its
        // targets exist would grade every frame against the baked instructor rig
        // (or, for an unbaked pose, against nothing at all) and hand back a
        // meaningless score.
        if (!IsStartReady)
        {
            Debug.LogWarning("[YogaManager] Start blocked: '" + selectedPose.name + "' still needs " +
                (calibrationState == CalibrationState.AwaitingMid ? "its mid" : "an open") + " calibration.", this);
            return;
        }

        // The setup screen's reference clip has done its job. Hiding the panel alone
        // would leave the VideoPlayer decoding behind it for the whole exercise.
        StopDemoVideo();

        // The feed stays up for the exercise, but back at full size in its own spot --
        // leaving it in the small calibrate slot would play the whole session inside
        // the footprint of a card that is no longer on screen.
        ShowCalibrateFeed(false);
        PlaceMediaPipeScreen(mediaPipeGameplayLocalPosition, mediaPipeGameplayScale);

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

        // No recenter step here for MediaPipePoseTracker: unlike the glove (whose
        // IMU has no absolute reference frame), MediaPipe's joint-angle accuracy
        // is body-relative by construction and needs no per-session calibration.

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

#if UNITY_EDITOR
        // Deferred: Unity forbids AssetDatabase access from inside OnValidate.
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) WarnOnCycleOverrun(); };
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// The hold timer ends the pose wherever the cycle has got to, so warn rather
    /// than let the last rep silently get cut off.
    ///
    /// Checks EVERY pose asset, not just the defaults on this component. Each
    /// YogaPose can override all four durations and the cycle count, so validating
    /// only these fields missed the exact case the check exists for: SideBendLeft's
    /// own timings need ~38s against a 30s hold and never once warned.
    /// </summary>
    private void WarnOnCycleOverrun()
    {
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:YogaPose"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            YogaPose pose = UnityEditor.AssetDatabase.LoadAssetAtPath<YogaPose>(path);

            // A pose with no mid clip never cycles -- it just holds, so it cannot overrun.
            if (pose == null || pose.MidPoseAnimation == null) continue;

            float cycle = Resolve(pose.openHoldDuration, openHoldDuration)
                        + Resolve(pose.toClosedDuration, toClosedDuration)
                        + Resolve(pose.closedHoldDuration, closedHoldDuration)
                        + Resolve(pose.toOpenDuration, toOpenDuration);
            int cycles = pose.midPoseCycles > 0 ? pose.midPoseCycles : midPoseCycles;
            float needed = cycle * cycles;

            if (needed > holdTime)
                Debug.LogWarning(
                    $"[YogaManager] '{pose.name}': {cycles} cycles x {cycle:F1}s need {needed:F1}s " +
                    $"but holdTime is {holdTime:F1}s - the last rep will be cut short. Raise holdTime to at " +
                    $"least {Mathf.Ceil(needed)}, lower that pose's Mid Pose Cycles, or shorten its durations.", pose);
        }
    }
#endif

    IEnumerator PoseLoopRoutine()
    {
        // No mid pose means the pose clip just loops on its own — nothing to drive.
        if (selectedPose.MidPoseAnimation == null)
            yield break;

        // Played forwards to reach the mid pose, then backwards to come home.
        AnimationClip midTransition = selectedPose.reverseTransitionAnimation;

        // The pose clips loop, so holding longer than the clip just keeps them
        // breathing rather than freezing on the last frame.
        // This loop is the single source of truth for which state the exercise is
        // asking for, so it also drives what the tracker grades against. The
        // tracker used to decide that itself by picking whichever target the
        // player was nearest, which flipped unpredictably mid-transition and
        // rewarded standing still in either end state. Travel times are handed
        // over so the graded target sweeps in step with the instructor.
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            if (yogaTracker != null) yogaTracker.SetMidBlend(0f, 0f);
            yield return new WaitForSeconds(OpenHold);

            if (yogaTracker != null) yogaTracker.SetMidBlend(1f, ToClosed);
            if (midTransition != null)
                yield return StartCoroutine(PlayForward(midTransition, ToClosed));

            instructorAnimator.CrossFadeInFixedTime(selectedPose.MidPoseAnimation.name, Blend);
            yield return new WaitForSeconds(ClosedHold);

            if (yogaTracker != null) yogaTracker.SetMidBlend(0f, ToOpen);
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