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

    private readonly List<float> _accuracySamples = new List<float>();
    private float _sampleTimer;

    private YogaJointAngles.JointAngles _target;
    private bool _hasTarget;

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
    private Vector2 _targetPosLeftElbow, _targetPosRightElbow, _targetPosLeftWrist, _targetPosRightWrist;
    private bool _hasTargetPositions;

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

    public void SetTargetPose(YogaPose pose)
    {
        if (pose == null)
        {
            _hasTarget = false;
            return;
        }
        if (!pose.hasMediaPipeTarget)
        {
            Debug.LogWarning($"[MediaPipePoseTracker] '{pose.name}' has no baked MediaPipe target " +
                "(run Tools > Yoga > Bake MediaPipe Target For Selected Pose first) -- accuracy will stay 0.", pose);
            _hasTarget = false;
            return;
        }

        _target = new YogaJointAngles.JointAngles
        {
            leftElbow = pose.targetLeftElbowAngle,
            rightElbow = pose.targetRightElbowAngle,
            leftShoulder = pose.targetLeftShoulderAngle,
            rightShoulder = pose.targetRightShoulderAngle,
            torsoLean = pose.targetTorsoLean
        };
        _hasTarget = true;
    }

    public void StartTracking()
    {
        tracking = true;
        _accuracySamples.Clear();
        _sampleTimer = 0f;
    }

    public void StopTracking()
    {
        tracking = false;
        CalculateSessionResult();
    }

    private void Update()
    {
        if (_hasPendingResult)
        {
            var result = _pendingResult;
            _hasPendingResult = false;
            TryProcessResult(result);
        }

        if (Time.unscaledTime - _diagLastLogTime > 1f)
        {
            _diagLastLogTime = Time.unscaledTime;
            Debug.Log($"[MediaPipePoseTracker DIAG] resultsReceived={_diagResultsReceived} tracking={tracking} " +
                $"hasTarget={_hasTarget} lastFrame={_diagLastFailReason} rawAccuracy={_rawAccuracy:F0} accuracy={accuracy:F0}");
        }

        if (!tracking) return;

        accuracy = Mathf.Lerp(accuracy, _rawAccuracy, Time.deltaTime * smoothSpeed);

        if (accuracyText != null)
            accuracyText.text = "Accuracy: " + Mathf.RoundToInt(accuracy) + "%";

        _sampleTimer += Time.deltaTime;
        if (_sampleTimer >= sampleInterval)
        {
            _sampleTimer = 0f;
            _accuracySamples.Add(accuracy);
        }

        UpdateTargetCircles();
    }

    // Cached last-known-good positions, per canonical joint -- used so a
    // momentarily-unreliable point doesn't block angle math for OTHER joints
    // that don't depend on it (e.g. a shaky elbow shouldn't freeze hip-based
    // torso lean). Freshness (was THIS frame's reading usable?) is tracked
    // separately per point and is what actually gates whether a given SCORE
    // updates -- a joint's score holds its last value rather than the whole
    // frame being discarded, per the "hold last value" design principle.
    private Vector3 _cLShoulder, _cRShoulder, _cLElbow, _cRElbow, _cLWrist, _cRWrist, _cLHip, _cRHip;
    private bool _hasCache;

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

        // Fill in gaps with the last known-good position so Compute() always has
        // a full 8-point set to work with; only the joints whose OWN required
        // points are fresh this frame get their score updated below.
        if (_hasCache)
        {
            if (!fLShoulder) lShoulder = _cLShoulder; if (!fRShoulder) rShoulder = _cRShoulder;
            if (!fLElbow) lElbow = _cLElbow; if (!fRElbow) rElbow = _cRElbow;
            if (!fLWrist) lWrist = _cLWrist; if (!fRWrist) rWrist = _cRWrist;
            if (!fLHip) lHip = _cLHip; if (!fRHip) rHip = _cRHip;
        }
        else if (!(fLShoulder && fRShoulder && fLElbow && fRElbow && fLWrist && fRWrist && fLHip && fRHip))
        {
            // No cache yet and at least one point missing -- nothing usable to fall back to.
            _diagLastFailReason = "warming up (waiting for first fully-tracked frame)";
            return;
        }

        _cLShoulder = lShoulder; _cRShoulder = rShoulder;
        _cLElbow = lElbow; _cRElbow = rElbow;
        _cLWrist = lWrist; _cRWrist = rWrist;
        _cLHip = lHip; _cRHip = rHip;
        _hasCache = true;

        var current = YogaJointAngles.Compute(lShoulder, rShoulder, lElbow, rElbow, lWrist, rWrist, lHip, rHip);

        if (fLShoulder && fLElbow && fLWrist) _scoreLeftElbow = Score(current.leftElbow, _target.leftElbow, elbowTolerance);
        if (fRShoulder && fRElbow && fRWrist) _scoreRightElbow = Score(current.rightElbow, _target.rightElbow, elbowTolerance);
        if (fLHip && fRHip && fLShoulder && fLElbow) _scoreLeftShoulder = Score(current.leftShoulder, _target.leftShoulder, shoulderTolerance);
        if (fLHip && fRHip && fRShoulder && fRElbow) _scoreRightShoulder = Score(current.rightShoulder, _target.rightShoulder, shoulderTolerance);
        if (fLHip && fRHip && fLShoulder && fRShoulder) _scoreTorsoLean = Score(current.torsoLean, _target.torsoLean, torsoLeanTolerance);

        _diagLastFailReason = $"OK (fresh: {(fLShoulder?"LSh ":"")}{(fRShoulder?"RSh ":"")}{(fLElbow?"LEl ":"")}{(fRElbow?"REl ":"")}{(fLWrist?"LWr ":"")}{(fRWrist?"RWr ":"")}{(fLHip?"LHi ":"")}{(fRHip?"RHi":"")})";

        float weightSum = elbowWeight * 2f + shoulderWeight * 2f + torsoLeanWeight;
        _rawAccuracy = (
            _scoreLeftElbow * elbowWeight + _scoreRightElbow * elbowWeight +
            _scoreLeftShoulder * shoulderWeight + _scoreRightShoulder * shoulderWeight +
            _scoreTorsoLean * torsoLeanWeight
        ) / Mathf.Max(0.0001f, weightSum);

        _hasLivePose = true;

        // Screen-space (normalized 0-1 image coords) for the target circles --
        // deliberately from poseLandmarks, not poseWorldLandmarks: angle math and
        // on-screen placement are kept as separate concerns even though they share
        // a source frame (see design notes).
        UpdateTargetPositions(result, current);
    }

    private void UpdateTargetPositions(PoseLandmarkerResult result, YogaJointAngles.JointAngles current)
    {
        _hasTargetPositions = false;
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0) return;
        var norm = result.poseLandmarks[0].landmarks;
        if (norm == null || norm.Count < RequiredLandmarkCount) return;

        Vector2 shoulderL = new Vector2(norm[LeftShoulder].x, norm[LeftShoulder].y);
        Vector2 shoulderR = new Vector2(norm[RightShoulder].x, norm[RightShoulder].y);
        Vector2 elbowL = new Vector2(norm[LeftElbow].x, norm[LeftElbow].y);
        Vector2 elbowR = new Vector2(norm[RightElbow].x, norm[RightElbow].y);
        Vector2 wristL = new Vector2(norm[LeftWrist].x, norm[LeftWrist].y);
        Vector2 wristR = new Vector2(norm[RightWrist].x, norm[RightWrist].y);

        // Target position = the player's OWN live anchor + own live limb length,
        // rotated by (target angle - current angle) from the instructor. Angle
        // comes from the instructor; position/scale comes from the player's own
        // live geometry -- deliberately not a reprojection of the instructor's
        // Unity world position (see design notes' explicit clarification on this).
        // This is a simplified 2D heuristic, not a rigorous 3D reprojection: at
        // score=100 (delta=0) the target exactly coincides with the live point,
        // which is the important convergence property for an MVP; the rotational
        // direction for partial mismatches is an approximation to be checked
        // visually, not a precise IK solve.
        _targetPosLeftElbow = shoulderL + RotateBy(elbowL - shoulderL, _target.leftShoulder - current.leftShoulder);
        _targetPosRightElbow = shoulderR + RotateBy(elbowR - shoulderR, _target.rightShoulder - current.rightShoulder);
        _targetPosLeftWrist = elbowL + RotateBy(wristL - elbowL, _target.leftElbow - current.leftElbow);
        _targetPosRightWrist = elbowR + RotateBy(wristR - elbowR, _target.rightElbow - current.rightElbow);
        _hasTargetPositions = true;
    }

    private static Vector2 RotateBy(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
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
        bool show = tracking && _hasLivePose && _hasTargetPositions && targetSpace != null;

        SetCircle(leftElbowCircle, show, _targetPosLeftElbow, _scoreLeftElbow, elbowTolerance);
        SetCircle(rightElbowCircle, show, _targetPosRightElbow, _scoreRightElbow, elbowTolerance);
        SetCircle(leftWristCircle, show, _targetPosLeftWrist, _scoreLeftElbow, elbowTolerance);
        SetCircle(rightWristCircle, show, _targetPosRightWrist, _scoreRightElbow, elbowTolerance);
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
