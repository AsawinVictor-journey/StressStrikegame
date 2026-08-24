using UnityEngine;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;

/// <summary>
/// Lets Yoga Mode use whichever webcam the player actually has, instead of
/// hard-coding a device name/index. Detects available cameras, restores a
/// previously-saved choice by device NAME (falls back to the first camera if
/// that device is gone), and exposes SelectCamera()/GetCameraNames() for a
/// future picker UI.
///
/// Does not assume WebCamSource is a MonoBehaviour -- it isn't. The live
/// instance only exists via ImageSourceProvider.ImageSource, and only once
/// Bootstrap has wired it up. This subscribes to Bootstrap's static
/// "image source ready" signal (see Bootstrap.cs) rather than polling a
/// [SerializeField] reference, since none can exist for a plain C# class.
/// </summary>
public class YogaCameraSelector : MonoBehaviour
{
    private const string SavedCameraKey = "StressStrike_YogaCamera";

    [Header("Editor-only: set the camera without Play Mode")]
    [Tooltip("Type an exact camera name here, then right-click this component's " +
        "header (or the ⋮ menu) and choose 'Save Preferred Camera Now'. " +
        "Right-click → 'List Available Cameras' first if you don't know the " +
        "exact name -- it prints the list to the Console. This writes the SAME " +
        "PlayerPrefs value SelectCamera() would, so it works without ever " +
        "entering Play Mode, and persists exactly the same way.")]
    [SerializeField] private string editorPreferredCameraName;

    // Best-effort heuristic only -- Unity's WebCamDevice exposes no field that
    // says "this is a virtual/software camera", so there's no way to be certain.
    // This just avoids the worst case (defaulting straight into a phone-cam app
    // or streaming tool on a fresh install, the way index 0 did before). Once a
    // player actually picks a camera (via SelectCamera, e.g. from a picker UI),
    // that saved choice always wins over this list -- this only matters for the
    // very first run on a machine with nothing saved yet. Extend as needed.
    private static readonly string[] _knownVirtualCameraNameFragments =
    {
        "obs virtual camera", "iriun", "droidcam", "camo", "manycam",
        "xsplit vcam", "snap camera", "nvidia broadcast", "epoccam",
        "continuity camera", "virtual camera",
    };

    private void Awake()
    {
        // Safe regardless of whether Bootstrap has already run (fires
        // immediately) or hasn't yet (queued) -- see Bootstrap.cs for why a
        // plain instance event isn't reliable here.
        Bootstrap.SubscribeImageSourceReady(ApplySavedOrDefaultCamera);
    }

    private void ApplySavedOrDefaultCamera()
    {
        var webCamSource = ImageSourceProvider.ImageSource as WebCamSource;
        if (webCamSource == null)
        {
            // Not an error by itself -- AppSettings.defaultImageSource may be
            // set to Image/Video instead of WebCamera for this build.
            Debug.LogWarning("[YogaCameraSelector] Active ImageSource is not a WebCamSource -- skipping camera selection.");
            return;
        }

        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogWarning("[YogaCameraSelector] No webcams found.");
            return;
        }

