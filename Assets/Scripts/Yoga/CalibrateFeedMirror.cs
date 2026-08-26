using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mirrors the MediaPipe webcam picture onto a second RawImage in a different canvas.
///
/// Why a copy rather than moving the original: the real feed lives under a
/// Screen-Space-CAMERA canvas, and Unity always composites Screen-Space-OVERLAY
/// canvases on top of camera-based ones -- so the original can never be drawn above
/// the Yoga UI no matter what sorting order it is given. A plain RawImage in its own
/// Overlay canvas CAN sit above it, and this keeps that copy fed.
///
/// Only the picture is mirrored. The pose skeleton is drawn with LineRenderers, which
/// are world-space renderers a camera has to render -- they do not exist in an Overlay
/// canvas. That is why this is used for calibration (see yourself, get in frame) while
/// gameplay still shows the real annotated screen.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class CalibrateFeedMirror : MonoBehaviour
{
    [Tooltip("The MediaPipe 'Annotatable Screen' RawImage. Its texture is assigned at runtime, " +
             "not in the Editor, so it has to be read every frame rather than copied once.")]
    public RawImage source;

    [Tooltip("Keeps the copy at the source texture's aspect ratio instead of stretching it into " +
             "whatever rect it was given. Leave on unless the rect is already the right shape.")]
    public bool preserveAspect = true;

    private RawImage _target;
    private RectTransform _rect;

    private void Awake()
    {
        _target = GetComponent<RawImage>();
        _rect = transform as RectTransform;
    }

    // LateUpdate, not Update: MediaPipe assigns the decoded frame during its own update,
    // so reading earlier would show last frame's texture on the frame it first appears.
    private void LateUpdate()
    {
        if (source == null || _target == null) return;

        if (_target.texture != source.texture) _target.texture = source.texture;
        _target.uvRect = source.uvRect;

        // Hide rather than show a white box while the webcam is still starting up.
        bool hasFrame = source.texture != null;
        if (_target.enabled != hasFrame) _target.enabled = hasFrame;

        if (preserveAspect && hasFrame && _rect != null)
        {
            float aspect = (float)source.texture.width / source.texture.height;
            if (aspect > 0f)
            {
                Vector2 size = _rect.sizeDelta;
                float fitted = size.x / aspect;
                if (!Mathf.Approximately(size.y, fitted))
                    _rect.sizeDelta = new Vector2(size.x, fitted);
            }
        }
    }
}
