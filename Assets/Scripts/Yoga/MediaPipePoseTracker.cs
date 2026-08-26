using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;

/// <summary>
/// MediaPipe-driven sibling to YogaTracker (glove/IMU). Exposes the same surface
/// YogaManager already reads (accuracy/alignment/steadiness, StartTracking/
/// StopTracking, SetTargetPose) but sources it from MediaPipe's upper-body world
/// landmarks instead of the ESP32 glove. YogaTracker.cs itself is untouched --
/// this is a genuinely separate component (not a subclass, not behind a shared
/// interface), matching the "must not be combined" decision.
///
/// One-pose (Prayer) MVP -- only the 8 canonical upper-body joints / 5 joint
/// angles from YogaJointAngles are evaluated.
/// </summary>
public class MediaPipePoseTracker : MonoBehaviour
{
    // MediaPipe BlazePose landmark indices used by the upper-body MVP.
    private const int LeftShoulder = 11;
    private const int RightShoulder = 12;
    private const int LeftElbow = 13;
    private const int RightElbow = 14;
    private const int LeftWrist = 15;
    private const int RightWrist = 16;
    private const int LeftHip = 23;
    private const int RightHip = 24;
    private const int RequiredLandmarkCount = 25; // must cover index 24 (right hip)

    [Header("MediaPipe Source")]
    [Tooltip("The PoseLandmarkerRunner on the scene's 'Solution' object.")]
    public Mediapipe.Unity.Sample.PoseLandmarkDetection.PoseLandmarkerRunner poseLandmarkerRunner;

    [Header("Score")]
    public float accuracy;
    public TMPro.TMP_Text accuracyText;
    [Tooltip("Shown alongside accuracyText only on a successful Calibrate/Calibrate-Mid -- hidden the moment a new countdown starts, and never shown on a failed calibration.")]
    public GameObject accuracyCheckmark;

    [Header("Calibration (tunable -- not yet playtested)")]
    public float elbowTolerance = 25f;
    public float shoulderTolerance = 25f;
    public float torsoLeanTolerance = 15f;
    public float elbowWeight = 1f;
    public float shoulderWeight = 1f;
    public float torsoLeanWeight = 1f;
    // Live-tested: even clearly-tracked landmarks commonly read 0.1-0.5 visibility
    // in a typical desk/laptop webcam framing (see design notes) -- 0.5 rejected
    // almost every frame. 0.25 is a deliberately forgiving starting point.
    [Range(0f, 1f)] public float minLandmarkConfidence = 0.25f;
    public float smoothSpeed = 5f;
    [Tooltip("How quickly the live joint angles (used for scoring AND target-circle placement) settle toward each new MediaPipe reading, damping frame-to-frame landmark jitter before it reaches the accuracy score. Higher = snappier but jitterier; lower = smoother but laggier.")]
    public float jointSmoothSpeed = 10f;

    public bool tracking { get; private set; }

    [Header("Session Result")]
    public float sampleInterval = 0.2f;
    public float alignment;
    public float steadiness;

    [Header("Target Circles")]
    [Tooltip("Full-stretch RectTransform the circles are positioned within (local space, center-pivot).")]
    public RectTransform targetSpace;
    public TargetCircleView leftElbowCircle;
    public TargetCircleView rightElbowCircle;
    public TargetCircleView leftWristCircle;
    public TargetCircleView rightWristCircle;
    [Tooltip("How quickly each target circle's on-screen position settles toward its newly computed position -- damps jitter/rotation noise on top of the joint-angle smoothing above.")]
    public float circleSmoothSpeed = 12f;

    [Header("Actual Position Dots (live-detected, separate from targets)")]
    [Tooltip("Solid dot = where MediaPipe currently detects this joint, live. Hollow circle above = the " +
        "FIXED calibrated target -- static, from the exact screen position MediaPipe saw this joint the " +
        "moment you pressed Calibrate. Only shown once that joint has an actual saved calibration.")]
    public ActualPositionDotView leftElbowDot;
    public ActualPositionDotView rightElbowDot;
    public ActualPositionDotView leftWristDot;
    public ActualPositionDotView rightWristDot;

    private readonly List<float> _accuracySamples = new List<float>();
    private float _sampleTimer;

    private YogaJointAngles.JointAngles _target;
    private bool _hasTarget;

    // Second target for poses with a genuine held second state (e.g. Open Arms
    // <-> Closed Arms). Which of _target/_targetMid scoring uses is decided by
    // the exercise phase, not by the player's proximity to either -- see
    // SetMidBlend / ResolveActiveTarget. Blending is whole-pose, never per-joint:
    // mixing pieces of two different states could "pass" a pose that doesn't
    // actually match either one.
    private YogaJointAngles.JointAngles _targetMid;
    private bool _hasMidTarget;

    // Written from the LIVE_STREAM callback, which per PoseLandmarkerRunner's
    // Run() coroutine may fire off Unity's main thread (see AnnotationController's
    // own doc comment on this). Must stay allocation-light and touch no Unity API.
    // Read/cleared on the main thread in Update() -- same isStale-style handoff
    // AnnotationController already uses for the identical reason.
    private PoseLandmarkerResult _pendingResult;
    private volatile bool _hasPendingResult;

    private float _rawAccuracy;
    private bool _hasLivePose;

    // Per-joint state, refreshed whenever a valid result arrives; consumed every
    // Update() frame (not just on new-data frames) for smoothing & circle display.
    private float _scoreLeftElbow, _scoreRightElbow, _scoreLeftShoulder, _scoreRightShoulder, _scoreTorsoLean;
    // Whether each joint's score has ever been computed at least once -- gates
    // both the weighted average (below) and the "nothing scoreable yet" check,
    // independently per joint (e.g. hips never in frame permanently excludes
    // torso lean/shoulder angle but must not block elbow scoring).
    private bool _hasLeftElbow, _hasRightElbow, _hasLeftShoulder, _hasRightShoulder, _hasTorsoLean;

    // Smoothing state -- separate from the per-point "ever seen" cache above,
    // which handles missing data, not noise. These damp frame-to-frame jitter in
    // otherwise-valid, currently-tracked readings. Reset on StartTracking() so a
    // freshly-selected pose doesn't visibly drift in from the previous pose's
    // settled values.
    private YogaJointAngles.JointAngles _smoothedJoints;
    private bool _hasSmoothedJoints;

    // Live (un-rotated) 2D positions for the solid actual-position dots.
    private Vector2 _livePosLeftElbow, _livePosRightElbow, _livePosLeftWrist, _livePosRightWrist;
    private bool _hasSmLeftElbowLive, _hasSmRightElbowLive, _hasSmLeftWristLive, _hasSmRightWristLive;

