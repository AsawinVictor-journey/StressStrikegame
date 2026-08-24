using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin presentation-only view for one SOLID dot marking the player's actual,
/// live-detected joint position -- the counterpart to TargetCircleView's hollow
/// target rings. MediaPipePoseTracker drives it every frame via
/// SetPosition()/SetVisible(); this class contains no MediaPipe/tracking logic
/// of its own.
///
/// Deliberately a separate, minimal component rather than a "mode" flag on
/// TargetCircleView: a small fixed-size marker of the live position is a
/// different concern from a tolerance-scaled target zone (see design notes --
/// hollow = target, solid = actual, and they must not be conflated into one
/// component that has to juggle both).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ActualPositionDotView : MonoBehaviour
{
    public Image image;
    [Tooltip("Solid filled circle sprite -- if left empty, falls back to a generated placeholder disc.")]
    public Sprite dotSprite;
    public Color color = new Color(0.9f, 0.1f, 0.1f, 0.95f); // solid red -- live tracking indicator
    [Tooltip("Fixed on-screen radius, in target-space local units. Matches the fixed hollow ring's size (44) so the two markers read as the same scale.")]
    public float radius = 44f;

    private RectTransform _rect;
    private static Sprite _fallbackDotSprite;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        if (image == null) image = GetComponent<Image>();
        if (image != null)
        {
            image.sprite = dotSprite != null ? dotSprite : GetFallbackDotSprite();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }
        _rect.sizeDelta = new Vector2(radius * 2f, radius * 2f);
    }

    /// <summary>Position the dot. localPosition is in the parent target-space's local units.</summary>
    public void SetPosition(Vector2 localPosition)
    {
        if (_rect == null) _rect = (RectTransform)transform;
        _rect.anchoredPosition = localPosition;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    private static Sprite GetFallbackDotSprite()
    {
        if (_fallbackDotSprite != null) return _fallbackDotSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SolidDotSprite_Generated" };
        var center = new Vector2(size / 2f, size / 2f);
        float r = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = d <= r ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        _fallbackDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _fallbackDotSprite.name = "SolidDotSprite_Generated";
        return _fallbackDotSprite;
    }
}
