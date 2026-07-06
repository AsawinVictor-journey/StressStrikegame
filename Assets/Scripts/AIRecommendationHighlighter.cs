using UnityEngine;
using System.Collections.Generic;

public class AIRecommendationHighlighter : MonoBehaviour
{
    private Dictionary<string, OutlineHighlight> highlightableObjects = new Dictionary<string, OutlineHighlight>();

    private void Start()
    {
        RegisterAllHighlightable();
    }

    private void RegisterAllHighlightable()
    {
        OutlineHighlight[] highlights = FindObjectsByType<OutlineHighlight>(FindObjectsSortMode.None);
        foreach (var highlight in highlights)
        {
            highlightableObjects[highlight.gameObject.name] = highlight;
        }
    }

    public void HighlightTargets(string[] suggestions)
    {
        if (suggestions == null || suggestions.Length == 0) return;

        foreach (var suggestion in suggestions)
        {
            if (highlightableObjects.TryGetValue(suggestion, out var highlighter))
            {
                highlighter.EnableOutline();
            }
            else
            {
                Debug.LogWarning($"Could not find highlightable object: {suggestion}");
            }
        }
    }

    public void ClearAllHighlights()
    {
        foreach (var highlighter in highlightableObjects.Values)
        {
            highlighter.DisableOutline();
        }
    }
}