    // Fixed calibrated-target screen positions -- captured (a snapshot of the
    // live position fields above) once at Calibrate/Calibrate Mid time, then
    // static until recalibrated. Two independent sets since a pose can have two
    // calibrated states; which one is shown each frame follows the same
    // open/mid state scoring is using (_activeIsMid, derived from the
    // phase-driven _midBlend). Per-joint has-flags: a pose might have its open
    // elbow calibrated but never its wrist, if the wrist wasn't tracked at the
    // moment Calibrate was pressed.
    private Vector2 _fixedPosOpenLeftElbow, _fixedPosOpenRightElbow, _fixedPosOpenLeftWrist, _fixedPosOpenRightWrist;
    private Vector2 _fixedPosMidLeftElbow, _fixedPosMidRightElbow, _fixedPosMidLeftWrist, _fixedPosMidRightWrist;
    private bool _hasFixedOpenLeftElbow, _hasFixedOpenRightElbow, _hasFixedOpenLeftWrist, _hasFixedOpenRightWrist;
    private bool _hasFixedMidLeftElbow, _hasFixedMidRightElbow, _hasFixedMidLeftWrist, _hasFixedMidRightWrist;

    // Which held state scoring currently evaluates against, as a 0..1 blend
    // (0 = open target, 1 = mid target). Driven ONLY by the exercise phase --
    // YogaManager.PoseLoopRoutine calls SetMidBlend() as it moves the instructor
    // between states. It is deliberately NOT derived from how close the player
    // happens to be to either target: the exercise is a prescribed sequence
    // (open -> mid -> open), so the thing being graded is whether the player is
    // in the state the routine is currently asking for. Picking whichever target
    // the player was already nearest to made the grade flip mid-transition and
    // let a player who never moved score well against whichever state they
    // happened to be standing in.
    private float _midBlend;
    private float _midBlendFrom;
    private float _midBlendTo;
    private float _midBlendDuration;
    private float _midBlendElapsed;

    // Derived from _midBlend, not decided independently. UpdateTargetCircles
    // reads this to pick which set of fixed calibrated rings to draw; there is
    // only one ring set per state, so it snaps at the halfway point rather than
    // trying to interpolate two different saved screen positions.
    private bool _activeIsMid;

    private void OnEnable()
    {
        if (poseLandmarkerRunner != null)
            poseLandmarkerRunner.OnPoseLandmarkerResult += OnPoseLandmarkerResult;
        else
            Debug.LogWarning("[MediaPipePoseTracker] poseLandmarkerRunner is not assigned -- no landmark data will arrive.", this);
    }

    private void OnDisable()
    {
        if (poseLandmarkerRunner != null)
            poseLandmarkerRunner.OnPoseLandmarkerResult -= OnPoseLandmarkerResult;
    }

    /// <summary>
    /// Subscribed to PoseLandmarkerRunner.OnPoseLandmarkerResult. May run off the
    /// main thread -- must not touch Unity APIs here beyond this simple field/flag
    /// write.
    /// </summary>
    private void OnPoseLandmarkerResult(PoseLandmarkerResult result)
    {
        _pendingResult = result;
        _hasPendingResult = true;
        _diagResultsReceived++;
    }

    // --- TEMPORARY diagnostics (Prayer MVP bring-up) ---------------------------
    // Throttled to ~once/sec so it doesn't flood the console. Safe to delete once
    // live accuracy is confirmed working.
    private int _diagResultsReceived;
    private float _diagLastLogTime;
    private string _diagLastFailReason = "";

    // Local persistence for player-calibrated targets (see CalibrateFromCurrentPose
    // below). PlayerPrefs, not the YogaPose asset: writing back into a
    // ScriptableObject asset only works via AssetDatabase, which is Editor-only
    // and does not exist in a built player. Keyed per pose by asset name, since
    // that's already how every pose is uniquely identified elsewhere (bake tool,
    // pose selection buttons).
    private const string CalibrationPrefPrefix = "YogaCalib_";
    private string _currentPoseKey;

    public void SetTargetPose(YogaPose pose)
    {
        if (pose == null)
        {
            _hasTarget = false;
            _currentPoseKey = null;
            return;
        }
        if (!pose.hasMediaPipeTarget)
        {
            Debug.LogWarning($"[MediaPipePoseTracker] '{pose.name}' has no baked MediaPipe target " +
                "(run Tools > Yoga > Bake MediaPipe Target For Selected Pose first) -- accuracy will stay 0.", pose);
            _hasTarget = false;
            _currentPoseKey = null;
            return;
        }

        _currentPoseKey = pose.name;

        // A pose switch must not inherit the previous pose's open/mid decision --
        // UpdateTargetCircles reads _activeIsMid directly, so a stale 'true' left
        // over from a two-state pose makes a single-state pose look at the (now
        // cleared) _hasFixedMid* flags and never draw its target rings at all.
        ResetPosePhase();

        _target = LoadTarget("", pose.targetLeftElbowAngle, pose.targetRightElbowAngle,
            pose.targetLeftShoulderAngle, pose.targetRightShoulderAngle, pose.targetTorsoLean);

        // NOT pose.hasMediaPipeMidTarget on its own: the baker fills the Mid angles
        // in for ANY pose that merely has a MidPoseAnimation, including ones whose
        // mid clip is only a transition/rest position. Grading against a rest clip
        // hands the player a ~100% score for standing in it. YogaPose.gradeMidPose
        // is the author's explicit opt-in that the second state is a real held pose.
        _hasMidTarget = pose.HasGradableMidPose;
        if (_hasMidTarget)
        {
            _targetMid = LoadTarget("Mid", pose.targetLeftElbowAngleMid, pose.targetRightElbowAngleMid,
                pose.targetLeftShoulderAngleMid, pose.targetRightShoulderAngleMid, pose.targetTorsoLeanMid);
        }

        // Fixed calibrated-target dot positions -- independent of the angle
        // targets above (no baked-instructor fallback: a pose that's never been
        // calibrated simply has no fixed dot to show, per joint).
        LoadFixedPositions("", out _fixedPosOpenLeftElbow, out _hasFixedOpenLeftElbow, out _fixedPosOpenRightElbow, out _hasFixedOpenRightElbow,
            out _fixedPosOpenLeftWrist, out _hasFixedOpenLeftWrist, out _fixedPosOpenRightWrist, out _hasFixedOpenRightWrist);
        if (_hasMidTarget)
        {
            LoadFixedPositions("Mid", out _fixedPosMidLeftElbow, out _hasFixedMidLeftElbow, out _fixedPosMidRightElbow, out _hasFixedMidRightElbow,
                out _fixedPosMidLeftWrist, out _hasFixedMidLeftWrist, out _fixedPosMidRightWrist, out _hasFixedMidRightWrist);
        }
        else
        {
            _hasFixedMidLeftElbow = _hasFixedMidRightElbow = _hasFixedMidLeftWrist = _hasFixedMidRightWrist = false;
        }

        _hasTarget = true;
    }

