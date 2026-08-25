using UnityEngine;

/// <summary>
/// Reveals MediaPipeAccuracyText the moment this GameObject (RaiseArmsDescription,
/// the pose description panel) becomes active -- so "Accuracy: X%" shows up
/// as soon as any of the 5 pose options is picked, not just later once actual
/// pose-holding gameplay starts.
///
/// Attached to RaiseArmsDescription itself (NOT to the text) and driven by
/// OnEnable -- a disabled GameObject's own Update() never runs, so a watcher
/// living on the text couldn't reliably re-show itself once switched off.
/// OnEnable on the panel fires every time it's activated regardless of prior
/// state, which is what "appear when this panel appears" actually needs.
///
/// Deliberately one-directional: only ever turns the text ON, never OFF.
/// MediaPipeAccuracyText is shared/reused by later phases too (calibration
/// countdown, live accuracy during HoldPose -- see
/// MediaPipePoseTracker.accuracyText), so this must not fight whatever else
/// legitimately shows or hides it once this panel itself closes.
/// </summary>
public class AccuracyTextVisibilityLink : MonoBehaviour
{
    [Tooltip("MediaPipeAccuracyText -- shown whenever this GameObject (RaiseArmsDescription) activates.")]
    public GameObject accuracyText;

    private void OnEnable()
    {
        if (accuracyText != null && !accuracyText.activeSelf)
            accuracyText.SetActive(true);
    }
}
