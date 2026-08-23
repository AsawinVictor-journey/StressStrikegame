using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin presentation-only view for one hollow-red-circle target indicator.
/// MediaPipePoseTracker drives it every frame via SetState()/SetVisible(); this
/// class contains no MediaPipe or tracking logic of its own.
/// Uses the real "Red circle" art (Assets/UI/Yoga/Red circle.png) when assigned;
/// falls back to a procedurally generated ring if not, so this still works
/// standalone without the art dependency.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TargetCircleView : MonoBehaviour
{
    public Image image;
    [Tooltip("Assets/UI/Yoga/Red circle.png -- if left empty, falls back to a generated placeholder ring.")]
    public Sprite ringSprite;
    [Tooltip("Tint applied on top of ringSprite. White = the art's own color, unmodified.")]
    public Color color = Color.white;

    private RectTransform _rect;
    private static Sprite _fallbackRingSprite;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        if (image == null) image = GetComponent<Image>();
        if (image != null)
        {
            image.sprite = ringSprite != null ? ringSprite : GetFallbackRingSprite();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }
    }

    /// <summary>Position/size the circle. localPosition is in the parent target-space's local units.</summary>
    public void SetState(Vector2 localPosition, float radius, float score)
    {
        if (_rect == null) _rect = (RectTransform)transform;
        _rect.anchoredPosition = localPosition;
        _rect.sizeDelta = new Vector2(radius * 2f, radius * 2f);

        if (image != null)
        {
            // Minimal per-joint feedback: more opaque as the joint nears its
            // target. Full visual styling explicitly deferred per the plan.
            float t = Mathf.Clamp01(score / 100f);
            var c = color;
            c.a = Mathf.Lerp(0.35f, 1f, t);
            image.color = c;
        }
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    private static Sprite GetFallbackRingSprite()
    {
        if (_fallbackRingSprite != null) return _fallbackRingSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HollowRingSprite_Generated" };
        var center = new Vector2(size / 2f, size / 2f);
        float outerR = size / 2f - 2f;
        float innerR = outerR - 6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = (d <= outerR && d >= innerR) ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        _fallbackRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _fallbackRingSprite.name = "HollowRingSprite_Generated";
        return _fallbackRingSprite;
    }
}
