using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manual replacement for UnityEngine.UI.HorizontalLayoutGroup on the Yoga pose
/// description panel's button row (Calibrate / Calibrate-Mid / Start).
///
/// Empirically verified in this project: HorizontalLayoutGroup does NOT
/// correctly account for a center-pivoted (0.5,0.5) container's own rect
/// offset when computing child anchoredPosition -- children end up packed
/// against the container's positive-x edge instead of centered on local
/// (0,0), even with childAlignment set to MiddleCenter. Rather than fight
/// that (re-pivoting the container, hand-compensating offsets, etc.), this
/// component does the packing math itself.
///
/// Only ACTIVE children are included, so this also gives the row its
/// "flexbox" reflow behaviour: when a pose has no MidPoseAnimation and
/// YogaManager disables the Calibrate-Mid button, the remaining buttons
/// re-center with no gap on the very next Update().
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ButtonRowLayout : MonoBehaviour
{
    public float spacing = 50f;

    private RectTransform _rect;
    private bool[] _lastActive;

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    private void OnEnable()
    {
        Relayout();
    }

    private void Update()
    {
        // Cheap poll over a handful of children -- only re-lay-out when an
        // active state actually flipped since last check (e.g. YogaManager
        // toggling the Calibrate-Mid button per selected pose).
        int n = _rect.childCount;
        // A changed child COUNT is itself a reason to re-lay-out: without forcing it
        // here, a freshly sized (all-false) array compared against a row whose
        // children are all inactive reports "no change" and the row never repacks.
        bool changed = false;
        if (_lastActive == null || _lastActive.Length != n)
        {
            _lastActive = new bool[n];
            changed = true;
        }
        for (int i = 0; i < n; i++)
        {
            bool active = _rect.GetChild(i).gameObject.activeSelf;
            if (_lastActive[i] != active) changed = true;
            _lastActive[i] = active;
        }
        if (changed) Relayout();
    }

    /// <summary>Packs all currently-active children left-to-right (in sibling order), centered on this RectTransform's local (0,0).</summary>
    public void Relayout()
    {
        if (_rect == null) _rect = (RectTransform)transform;
        int n = _rect.childCount;

        var active = new List<RectTransform>();
        float totalWidth = 0f;
        for (int i = 0; i < n; i++)
        {
            var child = (RectTransform)_rect.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            active.Add(child);
            // rect.width, NOT sizeDelta.x: sizeDelta is only the width for a
            // point-anchored child. For any child with stretched anchors it is the
            // offset from those anchors (often 0 or negative), which packs the row
            // on top of itself. rect.width is the resolved width either way.
            totalWidth += child.rect.width;
        }
        if (active.Count == 0) return;
        totalWidth += spacing * (active.Count - 1);

        float x = -totalWidth * 0.5f;
        foreach (var child in active)
        {
            float w = child.rect.width;
            var pos = child.anchoredPosition;
            pos.x = x + w * 0.5f;
            pos.y = 0f;
            child.anchoredPosition = pos;
            x += w + spacing;
        }
    }
}
