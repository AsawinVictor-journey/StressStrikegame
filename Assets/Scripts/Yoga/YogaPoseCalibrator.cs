using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Dev-time tool to CALIBRATE yoga pose targets from the real glove instead of
/// hand-typing Euler guesses (which is why scoring was off — the targets were
/// placeholders like (0,0,0) / (3,3,3)).
///
/// How to use (Editor, Play mode, glove on):
///   1. Sit/stand in your NEUTRAL pose (arms relaxed, facing the screen) and
///      press the Recenter key. This sets the reference frame.
///   2. Move into each pose in turn and press that pose's capture key. The
///      glove's current orientation (relative to neutral) is written into the
///      pose's targetArmRotation and saved to the asset immediately.
///   3. Repeat for every pose, then remove/disable this component for the build.
///
/// Gameplay recenters to the SAME neutral at the start of each session (see
/// YogaManager), so the captured targets line up for every player and session.
/// </summary>
public class YogaPoseCalibrator : MonoBehaviour
{
    [Tooltip("The scene's YogaTracker.")]
    public YogaTracker tracker;

    [Tooltip("The pose assets to calibrate, in the same order as Capture Keys.")]
    public YogaPose[] poses;

    [Header("Hotkeys")]
    [Tooltip("Hold your neutral pose, then press this to set the reference frame.")]
    public KeyCode recenterKey = KeyCode.R;

    [Tooltip("One capture key per pose (same order as 'poses'). Hold the pose, press the key.")]
    public KeyCode[] captureKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };

    void Update()
    {
        if (tracker == null) return;

        if (Input.GetKeyDown(recenterKey))
        {
            bool ok = tracker.Recenter();
            Debug.Log(ok
                ? "[YogaCalibrator] Recentered — neutral reference set. Now capture each pose."
                : "[YogaCalibrator] Recenter failed — no glove connected.", this);
        }

        int count = Mathf.Min(poses.Length, captureKeys.Length);
        for (int i = 0; i < count; i++)
        {
            if (poses[i] != null && Input.GetKeyDown(captureKeys[i]))
                Capture(poses[i]);
        }
    }

    void Capture(YogaPose pose)
    {
        Vector3 euler = tracker.CurrentRecentered.eulerAngles;
        pose.targetArmRotation = euler;
        Debug.Log($"[YogaCalibrator] Captured '{pose.name}' target = {euler}", pose);

#if UNITY_EDITOR
        EditorUtility.SetDirty(pose);
        AssetDatabase.SaveAssets();
#endif
    }
}
