using System.Collections;
using UnityEngine;

public class UIFade : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.5f;

    public void ShowUI(CanvasGroup group)
    {
        StartCoroutine(FadeIn(group));
    }

    public void HideUI(CanvasGroup group)
    {
        StartCoroutine(FadeOut(group));
    }

    public void SwitchUI(CanvasGroup hide, CanvasGroup show)
    {
        StartCoroutine(SwitchRoutine(hide, show));
    }

    IEnumerator SwitchRoutine(CanvasGroup hide, CanvasGroup show)
    {
        if (hide != null)
            yield return FadeOut(hide);

        if (show != null)
            yield return FadeIn(show);
    }

    public IEnumerator FadeIn(CanvasGroup group)
    {
        if (group == null) yield break;

        group.gameObject.SetActive(true);
        group.interactable = false;
        group.blocksRaycasts = false;

        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    public IEnumerator FadeOut(CanvasGroup group)
    {
        if (group == null) yield break;

        group.interactable = false;
        group.blocksRaycasts = false;

        float t = 0f;
        float startAlpha = group.alpha;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeDuration);
            yield return null;
        }

        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }
}