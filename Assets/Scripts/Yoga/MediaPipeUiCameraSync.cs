using UnityEngine;

/// <summary>
/// Keeps a dedicated Screen-Space-Camera UI camera's orthographic size locked to
/// the canvas's own actual (CanvasScaler-computed) pixel size every frame.
///
/// Why this exists: Main Canvas uses CanvasScaler in "Scale With Screen Size"
/// mode, so its RectTransform.rect (in canvas units) changes with the real
/// screen/window resolution (recomputed by Unity each time it changes). For a
/// Screen Space - Camera canvas to render at the same 1:1 canvas-unit-to-pixel
/// scale Screen Space - Overlay provides for free, this camera's orthographic
/// size must equal exactly half the canvas's CURRENT rect height -- a fixed/
/// hardcoded size only happens to be correct at one specific resolution and
/// silently breaks (wrong scale for anything parented under the canvas that
/// isn't itself a UI Graphic, e.g. MediaPipe's LineRenderer-based skeleton
/// annotations) the moment the window is resized or run at a different
/// resolution.
///
/// Deliberately does NOT touch canvas.worldCamera/renderMode/planeDistance --
/// those are one-time scene setup, done once in the Editor, not runtime state.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MediaPipeUiCameraSync : MonoBehaviour
{
    [Tooltip("Main Canvas's own RectTransform -- the canvas this camera renders, NOT the annotation/target sub-panels inside it.")]
    public RectTransform canvasRect;

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (canvasRect == null || _camera == null) return;

        float height = canvasRect.rect.height;
        if (height > 0f)
            _camera.orthographicSize = height * 0.5f;
    }
}
