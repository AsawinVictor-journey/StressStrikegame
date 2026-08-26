using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the MediaPipe-style skeleton as UI lines, in the same canvas as the
/// solid joint dots and hollow target rings.
///
/// Why this exists rather than reusing MediaPipe's own skeleton: MediaPipe's
/// annotation skeleton is LineRenderer-based, which is world-space geometry. It
/// cannot render inside a UI canvas at all, which is why the connections are
/// invisible over the camera picture even though the dots (real UI Images) show
/// up fine. Rebuilding the bones as rotated UI Images puts them in the exact
/// same rendering path as the dots, so they line up with the dots by
/// construction rather than by coincidence.
///
/// Line objects are pooled and created on demand, so the only thing to wire in
/// the Inspector is this component itself -- there is no per-bone GameObject to
/// author or keep in sync with the bone list in MediaPipePoseTracker.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SkeletonLineView : MonoBehaviour
{
    [Tooltip("Parent for the generated line Images. Leave empty to use this object's own " +
             "RectTransform. MUST be the same space the dots are positioned in (the tracker's " +
             "'targetSpace'), or the bones will not line up with the joints.")]
    public RectTransform lineParent;

    [Tooltip("Bone colour. Red by default so the connections read as distinct from the blue " +
             "live-position dots they join.")]
    public Color lineColor = new Color(0.9f, 0.1f, 0.1f, 0.9f);

    [Tooltip("Bone thickness in target-space local units.")]
    public float thickness = 8f;

    [Tooltip("Optional sprite for each bone. Left empty, bones are drawn as plain filled " +
             "rectangles, which is what the MediaPipe look actually is.")]
    public Sprite lineSprite;

    private readonly List<Image> _pool = new List<Image>();
    private int _used;

    private void Awake()
    {
        if (lineParent == null) lineParent = (RectTransform)transform;
    }

    /// <summary>Start a frame's worth of bones. Call before any AddBone.</summary>
    public void BeginBones()
    {
        _used = 0;
    }

    /// <summary>
    /// Add one bone between two points, both in the SAME local space the dots use
    /// (target-space local units, centre-origin).
    /// </summary>
    public void AddBone(Vector2 localA, Vector2 localB)
    {
        var img = GetOrCreate(_used++);
        var rect = (RectTransform)img.transform;

        Vector2 delta = localB - localA;
        float length = delta.magnitude;

        // Pivot at the left-centre so the rect grows from A toward B: that makes
        // position and rotation independent of length, which is what keeps a bone
        // anchored on its joint as the limb extends.
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = localA;
        rect.sizeDelta = new Vector2(length, thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        rect.localScale = Vector3.one;

        img.color = lineColor;
        if (img.sprite != lineSprite) img.sprite = lineSprite;
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
    }

    /// <summary>Hide any pooled lines this frame did not use. Call after the last AddBone.</summary>
    public void EndBones()
    {
        for (int i = _used; i < _pool.Count; i++)
        {
            if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                _pool[i].gameObject.SetActive(false);
        }
    }

    /// <summary>Hide every bone -- used when tracking stops or the skeleton should not show.</summary>
    public void HideAll()
    {
        BeginBones();
        EndBones();
    }

    private Image GetOrCreate(int index)
    {
        while (_pool.Count <= index)
        {
            var go = new GameObject("Bone" + _pool.Count, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(lineParent != null ? lineParent : (RectTransform)transform, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;   // must never sit in front of a button
            img.color = lineColor;
            img.sprite = lineSprite;

            // Behind the dots and rings, so a bone can never cover the markers the
            // player is actually aiming with.
            go.transform.SetAsFirstSibling();

            _pool.Add(img);
        }
        return _pool[index];
    }
}
