using UnityEngine;

[CreateAssetMenu(fileName = "New Yoga Pose", menuName = "Yoga/Pose")]
public class YogaPose : ScriptableObject
{
    [TextArea]
    public string description;
    public Sprite icon;
    public AnimationClip transitionAnimation;
    public AnimationClip reverseTransitionAnimation;
    public AnimationClip poseAnimation;
    public AnimationClip MidPoseAnimation;
    public float duration = 30f;
    public int difficulty = 1;

    [Header("Cycle Timing")]
    // Per-pose rhythm, measured from that pose's reference footage. Different
    // exercises move at very different speeds -- the open/close arm swing runs a
    // ~9.3s cycle while the side bend is closer to twice that -- so timing lives
    // on the pose rather than on YogaManager.
    // Leave any value at 0 to fall back to the YogaManager default.
    [Tooltip("Seconds held in the main pose. 0 = use YogaManager's value.")]
    public float openHoldDuration;

    [Tooltip("Seconds held in the mid pose. 0 = use YogaManager's value.")]
    public float closedHoldDuration;

    [Tooltip("Seconds to travel from the main pose to the mid pose. 0 = use YogaManager's value.")]
    public float toClosedDuration;

    [Tooltip("Seconds to travel back from the mid pose to the main pose. 0 = use YogaManager's value.")]
    public float toOpenDuration;

    [Tooltip("Seconds of blend between clips. 0 = use YogaManager's value.")]
    public float blendDuration;

    [Tooltip("How many out-and-back cycles this pose runs. 0 = use YogaManager's value.")]
    public int midPoseCycles;

    [Header("BNO055 Target")]
    public Vector3 targetArmRotation;

    [Header("MediaPipe Target (upper body, baked from instructor rig)")]
    [Tooltip("Set by Tools > Yoga > Bake MediaPipe Target For Selected Pose. False until baked -- " +
             "MediaPipePoseTracker refuses to score against an un-baked pose rather than silently using zeros.")]
    public bool hasMediaPipeTarget;
    public float targetLeftElbowAngle;
    public float targetRightElbowAngle;
    public float targetLeftShoulderAngle;
    public float targetRightShoulderAngle;
    public float targetTorsoLean;

    [Header("MediaPipe Target - Mid Pose (only baked/used if MidPoseAnimation is set)")]
    [Tooltip("Set alongside the target above when this pose has a MidPoseAnimation. MediaPipePoseTracker " +
             "scores against whichever of the two states (open vs mid) the player is currently closer to, " +
             "so a pose with a genuine second position (e.g. Open Arms <-> Closed Arms) gets both counted " +
             "instead of only ever grading the open state.")]
    public bool hasMediaPipeMidTarget;
    public float targetLeftElbowAngleMid;
    public float targetRightElbowAngleMid;
    public float targetLeftShoulderAngleMid;
    public float targetRightShoulderAngleMid;
    public float targetTorsoLeanMid;

    [Tooltip("OPT-IN: tick only when MidPoseAnimation is a genuine second HELD position that the " +
             "player is meant to be graded on (Open Arms <-> Closed Arms). Leave off when it is just " +
             "a transition/rest clip -- the baker fills the Mid angles in for any pose that has a " +
             "MidPoseAnimation at all, and grading against a rest clip lets the player score ~100% " +
             "by standing in the rest position instead of performing the pose.")]
    public bool gradeMidPose;

    /// <summary>
    /// Single source of truth for "this pose has a second state worth scoring/calibrating against".
    /// Both MediaPipePoseTracker (scoring + Calibrate Mid) and YogaManager (Calibrate-Mid button
    /// visibility) must agree, or the button shows for poses the tracker will refuse to calibrate.
    /// </summary>
    public bool HasGradableMidPose
    {
        get { return hasMediaPipeMidTarget && gradeMidPose && MidPoseAnimation != null; }
    }
}