        string savedName = PlayerPrefs.GetString(SavedCameraKey, "");
        if (!string.IsNullOrEmpty(savedName))
        {
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].name == savedName)
                {
                    webCamSource.SelectSource(i);
                    Debug.Log($"[YogaCameraSelector] Restored saved camera: {devices[i].name}");
                    return;
                }
            }
            Debug.LogWarning($"[YogaCameraSelector] Saved camera '{savedName}' is no longer available -- falling back to the first camera.");
        }

        // No saved camera, or it's gone -- prefer the first device that isn't a
        // known virtual/software camera (see _knownVirtualCameraNameFragments),
        // so a fresh install is less likely to land on a phone-cam app or
        // streaming tool. Falls back to plain index 0 (WebCamSource's own
        // default) if every device matches the list or nothing does.
        int defaultIndex = 0;
        for (int i = 0; i < devices.Length; i++)
        {
            if (!IsLikelyVirtualCamera(devices[i].name))
            {
                defaultIndex = i;
                break;
            }
        }

        webCamSource.SelectSource(defaultIndex);
        Debug.Log($"[YogaCameraSelector] Using default camera: {devices[defaultIndex].name}");
    }

    private static bool IsLikelyVirtualCamera(string deviceName)
    {
        string lower = deviceName.ToLowerInvariant();
        foreach (var fragment in _knownVirtualCameraNameFragments)
        {
            if (lower.Contains(fragment)) return true;
        }
        return false;
    }

    /// <summary>
    /// Selects a camera by index into GetCameraNames() and remembers it for next
    /// time. Intended to be called BEFORE the pose landmarker starts playing
    /// (i.e. from a pre-game camera picker) -- ImageSource's own contract is
    /// that a source change only takes effect on the next Play(), so calling
    /// this after tracking has already started won't switch the live feed.
    /// </summary>
    public void SelectCamera(int index)
    {
        var webCamSource = ImageSourceProvider.ImageSource as WebCamSource;
        if (webCamSource == null)
        {
            Debug.LogError("[YogaCameraSelector] ImageSource isn't ready yet (Bootstrap hasn't finished) or isn't a WebCamSource.");
            return;
        }

        var devices = WebCamTexture.devices;
        if (index < 0 || index >= devices.Length)
        {
            Debug.LogError($"[YogaCameraSelector] Invalid camera index: {index}");
            return;
        }

        webCamSource.SelectSource(index);
        PlayerPrefs.SetString(SavedCameraKey, devices[index].name);
        PlayerPrefs.Save();
        Debug.Log($"[YogaCameraSelector] Camera selected and saved: {devices[index].name}");
    }

    /// <summary>Device names for a future camera-picker dropdown, in the same order SelectCamera() expects.</summary>
    public string[] GetCameraNames()
    {
        var devices = WebCamTexture.devices;
        var names = new string[devices.Length];
        for (int i = 0; i < devices.Length; i++) names[i] = devices[i].name;
        return names;
    }

    // --- Editor-only self-service helpers -- no script/Play Mode needed -------
    // Right-click the component header (or its ⋮ menu) in the Inspector to use
    // these. They read/write the exact same PlayerPrefs key as SelectCamera(),
    // so anything set this way is used automatically the next time Play Mode
    // (or a real build) starts.

    [ContextMenu("List Available Cameras (check Console)")]
    private void ListAvailableCameras()
    {
        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.Log("[YogaCameraSelector] No cameras detected on this machine.");
            return;
        }
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"[YogaCameraSelector] {i}: {devices[i].name}" +
                (IsLikelyVirtualCamera(devices[i].name) ? "  (looks like a virtual/software camera)" : ""));
        }
    }

    [ContextMenu("Save Preferred Camera Now")]
    private void SavePreferredCameraNow()
    {
        if (string.IsNullOrEmpty(editorPreferredCameraName))
        {
            Debug.LogError("[YogaCameraSelector] 'Editor Preferred Camera Name' is empty -- " +
                "type an exact camera name first (use 'List Available Cameras' to find it).");
            return;
        }

        PlayerPrefs.SetString(SavedCameraKey, editorPreferredCameraName);
        PlayerPrefs.Save();
        Debug.Log($"[YogaCameraSelector] Saved preferred camera: '{editorPreferredCameraName}'. " +
            "Takes effect next time Play Mode starts.");
    }

    [ContextMenu("Show Currently Saved Camera (check Console)")]
    private void ShowCurrentlySavedCamera()
    {
        string saved = PlayerPrefs.GetString(SavedCameraKey, "");
        Debug.Log(string.IsNullOrEmpty(saved)
            ? "[YogaCameraSelector] Nothing saved yet -- will use the default-camera heuristic."
            : $"[YogaCameraSelector] Currently saved: '{saved}'");
    }
}
