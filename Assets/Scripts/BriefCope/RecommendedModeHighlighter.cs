using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Reads the result the Brief-COPE survey saved on its way out and gives the
// recommended mode's menu panel a comic-style "shine": a pulsing glow behind it
// plus a few drifting stars. Purely advisory - nothing is locked, nothing is
// blocked, and the player is free to pick any mode. Does nothing at all if the
// player skipped the survey or has never taken it.
public class RecommendedModeHighlighter : MonoBehaviour
{
    private const string PrefsKey = "BriefCope_LastResult";

    [Header("Menu panels to highlight (one per mode)")]
    [SerializeField] private RectTransform boxingTarget;
    [SerializeField] private RectTransform rageRoomTarget;
    [SerializeField] private RectTransform yogaTarget;

    [Header("Look")]
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private Sprite starSprite;
    [SerializeField] private Color glowColor = new Color(1f, 0.35f, 0.37f, 0.55f);
    [SerializeField] private float glowPadding = 40f;
    [SerializeField] private float pulseSpeed = 1.6f;
    [SerializeField] private float pulseAmount = 0.06f;
    [SerializeField] private int starCount = 3;

    private void Start()
    {
        // If the survey canvas is active, do not render the glow yet.
        // It will be triggered once the survey is finished or skipped.
        var surveyCanvas = GameObject.Find("SurveyCanvas");
        if (surveyCanvas != null && surveyCanvas.activeInHierarchy)
        {
            return;
        }

        TriggerHighlight();
    }

    public void TriggerHighlight()
    {
        // Stop any running coroutines first
        StopAllCoroutines();

        // Clean up any legacy highlights
        var oldGlows = new System.Collections.Generic.List<GameObject>();
        if (boxingTarget != null) FindLegacyHighlights(boxingTarget, oldGlows);
        if (rageRoomTarget != null) FindLegacyHighlights(rageRoomTarget, oldGlows);
        if (yogaTarget != null) FindLegacyHighlights(yogaTarget, oldGlows);
        
        foreach (var og in oldGlows)
        {
            DestroyImmediate(og);
        }

        RectTransform target = ResolveTarget();
        if (target == null) return;

        var glow = BuildGlow(target);
        BuildStars(target);
        StartCoroutine(Pulse(glow));
    }

    private void FindLegacyHighlights(RectTransform target, System.Collections.Generic.List<GameObject> list)
    {
        foreach (Transform child in target)
        {
            if (child.name.StartsWith("RecommendedGlow") || child.name.StartsWith("RecommendedStar"))
            {
                list.Add(child.gameObject);
            }
        }
    }

    private RectTransform ResolveTarget()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return null;

        BriefCopeResult result;
        try
        {
            result = JsonUtility.FromJson<BriefCopeResult>(json);
        }
        catch
        {
            return null;
        }

        if (result == null || result.skipped || string.IsNullOrEmpty(result.mode)) return null;

        switch (result.mode)
        {
            case nameof(GameMode.Boxing): return boxingTarget;
            case nameof(GameMode.RageRoom): return rageRoomTarget;
            case nameof(GameMode.Meditate): return yogaTarget;
            default: return null;
        }
    }

    private RectTransform BuildGlow(RectTransform target)
    {
        var go = new GameObject("RecommendedGlow", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(target, false);

        // Sits behind the panel's own art but inside its rect, so it reads as a halo.
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-glowPadding, -glowPadding);
        rt.offsetMax = new Vector2(glowPadding, glowPadding);

        var img = go.AddComponent<Image>();
        img.sprite = glowSprite;
        img.color = glowColor;
        img.raycastTarget = false;
        img.preserveAspect = false;

        return rt;
    }

    private void BuildStars(RectTransform target)
    {
        if (starSprite == null || starCount <= 0) return;

        for (int i = 0; i < starCount; i++)
        {
            var go = new GameObject($"RecommendedStar{i}", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(target, false);

            float size = Random.Range(26f, 46f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = new Vector2(Random.Range(0.05f, 0.95f), Random.Range(0.65f, 1.05f));
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));

            var img = go.AddComponent<Image>();
            img.sprite = starSprite;
            img.raycastTarget = false;
            img.preserveAspect = true;

            StartCoroutine(Twinkle(img, Random.Range(0f, Mathf.PI * 2f)));
        }
    }

    private IEnumerator Pulse(RectTransform glow)
    {
        if (glow == null) yield break;

        while (glow != null)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float scale = 1f + t * pulseAmount;
            glow.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
    }

    private IEnumerator Twinkle(Image star, float phase)
    {
        while (star != null)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed + phase) + 1f) * 0.5f;
            var c = star.color;
            c.a = Mathf.Lerp(0.45f, 1f, t);
            star.color = c;
            yield return null;
        }
    }
}
