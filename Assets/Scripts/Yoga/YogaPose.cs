using UnityEngine;

[CreateAssetMenu(fileName = "New Yoga Pose", menuName = "Yoga/Pose")]
public class YogaPose : ScriptableObject
{
    [TextArea]
    public string description;
    public Sprite icon;
    public AnimationClip transitionAnimation;
    public AnimationClip poseAnimation;
    public float duration = 30f;
    public int difficulty = 1;

    [Header("BNO055 Target")]
    public Vector3 targetArmRotation;
}