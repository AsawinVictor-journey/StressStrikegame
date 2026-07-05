using System.Collections;
using UnityEngine;

public class UIFade : MonoBehaviour
{
    public CanvasGroup resultGroup;
    public float fadeDuration = 0.5f;

    public void ShowResult()
    {
        resultGroup.alpha = 0f;
        resultGroup.interactable = false;
        resultGroup.blocksRaycasts = false;

        resultGroup.gameObject.SetActive(true);

        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            resultGroup.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
            yield return null;
        }

        resultGroup.alpha = 1f;
        resultGroup.interactable = true;
        resultGroup.blocksRaycasts = true;
    }
}