    private void LoadFixedPositions(string suffix,
        out Vector2 leftElbow, out bool hasLeftElbow, out Vector2 rightElbow, out bool hasRightElbow,
        out Vector2 leftWrist, out bool hasLeftWrist, out Vector2 rightWrist, out bool hasRightWrist)
    {
        hasLeftElbow = PlayerPrefs.GetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosLEl" + suffix, 0) == 1;
        leftElbow = hasLeftElbow
            ? new Vector2(PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLElX" + suffix),
                           PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLElY" + suffix))
            : default;

        hasRightElbow = PlayerPrefs.GetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosREl" + suffix, 0) == 1;
        rightElbow = hasRightElbow
            ? new Vector2(PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRElX" + suffix),
                           PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRElY" + suffix))
            : default;

        hasLeftWrist = PlayerPrefs.GetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosLWr" + suffix, 0) == 1;
        leftWrist = hasLeftWrist
            ? new Vector2(PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLWrX" + suffix),
                           PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLWrY" + suffix))
            : default;

        hasRightWrist = PlayerPrefs.GetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosRWr" + suffix, 0) == 1;
        rightWrist = hasRightWrist
            ? new Vector2(PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRWrX" + suffix),
                           PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRWrY" + suffix))
            : default;
    }

    // Shared by both the open and mid target: baked value from the pose asset,
    // overridden wholesale by a saved player calibration if one exists for this
    // pose+suffix (suffix "" = open state, "Mid" = second state).
    private YogaJointAngles.JointAngles LoadTarget(string suffix,
        float bakedLeftElbow, float bakedRightElbow, float bakedLeftShoulder, float bakedRightShoulder, float bakedTorsoLean)
    {
        var t = new YogaJointAngles.JointAngles
        {
            leftElbow = bakedLeftElbow,
            rightElbow = bakedRightElbow,
            leftShoulder = bakedLeftShoulder,
            rightShoulder = bakedRightShoulder,
            torsoLean = bakedTorsoLean
        };

        if (PlayerPrefs.GetInt(CalibrationPrefPrefix + _currentPoseKey + "_Has" + suffix, 0) == 1)
        {
            t.leftElbow = PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_LEl" + suffix);
            t.rightElbow = PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_REl" + suffix);
            t.leftShoulder = PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_LSh" + suffix);
            t.rightShoulder = PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_RSh" + suffix);
            t.torsoLean = PlayerPrefs.GetFloat(CalibrationPrefPrefix + _currentPoseKey + "_Torso" + suffix);
            Debug.Log($"[MediaPipePoseTracker] Loaded saved calibration for '{_currentPoseKey}'" +
                (string.IsNullOrEmpty(suffix) ? "" : $" ({suffix} state)") + ".");
        }

        return t;
    }

    /// <summary>
    /// Whether the CURRENTLY selected pose already has a player calibration saved
    /// for that state. Calibrations live in PlayerPrefs and outlive the session,
    /// so a flow that sequences calibration steps has to seed itself from this --
    /// otherwise a player who calibrated yesterday is marched through the whole
    /// sequence again even though LoadTarget has already restored their values.
    /// False whenever no pose is selected, or the pose failed to load a target.
    /// </summary>
    public bool HasSavedCalibration(bool mid)
    {
        if (string.IsNullOrEmpty(_currentPoseKey)) return false;
        if (mid && !_hasMidTarget) return false;
        return PlayerPrefs.GetInt(CalibrationPrefPrefix + _currentPoseKey + "_Has" + (mid ? "Mid" : ""), 0) == 1;
    }

    /// <summary>True when this pose has a second held state that needs its own calibration (see YogaPose.HasGradableMidPose).</summary>
    public bool RequiresMidCalibration { get { return _hasMidTarget; } }

    private void SaveCalibration(YogaJointAngles.JointAngles values, string suffix)
    {
        if (string.IsNullOrEmpty(_currentPoseKey)) return;

        PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_LEl" + suffix, values.leftElbow);
        PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_REl" + suffix, values.rightElbow);
        PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_LSh" + suffix, values.leftShoulder);
        PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_RSh" + suffix, values.rightShoulder);
        PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_Torso" + suffix, values.torsoLean);
        PlayerPrefs.SetInt(CalibrationPrefPrefix + _currentPoseKey + "_Has" + suffix, 1);
        PlayerPrefs.Save();
    }

    // Fixed calibrated-target screen positions -- a separate PlayerPrefs record
    // from the angle values above (different consumer: the fixed dot markers,
    // not scoring). Saved/has-flagged per JOINT, not as one combined flag like
    // the angles: a position only needs that one landmark visible, so e.g. an
    // elbow calibrated while the wrist happened to be out of frame should still
    // save the elbow dot, not silently save a bogus (0,0) for the wrist too.
    private void SaveFixedPositions(Vector2 leftElbow, bool hasLeftElbow, Vector2 rightElbow, bool hasRightElbow,
        Vector2 leftWrist, bool hasLeftWrist, Vector2 rightWrist, bool hasRightWrist, string suffix)
    {
        if (string.IsNullOrEmpty(_currentPoseKey)) return;

        if (hasLeftElbow)
        {
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLElX" + suffix, leftElbow.x);
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLElY" + suffix, leftElbow.y);
            PlayerPrefs.SetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosLEl" + suffix, 1);
        }
        if (hasRightElbow)
        {
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRElX" + suffix, rightElbow.x);
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRElY" + suffix, rightElbow.y);
            PlayerPrefs.SetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosREl" + suffix, 1);
        }
        if (hasLeftWrist)
        {
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLWrX" + suffix, leftWrist.x);
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosLWrY" + suffix, leftWrist.y);
            PlayerPrefs.SetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosLWr" + suffix, 1);
        }
        if (hasRightWrist)
        {
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRWrX" + suffix, rightWrist.x);
            PlayerPrefs.SetFloat(CalibrationPrefPrefix + _currentPoseKey + "_PosRWrY" + suffix, rightWrist.y);
            PlayerPrefs.SetInt(CalibrationPrefPrefix + _currentPoseKey + "_HasPosRWr" + suffix, 1);
        }
        PlayerPrefs.Save();
    }

    /// <summary>Clears BOTH saved calibrations (open and mid, if any) for the given pose, reverting it to the baked instructor target(s) next time it's selected.</summary>
    public static void ClearSavedCalibration(string poseName)
    {
        foreach (var suffix in new[] { "", "Mid" })
        {
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_LEl" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_REl" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_LSh" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_RSh" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_Torso" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_Has" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosLElX" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosLElY" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosRElX" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosRElY" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosLWrX" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosLWrY" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosRWrX" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_PosRWrY" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_HasPosLEl" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_HasPosREl" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_HasPosLWr" + suffix);
            PlayerPrefs.DeleteKey(CalibrationPrefPrefix + poseName + "_HasPosRWr" + suffix);
        }
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Overwrites the OPEN (main) target with the player's own live (smoothed)
    /// joint angles -- lets a player calibrate "correct" to their own body/
    /// mobility instead of the baked instructor-rig target. Only overwrites
    /// joints that are currently reliably tracked (same _hasX gating used for
    /// scoring); a joint with no live data yet keeps whatever target it had
    /// before, rather than being corrupted with garbage default-Vector3 angles.
    /// Persisted locally (PlayerPrefs, keyed by pose name) -- selecting this
    /// pose again, including in a future session, loads the saved calibration
    /// instead of the baked instructor target. Does NOT touch the YogaPose
    /// asset itself (can't, from a build) or affect any other player's device.
    /// </summary>
    public bool CalibrateFromCurrentPose()
    {
        // Without a loaded pose, _target still holds the PREVIOUS pose's angles and
        // _currentPoseKey is null -- calibrating here would switch scoring back on
        // (_hasTarget = true) against a hybrid of the old pose's target and the
        // player's live angles, while SaveCalibration silently discarded the result
        // because it has no key to save under.
        if (!_hasTarget || string.IsNullOrEmpty(_currentPoseKey))
        {
            Debug.LogWarning("[MediaPipePoseTracker] CalibrateFromCurrentPose: no pose selected, or the " +
                "selected pose has no baked MediaPipe target -- nothing to calibrate against.", this);
            if (accuracyText != null) accuracyText.text = "Failed! No pose selected.";
            return false;
        }

        bool anyJointTracked = _hasLeftElbow || _hasRightElbow || _hasLeftShoulder || _hasRightShoulder || _hasTorsoLean;
        if (!anyJointTracked)
        {
            Debug.LogWarning("[MediaPipePoseTracker] CalibrateFromCurrentPose: no joints currently tracked -- " +
                "make sure you're visible in the camera first.", this);
            if (accuracyText != null) accuracyText.text = "Failed! Get into frame!";
            return false;
        }

        if (_hasLeftElbow) _target.leftElbow = _smoothedJoints.leftElbow;
        if (_hasRightElbow) _target.rightElbow = _smoothedJoints.rightElbow;
        if (_hasLeftShoulder) _target.leftShoulder = _smoothedJoints.leftShoulder;
        if (_hasRightShoulder) _target.rightShoulder = _smoothedJoints.rightShoulder;
        if (_hasTorsoLean) _target.torsoLean = _smoothedJoints.torsoLean;
        SaveCalibration(_target, "");

        // Fixed on-screen target dot: a snapshot of where each joint's live
        // position IS right now, not a recomputed angle-based estimate. Gated
        // on the raw "ever seen" landmark flags (not the angle-scoring _hasX
        // flags above), since a position only needs that one joint visible.
        if (_seenLElbow) { _fixedPosOpenLeftElbow = _livePosLeftElbow; _hasFixedOpenLeftElbow = true; }
        if (_seenRElbow) { _fixedPosOpenRightElbow = _livePosRightElbow; _hasFixedOpenRightElbow = true; }
        if (_seenLWrist) { _fixedPosOpenLeftWrist = _livePosLeftWrist; _hasFixedOpenLeftWrist = true; }
        if (_seenRWrist) { _fixedPosOpenRightWrist = _livePosRightWrist; _hasFixedOpenRightWrist = true; }
        SaveFixedPositions(
            _fixedPosOpenLeftElbow, _seenLElbow, _fixedPosOpenRightElbow, _seenRElbow,
            _fixedPosOpenLeftWrist, _seenLWrist, _fixedPosOpenRightWrist, _seenRWrist, "");

        if (accuracyText != null) accuracyText.text = "Done!";
        if (accuracyCheckmark != null) accuracyCheckmark.SetActive(true);
        Debug.Log("[MediaPipePoseTracker] Calibrated OPEN target from current live pose.", this);
        return true;
    }

    /// <summary>
    /// Same as CalibrateFromCurrentPose, but for the MID (second) state -- only
    /// meaningful for a pose with a genuine second held position (_hasMidTarget).
    /// The Calibrate Mid button is hidden for poses without one, but this guards
    /// against it being called anyway (e.g. a stale UnityEvent).
    /// </summary>
    public bool CalibrateMidFromCurrentPose()
    {
        if (!_hasMidTarget)
        {
            Debug.LogWarning("[MediaPipePoseTracker] CalibrateMidFromCurrentPose: this pose has no second state to calibrate.", this);
            // Must give the same on-screen feedback as the other failure path below:
            // this runs at the END of a 3-2-1 countdown the player just sat through,
            // so returning silently reads as the button being broken.
            if (accuracyText != null) accuracyText.text = "Failed! No second pose.";
            return false;
        }

        bool anyJointTracked = _hasLeftElbow || _hasRightElbow || _hasLeftShoulder || _hasRightShoulder || _hasTorsoLean;
        if (!anyJointTracked)
        {
            Debug.LogWarning("[MediaPipePoseTracker] CalibrateMidFromCurrentPose: no joints currently tracked -- " +
                "make sure you're visible in the camera first.", this);
            if (accuracyText != null) accuracyText.text = "Failed! Get into frame!";
            return false;
        }

        if (_hasLeftElbow) _targetMid.leftElbow = _smoothedJoints.leftElbow;
        if (_hasRightElbow) _targetMid.rightElbow = _smoothedJoints.rightElbow;
        if (_hasLeftShoulder) _targetMid.leftShoulder = _smoothedJoints.leftShoulder;
        if (_hasRightShoulder) _targetMid.rightShoulder = _smoothedJoints.rightShoulder;
        if (_hasTorsoLean) _targetMid.torsoLean = _smoothedJoints.torsoLean;
        SaveCalibration(_targetMid, "Mid");

        if (_seenLElbow) { _fixedPosMidLeftElbow = _livePosLeftElbow; _hasFixedMidLeftElbow = true; }
        if (_seenRElbow) { _fixedPosMidRightElbow = _livePosRightElbow; _hasFixedMidRightElbow = true; }
        if (_seenLWrist) { _fixedPosMidLeftWrist = _livePosLeftWrist; _hasFixedMidLeftWrist = true; }
        if (_seenRWrist) { _fixedPosMidRightWrist = _livePosRightWrist; _hasFixedMidRightWrist = true; }
        SaveFixedPositions(
            _fixedPosMidLeftElbow, _seenLElbow, _fixedPosMidRightElbow, _seenRElbow,
            _fixedPosMidLeftWrist, _seenLWrist, _fixedPosMidRightWrist, _seenRWrist, "Mid");

        if (accuracyText != null) accuracyText.text = "Done!";
        if (accuracyCheckmark != null) accuracyCheckmark.SetActive(true);
        Debug.Log("[MediaPipePoseTracker] Calibrated MID target from current live pose.", this);
        return true;
    }

    [Tooltip("Seconds shown per step of the 3-2-1 countdown before a Calibrate button actually captures the pose.")]
    public float calibrateCountdownStepSeconds = 1f;

    private Coroutine _calibrateCountdownCoroutine;

    /// <summary>
    /// UnityEvent-friendly wrapper for the Calibrate button. Runs a 3-2-1
    /// countdown (via accuracyText, same as the pre-pose countdown elsewhere in
    /// the game) so the player has a moment to settle into position before the
    /// actual capture, rather than calibrating whatever they happened to be
    /// doing the instant they clicked. Re-clicking either Calibrate button while
    /// a countdown is already running restarts it.
    /// </summary>
    public void CalibrateButtonClicked()
    {
        StartCalibrationCountdown(CalibrateFromCurrentPose, false);
    }

    /// <summary>UnityEvent-friendly wrapper for the Calibrate Mid button -- same countdown, captures the second state instead.</summary>
    public void CalibrateMidButtonClicked()
    {
        StartCalibrationCountdown(CalibrateMidFromCurrentPose, true);
    }

    /// <summary>
    /// Raised once a calibration attempt has actually finished: (wasMid, succeeded).
    /// The Calibrate*ButtonClicked methods start a 3-2-1 countdown coroutine and
    /// return immediately, so a caller that advances its own state on the call
    /// itself would advance ~3s early AND on a failed capture ("no joints
    /// tracked"). Anything sequencing calibration steps must key off this instead.
    /// </summary>
    public event System.Action<bool, bool> CalibrationFinished;

    private void StartCalibrationCountdown(System.Func<bool> onComplete, bool isMid)
    {
        if (_calibrateCountdownCoroutine != null) StopCoroutine(_calibrateCountdownCoroutine);
        _calibrateCountdownCoroutine = StartCoroutine(CalibrateCountdownRoutine(onComplete, isMid));
    }

    private IEnumerator CalibrateCountdownRoutine(System.Func<bool> onComplete, bool isMid)
    {
        if (accuracyCheckmark != null) accuracyCheckmark.SetActive(false); // clear any leftover checkmark from a prior calibration before this one runs

        // Realtime, not scaled: this countdown is UI feedback the player is waiting
        // on, and a paused/slowed Time.timeScale would stall it indefinitely.
        for (int i = 3; i > 0; i--)
        {
            if (accuracyText != null) accuracyText.text = i.ToString();
            yield return new WaitForSecondsRealtime(calibrateCountdownStepSeconds);
        }

        bool succeeded = onComplete(); // sets accuracyText + shows accuracyCheckmark on success, or a failure message on its own

        // Fired here, not in the button handler: this is the first moment the
        // outcome is actually known. Listeners advance their own step only on
        // success, so a failed capture leaves the player on the same step to retry.
        var finished = CalibrationFinished;
        if (finished != null) finished(isMid, succeeded);

        // "Done!"/failure text is transient feedback, not a persistent state --
        // revert back to the normal accuracy readout on its own instead of
        // sitting there until something else happens to overwrite it.
        yield return new WaitForSecondsRealtime(2f);
        if (accuracyCheckmark != null) accuracyCheckmark.SetActive(false);

        // Cleared BEFORE the text write so Update() is free to drive the readout
        // again from the very next frame (it suppresses its own write while a
        // countdown is in flight, so that the 3-2-1 is not overwritten per-frame).
        _calibrateCountdownCoroutine = null;

        // Only restore a live readout when one actually exists. Outside a tracking
        // session 'accuracy' is not being updated at all, so writing it here would
        // park a stale/zero "Accuracy: 0%" on the description panel.
        if (accuracyText != null)
            accuracyText.text = tracking ? "Accuracy: " + Mathf.RoundToInt(accuracy) + "%" : "";
    }

    public void StartTracking()
    {
        tracking = true;
        _accuracySamples.Clear();
        _sampleTimer = 0f;
        _hasSmoothedJoints = false;
        _hasSmLeftElbowLive = _hasSmRightElbowLive = _hasSmLeftWristLive = _hasSmRightWristLive = false;

        // Every session opens in the open state; PoseLoopRoutine drives it from here.
        ResetPosePhase();
    }

    public void StopTracking()
    {
        tracking = false;
        CalculateSessionResult();
    }

    private void Update()
    {
        // Before TryProcessResult, so this frame's score is graded against this
        // frame's blend rather than the previous frame's.
        AdvanceMidBlend();

        if (_hasPendingResult)
        {
            var result = _pendingResult;
            _hasPendingResult = false;
            TryProcessResult(result);
        }

        if (Time.unscaledTime - _diagLastLogTime > 1f)
        {
            _diagLastLogTime = Time.unscaledTime;
            Debug.Log($"[MediaPipePoseTracker DIAG] midBlend={_midBlend:F2} ({(_activeIsMid ? "MID" : "OPEN")}) " +
                $"resultsReceived={_diagResultsReceived} tracking={tracking} " +
                $"hasTarget={_hasTarget} lastFrame={_diagLastFailReason} rawAccuracy={_rawAccuracy:F0} accuracy={accuracy:F0}");
        }

        if (tracking)
        {
            accuracy = Mathf.Lerp(accuracy, _rawAccuracy, Time.deltaTime * smoothSpeed);

            // Suppressed while a calibration countdown owns the label: this write
            // runs every frame and would otherwise stomp the 3-2-1 / "Done!" text
            // the instant a Calibrate button is used during a live session.
            if (accuracyText != null && _calibrateCountdownCoroutine == null)
                accuracyText.text = "Accuracy: " + Mathf.RoundToInt(accuracy) + "%";

            _sampleTimer += Time.deltaTime;
            if (_sampleTimer >= sampleInterval)
            {
                _sampleTimer = 0f;
                _accuracySamples.Add(accuracy);
            }
        }

        // Runs even when not tracking: UpdateTargetCircles is what HIDES the dots
        // and rings (show = tracking && ...). Skipping it after StopTracking() left
        // the last frame's markers frozen on screen over the result panel.
        UpdateTargetCircles();
    }

    // Cached last-known-good positions, per canonical joint -- used so a
    // momentarily-unreliable point doesn't block angle math for OTHER joints
    // that don't depend on it (e.g. a shaky elbow shouldn't freeze hip-based
    // torso lean). Freshness (was THIS frame's reading usable?) is tracked
    // separately per point and is what actually gates whether a given SCORE
    // updates -- a joint's score holds its last value rather than the whole
    // frame being discarded, per the "hold last value" design principle.
    // Each point warms up independently the first time IT is individually seen
    // valid -- NOT gated on all 8 landing in the same frame at once, which in
    // practice (see live testing) can take arbitrarily long or never happen for
    // a persistently marginal point like a hip near the frame edge.
    private Vector3 _cLShoulder, _cRShoulder, _cLElbow, _cRElbow, _cLWrist, _cRWrist, _cLHip, _cRHip;
    private bool _seenLShoulder, _seenRShoulder, _seenLElbow, _seenRElbow, _seenLWrist, _seenRWrist, _seenLHip, _seenRHip;

    private void TryProcessResult(PoseLandmarkerResult result)
    {
        if (!_hasTarget) { _diagLastFailReason = "no target set (SetTargetPose not called / pose not baked)"; return; }

        if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0)
        {
            _hasLivePose = false;
            _diagLastFailReason = "poseWorldLandmarks empty (no person detected this frame)";
            return; // no person detected -- hold last accuracy, don't zero it
        }
        var worldLandmarks = result.poseWorldLandmarks[0].landmarks;
        if (worldLandmarks == null || worldLandmarks.Count < RequiredLandmarkCount)
        {
            _diagLastFailReason = $"only {(worldLandmarks == null ? 0 : worldLandmarks.Count)} landmarks (need {RequiredLandmarkCount})";
            return;
        }

        bool fLShoulder = TryGetPoint(worldLandmarks, LeftShoulder, out var lShoulder);
        bool fRShoulder = TryGetPoint(worldLandmarks, RightShoulder, out var rShoulder);
        bool fLElbow = TryGetPoint(worldLandmarks, LeftElbow, out var lElbow);
        bool fRElbow = TryGetPoint(worldLandmarks, RightElbow, out var rElbow);
        bool fLWrist = TryGetPoint(worldLandmarks, LeftWrist, out var lWrist);
        bool fRWrist = TryGetPoint(worldLandmarks, RightWrist, out var rWrist);
        bool fLHip = TryGetPoint(worldLandmarks, LeftHip, out var lHip);
        bool fRHip = TryGetPoint(worldLandmarks, RightHip, out var rHip);

        // Each point independently: use this frame's fresh value if valid,
        // otherwise fall back to whatever was last seen for THAT point. A point
        // never seen at all stays unresolved -- tracked via _seen*, not a single
        // all-8 gate, so one persistently marginal point (e.g. a hip near the
        // frame edge) can't block every other point from warming up.
        if (fLShoulder) { _cLShoulder = lShoulder; _seenLShoulder = true; } else if (_seenLShoulder) lShoulder = _cLShoulder;
        if (fRShoulder) { _cRShoulder = rShoulder; _seenRShoulder = true; } else if (_seenRShoulder) rShoulder = _cRShoulder;
        if (fLElbow) { _cLElbow = lElbow; _seenLElbow = true; } else if (_seenLElbow) lElbow = _cLElbow;
        if (fRElbow) { _cRElbow = rElbow; _seenRElbow = true; } else if (_seenRElbow) rElbow = _cRElbow;
        if (fLWrist) { _cLWrist = lWrist; _seenLWrist = true; } else if (_seenLWrist) lWrist = _cLWrist;
        if (fRWrist) { _cRWrist = rWrist; _seenRWrist = true; } else if (_seenRWrist) rWrist = _cRWrist;
        if (fLHip) { _cLHip = lHip; _seenLHip = true; } else if (_seenLHip) lHip = _cLHip;
        if (fRHip) { _cRHip = rHip; _seenRHip = true; } else if (_seenRHip) rHip = _cRHip;

        // No longer gated on all 8 points ever having been seen: each JOINT below
        // only needs what it actually depends on (per YogaJointAngles.Compute's
        // formulas). A hip that's genuinely never in frame (common for a
        // desk/laptop webcam) permanently blocks torso-lean and shoulder-angle
        // scoring, but must NOT block elbow scoring, which needs no hip data at
        // all. Compute() itself is safe to call with never-seen points still at
        // their Vector3 default -- those values just won't feed any score whose
        // _seen* gate below excludes them.
        var current = SmoothJoints(YogaJointAngles.Compute(lShoulder, rShoulder, lElbow, rElbow, lWrist, rWrist, lHip, rHip));

        // Poses with a genuine second held state (Open Arms <-> Closed Arms, etc.)
        // are scored against the state the EXERCISE PHASE is currently asking for
        // (see SetMidBlend / ResolveActiveTarget), not against whichever target the
        // player happens to be nearest.
        var activeTarget = ResolveActiveTarget();

        if (_seenLShoulder && _seenLElbow && _seenLWrist) { _scoreLeftElbow = Score(current.leftElbow, activeTarget.leftElbow, elbowTolerance); _hasLeftElbow = true; }
        if (_seenRShoulder && _seenRElbow && _seenRWrist) { _scoreRightElbow = Score(current.rightElbow, activeTarget.rightElbow, elbowTolerance); _hasRightElbow = true; }
        if (_seenLHip && _seenRHip && _seenLShoulder && _seenLElbow) { _scoreLeftShoulder = Score(current.leftShoulder, activeTarget.leftShoulder, shoulderTolerance); _hasLeftShoulder = true; }
        if (_seenLHip && _seenRHip && _seenRShoulder && _seenRElbow) { _scoreRightShoulder = Score(current.rightShoulder, activeTarget.rightShoulder, shoulderTolerance); _hasRightShoulder = true; }
        if (_seenLHip && _seenRHip && _seenLShoulder && _seenRShoulder) { _scoreTorsoLean = Score(current.torsoLean, activeTarget.torsoLean, torsoLeanTolerance); _hasTorsoLean = true; }

        if (!(_hasLeftElbow || _hasRightElbow || _hasLeftShoulder || _hasRightShoulder || _hasTorsoLean))
        {
            _diagLastFailReason = "warming up (no joint has enough points seen yet: " +
                $"{(!_seenLShoulder?"LSh ":"")}{(!_seenRShoulder?"RSh ":"")}{(!_seenLElbow?"LEl ":"")}{(!_seenRElbow?"REl ":"")}" +
                $"{(!_seenLWrist?"LWr ":"")}{(!_seenRWrist?"RWr ":"")}{(!_seenLHip?"LHi ":"")}{(!_seenRHip?"RHi":"")})";
            return; // literally nothing scoreable yet -- e.g. right at session start before even shoulders/elbows/wrists have been seen once
        }

        _diagLastFailReason = $"OK (scoring: {(_hasLeftElbow?"LEl ":"")}{(_hasRightElbow?"REl ":"")}{(_hasLeftShoulder?"LSh ":"")}{(_hasRightShoulder?"RSh ":"")}{(_hasTorsoLean?"Torso":"")}" +
            $"{(!_seenLHip || !_seenRHip ? " -- hips never seen, torso lean & shoulder angle scores unavailable" : "")})";

        float weightSum = (_hasLeftElbow ? elbowWeight : 0f) + (_hasRightElbow ? elbowWeight : 0f) +
            (_hasLeftShoulder ? shoulderWeight : 0f) + (_hasRightShoulder ? shoulderWeight : 0f) + (_hasTorsoLean ? torsoLeanWeight : 0f);
        _rawAccuracy = (
            _scoreLeftElbow * elbowWeight + _scoreRightElbow * elbowWeight +
            _scoreLeftShoulder * shoulderWeight + _scoreRightShoulder * shoulderWeight +
            _scoreTorsoLean * torsoLeanWeight
        ) / Mathf.Max(0.0001f, weightSum);

        _hasLivePose = true;

        // Screen-space (normalized 0-1 image coords) live positions for the solid
        // actual-position dots. The hollow circles no longer come from here --
        // they show the FIXED calibrated position instead (set directly in
        // CalibrateFromCurrentPose/CalibrateMidFromCurrentPose, read in
        // UpdateTargetCircles).
        UpdateLivePositions(result);
    }

    /// <summary>
    /// Drive which held state scoring evaluates against, from the exercise phase.
    /// Called by YogaManager.PoseLoopRoutine: 0 at the start of the open hold,
    /// 1 at the start of the mid hold, with the travel time passed as
    /// <paramref name="transitionSeconds"/> so the graded target sweeps across
    /// in step with the instructor instead of snapping. Pass 0 seconds to set it
    /// immediately.
    /// </summary>
    public void SetMidBlend(float target01, float transitionSeconds)
    {
        _midBlendFrom = _midBlend;
        _midBlendTo = Mathf.Clamp01(target01);
        _midBlendDuration = Mathf.Max(0f, transitionSeconds);
        _midBlendElapsed = 0f;

        if (_midBlendDuration <= 0f)
        {
            _midBlend = _midBlendTo;
            _activeIsMid = _midBlend >= 0.5f;
        }
    }

    /// <summary>Snap back to the open state with no travel. Used whenever a pose is (re)selected or a session starts, so a half-finished sweep from a previous run never leaks in.</summary>
    public void ResetPosePhase()
    {
        _midBlend = 0f;
        _midBlendFrom = 0f;
        _midBlendTo = 0f;
        _midBlendDuration = 0f;
        _midBlendElapsed = 0f;
        _activeIsMid = false;
    }

    // Advances an in-flight sweep. Kept in Update() rather than a coroutine so it
    // cannot outlive a disable/scene change or double-run if a phase is set twice.
    private void AdvanceMidBlend()
    {
        if (_midBlendDuration <= 0f || _midBlend == _midBlendTo) return;

        _midBlendElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_midBlendElapsed / _midBlendDuration);
        _midBlend = Mathf.Lerp(_midBlendFrom, _midBlendTo, Mathf.SmoothStep(0f, 1f, t));

        if (t >= 1f)
        {
            _midBlend = _midBlendTo;
            _midBlendDuration = 0f;
        }

        _activeIsMid = _midBlend >= 0.5f;
    }

    // The target scoring actually grades against this frame. A pose with no
    // gradable second state is always its open target; otherwise the two targets
    // are interpolated by the phase-driven blend, so a player mid-transition is
    // graded against where the routine expects them to be right now rather than
    // against whichever endpoint they happen to be nearer.
    private YogaJointAngles.JointAngles ResolveActiveTarget()
    {
        if (!_hasMidTarget || _midBlend <= 0f) return _target;
        if (_midBlend >= 1f) return _targetMid;

        return new YogaJointAngles.JointAngles
        {
            leftElbow = Mathf.Lerp(_target.leftElbow, _targetMid.leftElbow, _midBlend),
            rightElbow = Mathf.Lerp(_target.rightElbow, _targetMid.rightElbow, _midBlend),
            leftShoulder = Mathf.Lerp(_target.leftShoulder, _targetMid.leftShoulder, _midBlend),
            rightShoulder = Mathf.Lerp(_target.rightShoulder, _targetMid.rightShoulder, _midBlend),
            torsoLean = Mathf.Lerp(_target.torsoLean, _targetMid.torsoLean, _midBlend)
        };
    }

    // Exponential-moving-average toward each new reading, at a rate independent
    // of the final accuracy-number smoothing above (smoothSpeed) -- this one runs
    // early, before jitter can propagate into the score or the circle rotation
    // math below. First reading snaps immediately rather than ramping in from
    // zero/default.
    private YogaJointAngles.JointAngles SmoothJoints(YogaJointAngles.JointAngles raw)
    {
        if (!_hasSmoothedJoints)
        {
            _smoothedJoints = raw;
            _hasSmoothedJoints = true;
            return _smoothedJoints;
        }
        float t = ExpSmoothT(jointSmoothSpeed);
        _smoothedJoints.leftElbow = Mathf.Lerp(_smoothedJoints.leftElbow, raw.leftElbow, t);
        _smoothedJoints.rightElbow = Mathf.Lerp(_smoothedJoints.rightElbow, raw.rightElbow, t);
        _smoothedJoints.leftShoulder = Mathf.Lerp(_smoothedJoints.leftShoulder, raw.leftShoulder, t);
        _smoothedJoints.rightShoulder = Mathf.Lerp(_smoothedJoints.rightShoulder, raw.rightShoulder, t);
        _smoothedJoints.torsoLean = Mathf.Lerp(_smoothedJoints.torsoLean, raw.torsoLean, t);
        return _smoothedJoints;
    }

    // Same idea as SmoothJoints, applied to each marker's final screen position --
    // catches residual jitter from the live 2D landmark anchors (elbowL/wristL etc.)
    // that joint-angle smoothing alone doesn't reach, since those anchors feed
    // UpdateLivePositions() directly rather than through YogaJointAngles.Compute().
    private static Vector2 SmoothCirclePos(ref Vector2 current, Vector2 target, ref bool hasValue, float smoothSpeedDeg)
    {
        if (!hasValue)
        {
            current = target;
            hasValue = true;
            return current;
        }
        float t = ExpSmoothT(smoothSpeedDeg);
        current = Vector2.Lerp(current, target, t);
        return current;
    }

    // Framerate-independent smoothing factor for Lerp-toward-target: 1-e^(-speed*dt)
    // is the correct time-constant formula (matches a continuous exponential decay
    // at rate 'speed'), unlike the naive 'speed * Time.deltaTime' approximation,
    // which overshoots/clamps inconsistently at low framerate and settles at a
    // different effective rate depending on frame rate.
    private static float ExpSmoothT(float speed)
    {
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }

    // Screen-space (normalized 0-1 image coords) live positions for the solid
    // actual-position dots, smoothed the same way everything else here is.
    // These are also what CalibrateFromCurrentPose/CalibrateMidFromCurrentPose
    // snapshot into the fixed calibrated-target positions.
    private void UpdateLivePositions(PoseLandmarkerResult result)
    {
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0) return;
        var norm = result.poseLandmarks[0].landmarks;
        if (norm == null || norm.Count < RequiredLandmarkCount) return;

        Vector2 elbowL = new Vector2(norm[LeftElbow].x, norm[LeftElbow].y);
        Vector2 elbowR = new Vector2(norm[RightElbow].x, norm[RightElbow].y);
        Vector2 wristL = new Vector2(norm[LeftWrist].x, norm[LeftWrist].y);
        Vector2 wristR = new Vector2(norm[RightWrist].x, norm[RightWrist].y);

        _livePosLeftElbow = SmoothCirclePos(ref _livePosLeftElbow, elbowL, ref _hasSmLeftElbowLive, circleSmoothSpeed);
        _livePosRightElbow = SmoothCirclePos(ref _livePosRightElbow, elbowR, ref _hasSmRightElbowLive, circleSmoothSpeed);
        _livePosLeftWrist = SmoothCirclePos(ref _livePosLeftWrist, wristL, ref _hasSmLeftWristLive, circleSmoothSpeed);
        _livePosRightWrist = SmoothCirclePos(ref _livePosRightWrist, wristR, ref _hasSmRightWristLive, circleSmoothSpeed);
    }

    private bool TryGetPoint(List<Landmark> landmarks, int index, out Vector3 point)
    {
        var lm = landmarks[index];
        // 'presence' deliberately not gated here: live-tested against this project's
        // BlazePoseFull model, presence sits persistently low (often <0.1) even for
        // landmarks 'visibility' scores as clearly tracked (0.3-0.9) -- gating on it
        // blocked nearly every frame. 'visibility' alone is the reliable signal here.
        if (lm.visibility.HasValue && lm.visibility.Value < minLandmarkConfidence) { point = default; return false; }
        point = new Vector3(lm.x, lm.y, lm.z);
        return true;
    }

    // TEMPORARY diagnostic helper -- reports which landmark failed and why.
    private static string DiagLowConfidence(string jointName, List<Landmark> landmarks, int index)
    {
        var lm = landmarks[index];
        return $"{jointName} low confidence (visibility={lm.visibility}, presence={lm.presence})";
    }

    private static float Score(float current, float target, float tolerance)
    {
        float diff = Mathf.Abs(current - target);
        return Mathf.Clamp(100f * (1f - diff / Mathf.Max(1f, tolerance)), 0f, 100f);
    }

    private void UpdateTargetCircles()
    {
        bool show = tracking && _hasLivePose && targetSpace != null;

        // The hollow red circles now show the FIXED calibrated position (static,
        // from whichever state -- open/mid -- the exercise phase currently asks
        // for, see SetMidBlend), not a live rotation-math estimate. A joint only
        // shows once it actually has a saved calibration for that state; there
        // is no more "moving target" fallback for an uncalibrated pose.
        Vector2 fixedLeftElbow = _activeIsMid ? _fixedPosMidLeftElbow : _fixedPosOpenLeftElbow;
        Vector2 fixedRightElbow = _activeIsMid ? _fixedPosMidRightElbow : _fixedPosOpenRightElbow;
        Vector2 fixedLeftWrist = _activeIsMid ? _fixedPosMidLeftWrist : _fixedPosOpenLeftWrist;
        Vector2 fixedRightWrist = _activeIsMid ? _fixedPosMidRightWrist : _fixedPosOpenRightWrist;
        bool hasFixedLeftElbow = _activeIsMid ? _hasFixedMidLeftElbow : _hasFixedOpenLeftElbow;
        bool hasFixedRightElbow = _activeIsMid ? _hasFixedMidRightElbow : _hasFixedOpenRightElbow;
        bool hasFixedLeftWrist = _activeIsMid ? _hasFixedMidLeftWrist : _hasFixedOpenLeftWrist;
        bool hasFixedRightWrist = _activeIsMid ? _hasFixedMidRightWrist : _hasFixedOpenRightWrist;

        SetCircle(leftElbowCircle, show && hasFixedLeftElbow, fixedLeftElbow, 0f, FixedTargetToleranceEquivalent);
        SetCircle(rightElbowCircle, show && hasFixedRightElbow, fixedRightElbow, 0f, FixedTargetToleranceEquivalent);
        SetCircle(leftWristCircle, show && hasFixedLeftWrist, fixedLeftWrist, 0f, FixedTargetToleranceEquivalent);
        SetCircle(rightWristCircle, show && hasFixedRightWrist, fixedRightWrist, 0f, FixedTargetToleranceEquivalent);

        SetDot(leftElbowDot, show, _livePosLeftElbow);
        SetDot(rightElbowDot, show, _livePosRightElbow);
        SetDot(leftWristDot, show, _livePosLeftWrist);
        SetDot(rightWristDot, show, _livePosRightWrist);
    }

    // SetCircle sizes its radius from a "tolerance" value (Mathf.Clamp(tol*2, 20,
    // 120)) originally meant for the old live rotation-math circle's
    // accuracy-zone sizing. The fixed dot isn't a zone, just a pinpoint marker --
    // this constant picks a fixed 44px radius (88x88 -- matches the solid dot's
    // size) via the same code path, rather than duplicating the sizing logic.
    private const float FixedTargetToleranceEquivalent = 22f;

    private void SetDot(ActualPositionDotView dot, bool show, Vector2 normPos)
    {
        if (dot == null) return;
        dot.SetVisible(show);
        if (!show) return;

        float w = targetSpace.rect.width;
        float h = targetSpace.rect.height;
        Vector2 local = new Vector2((normPos.x - 0.5f) * w, (0.5f - normPos.y) * h);
        dot.SetPosition(local);
    }

    private void SetCircle(TargetCircleView circle, bool show, Vector2 normPos, float score, float toleranceDeg)
    {
        if (circle == null) return;
        circle.SetVisible(show);
        if (!show) return;

        float w = targetSpace.rect.width;
        float h = targetSpace.rect.height;
        Vector2 local = new Vector2((normPos.x - 0.5f) * w, (0.5f - normPos.y) * h);
        float radius = Mathf.Clamp(toleranceDeg * 2f, 20f, 120f);
        circle.SetState(local, radius, score);
    }

    private void CalculateSessionResult()
    {
        if (_accuracySamples.Count == 0)
        {
            alignment = accuracy;
            steadiness = 100f;
            return;
        }

        float sum = 0f;
        foreach (float sample in _accuracySamples) sum += sample;
        float mean = sum / _accuracySamples.Count;

        float variance = 0f;
        foreach (float sample in _accuracySamples) variance += (sample - mean) * (sample - mean);
        variance /= _accuracySamples.Count;
        float stdDev = Mathf.Sqrt(variance);

        alignment = mean;
        steadiness = Mathf.Clamp(100f - (stdDev / 30f) * 100f, 0f, 100f);
    }
}
