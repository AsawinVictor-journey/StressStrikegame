using System.Reflection;
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
}