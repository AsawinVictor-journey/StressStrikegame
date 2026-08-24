using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Pure geometry for the MediaPipe-based Yoga pose evaluation (upper-body MVP,
/// Prayer pose). Turns 8 canonical joint world positions into 5 named joint
/// angles. Used identically by two callers so both sides are guaranteed
/// comparable even though their source coordinate systems are unrelated:
///   - MediaPipePoseTracker (player, positions from PoseLandmarkerResult.poseWorldLandmarks)
///   - BakeInstructorTarget below (instructor rig, positions from ORG- bone Transforms)
/// Only the derived angles are ever compared -- raw positions from the two
/// sources are never compared directly against each other.
/// </summary>
public static class YogaJointAngles
{
    public struct JointAngles
    {
        public float leftElbow;
        public float rightElbow;
        public float leftShoulder;
        public float rightShoulder;
        public float torsoLean;
    }

    /// <summary>
    /// Computes the 5 MVP joint angles from 8 canonical joint world positions.
    /// Elbow/shoulder angles are unsigned (0-180 deg, Vector3.Angle). Torso lean
    /// is signed (negative = left lean, positive = right lean), derived from the
    /// subject's own shoulder-to-shoulder axis (not hip-to-hip -- see TorsoLean)
    /// so it doesn't depend on any assumed camera/world orientation beyond
    /// world-up.
    /// </summary>
    public static JointAngles Compute(
        Vector3 leftShoulder, Vector3 rightShoulder,
        Vector3 leftElbow, Vector3 rightElbow,
        Vector3 leftWrist, Vector3 rightWrist,
        Vector3 leftHip, Vector3 rightHip)
    {
        var hipMid = (leftHip + rightHip) * 0.5f;
        var shoulderMid = (leftShoulder + rightShoulder) * 0.5f;

        return new JointAngles
        {
            leftElbow = ElbowFlexion(leftShoulder, leftElbow, leftWrist),
            rightElbow = ElbowFlexion(rightShoulder, rightElbow, rightWrist),
            leftShoulder = ShoulderAngle(hipMid, leftShoulder, leftElbow),
            rightShoulder = ShoulderAngle(hipMid, rightShoulder, rightElbow),
            torsoLean = TorsoLean(hipMid, shoulderMid, leftShoulder, rightShoulder)
        };
    }

    // Angle at the elbow between (elbow->shoulder) and (elbow->wrist).
    // 0 = fully bent (e.g. Prayer), 180 = fully straight (e.g. RaiseArms/OpenArms).
    // Unsigned -- flexion has one anatomical bend direction, no meaningful sign.
    private static float ElbowFlexion(Vector3 shoulder, Vector3 elbow, Vector3 wrist)
    {
        return Vector3.Angle(shoulder - elbow, wrist - elbow);
    }

    // Angle at the shoulder between the torso-up vector (hipMid->shoulder) and
    // the upper-arm vector (shoulder->elbow). ~0 = arm down at the side,
    // ~180 = arm raised overhead, ~90 = arm out to the side.
    // Unsigned -- known simplification: captures elevation only, not azimuth,
    // so it can't by itself distinguish a forward-raise from a side-raise at the
    // same elevation. Acceptable for the current 6-pose set (see design notes);
    // flag if a future pose needs that distinction.
    private static float ShoulderAngle(Vector3 hipMid, Vector3 shoulder, Vector3 elbow)
    {
        return Vector3.Angle(shoulder - hipMid, elbow - shoulder);
    }

    // Signed angle of the torso (hipMid->shoulderMid) away from world-up,
    // measured around the subject's own forward axis (derived from their
    // hip-to-hip lateral axis, not an assumed fixed camera axis). Negative =
    // left lean, positive = right lean, 0 = upright. Must be signed -- an
    // unsigned lean can't distinguish SideBendLeft from SideBendRight.
    private static float TorsoLean(Vector3 hipMid, Vector3 shoulderMid, Vector3 leftShoulder, Vector3 rightShoulder)
    {
        var torsoVec = (shoulderMid - hipMid).normalized;
        // Lateral axis from the SHOULDERS, not the hips: live-tested against the
        // instructor rig, ORG-pelvis.L/R turned out to sit at the exact same
        // world position (never separated laterally in this rig), which made a
        // hip-based lateral vector zero/degenerate and forced torsoLean to 0 for
        // every pose, including genuine side bends. Shoulders are reliably
        // separated on both the rig and real MediaPipe landmarks.
        var lateral = (rightShoulder - leftShoulder).normalized;
        var forward = Vector3.Cross(lateral, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.0001f)
            return 0f; // degenerate (shoulders coincident, or lateral axis parallel to world-up) -- hold neutral rather than NaN

        return Vector3.SignedAngle(Vector3.up, torsoVec, forward);
    }

#if UNITY_EDITOR
    private const string InstructorRigObjectName = "AGuyReworked2";

    // Rigify ORG- bones = canonical, undeformed reference bones (a bone's head
    // position is the joint it originates from) -- confirmed present by these
    // exact names in the live rig hierarchy. ORG-upper_arm (not ORG-shoulder,
    // which is the clavicle) is the true shoulder/glenohumeral joint.
    private const string BoneLeftShoulder = "ORG-upper_arm.L";
    private const string BoneRightShoulder = "ORG-upper_arm.R";
    private const string BoneLeftElbow = "ORG-forearm.L";
    private const string BoneRightElbow = "ORG-forearm.R";
    private const string BoneLeftWrist = "ORG-hand.L";
    private const string BoneRightWrist = "ORG-hand.R";
    private const string BoneLeftHip = "ORG-pelvis.L";
    private const string BoneRightHip = "ORG-pelvis.R";

    /// <summary>
    /// Samples 'pose.poseAnimation' at t=0 on the instructor rig, reads the 8
    /// canonical joint bone positions, computes the 5 target angles via the same
    /// Compute() the runtime tracker uses, and bakes them into the pose asset.
    /// Non-destructive to the open scene -- uses AnimationMode so the rig's
    /// current pose is restored afterward.
    /// </summary>
    public static bool BakeInstructorTarget(YogaPose pose, GameObject rigRoot)
    {
        if (pose == null || rigRoot == null)
        {
            Debug.LogError("[YogaJointAngles] BakeInstructorTarget: pose or rigRoot is null.");
            return false;
        }
        if (pose.poseAnimation == null)
        {
            Debug.LogError($"[YogaJointAngles] '{pose.name}' has no poseAnimation clip assigned.");
            return false;
        }

        var leftShoulder = FindDeepChild(rigRoot.transform, BoneLeftShoulder);
        var rightShoulder = FindDeepChild(rigRoot.transform, BoneRightShoulder);
        var leftElbow = FindDeepChild(rigRoot.transform, BoneLeftElbow);
        var rightElbow = FindDeepChild(rigRoot.transform, BoneRightElbow);
        var leftWrist = FindDeepChild(rigRoot.transform, BoneLeftWrist);
        var rightWrist = FindDeepChild(rigRoot.transform, BoneRightWrist);
        var leftHip = FindDeepChild(rigRoot.transform, BoneLeftHip);
        var rightHip = FindDeepChild(rigRoot.transform, BoneRightHip);

        if (leftShoulder == null || rightShoulder == null || leftElbow == null || rightElbow == null ||
            leftWrist == null || rightWrist == null || leftHip == null || rightHip == null)
        {
            Debug.LogError($"[YogaJointAngles] Bake aborted for '{pose.name}' -- one or more bones not found under '{rigRoot.name}'. " +
                $"L-shoulder:{leftShoulder != null} R-shoulder:{rightShoulder != null} " +
                $"L-elbow:{leftElbow != null} R-elbow:{rightElbow != null} " +
                $"L-wrist:{leftWrist != null} R-wrist:{rightWrist != null} " +
                $"L-hip:{leftHip != null} R-hip:{rightHip != null}");
            return false;
        }

        JointAngles angles;
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(rigRoot, pose.poseAnimation, 0f);
            AnimationMode.EndSampling();

            angles = Compute(
                leftShoulder.position, rightShoulder.position,
                leftElbow.position, rightElbow.position,
                leftWrist.position, rightWrist.position,
                leftHip.position, rightHip.position);
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        pose.targetLeftElbowAngle = angles.leftElbow;
        pose.targetRightElbowAngle = angles.rightElbow;
        pose.targetLeftShoulderAngle = angles.leftShoulder;
        pose.targetRightShoulderAngle = angles.rightShoulder;
        pose.targetTorsoLean = angles.torsoLean;
        pose.hasMediaPipeTarget = true;

        EditorUtility.SetDirty(pose);
        AssetDatabase.SaveAssets();

        Debug.Log($"[YogaJointAngles] Baked '{pose.name}': L-elbow={angles.leftElbow:F1} R-elbow={angles.rightElbow:F1} " +
            $"L-shoulder={angles.leftShoulder:F1} R-shoulder={angles.rightShoulder:F1} torsoLean={angles.torsoLean:F1}", pose);
        return true;
    }

    [MenuItem("Tools/Yoga/Bake MediaPipe Target For Selected Pose")]
    private static void BakeSelectedPoseMenuItem()
    {
        var pose = Selection.activeObject as YogaPose;
        if (pose == null)
        {
            Debug.LogError("[YogaJointAngles] Select a YogaPose asset in the Project window first.");
            return;
        }
        var rigRoot = GameObject.Find(InstructorRigObjectName);
        if (rigRoot == null)
        {
            Debug.LogError($"[YogaJointAngles] Could not find '{InstructorRigObjectName}' in the open scene -- open Yoga.unity first.");
            return;
        }
        BakeInstructorTarget(pose, rigRoot);
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
#endif
}
