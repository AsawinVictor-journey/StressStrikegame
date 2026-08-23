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
    [Range(0f, 1f)] public float minLandmarkConfidence = 0.5f;
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
    }

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

    private void TryProcessResult(PoseLandmarkerResult result)
    {
        if (!_hasTarget) return; // nothing to compare against yet

        if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0)
        {
            _hasLivePose = false;
            return; // no person detected -- hold last accuracy, don't zero it
        }
        var worldLandmarks = result.poseWorldLandmarks[0].landmarks;
        if (worldLandmarks == null || worldLandmarks.Count < RequiredLandmarkCount) return;

        if (!TryGetPoint(worldLandmarks, LeftShoulder, out var lShoulder)) return;
        if (!TryGetPoint(worldLandmarks, RightShoulder, out var rShoulder)) return;
        if (!TryGetPoint(worldLandmarks, LeftElbow, out var lElbow)) return;
        if (!TryGetPoint(worldLandmarks, RightElbow, out var rElbow)) return;
        if (!TryGetPoint(worldLandmarks, LeftWrist, out var lWrist)) return;
        if (!TryGetPoint(worldLandmarks, RightWrist, out var rWrist)) return;
        if (!TryGetPoint(worldLandmarks, LeftHip, out var lHip)) return;
        if (!TryGetPoint(worldLandmarks, RightHip, out var rHip)) return;

        var current = YogaJointAngles.Compute(lShoulder, rShoulder, lElbow, rElbow, lWrist, rWrist, lHip, rHip);

        _scoreLeftElbow = Score(current.leftElbow, _target.leftElbow, elbowTolerance);
        _scoreRightElbow = Score(current.rightElbow, _target.rightElbow, elbowTolerance);
        _scoreLeftShoulder = Score(current.leftShoulder, _target.leftShoulder, shoulderTolerance);
        _scoreRightShoulder = Score(current.rightShoulder, _target.rightShoulder, shoulderTolerance);
        _scoreTorsoLean = Score(current.torsoLean, _target.torsoLean, torsoLeanTolerance);

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
        if (lm.visibility.HasValue && lm.visibility.Value < minLandmarkConfidence) { point = default; return false; }
        if (lm.presence.HasValue && lm.presence.Value < minLandmarkConfidence) { point = default; return false; }
        point = new Vector3(lm.x, lm.y, lm.z);
        return true;
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
