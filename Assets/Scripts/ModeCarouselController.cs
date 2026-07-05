using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ModeCarouselController : MonoBehaviour
{
    [System.Serializable]
    public class ModePage
    {
        public string modeName;
        public GameObject panel;
        public string sceneToLoad;
        public Button playButton;
    }

    [Header("Pages (order: Level, Training, Rage Room, Meditate)")]
    public ModePage[] pages;

    [Header("Nav Buttons")]
    public Button leftArrowButton;
    public Button rightArrowButton;
    public Button backButton;

    [Header("Coming-Soon Toast")]
    public GameObject comingSoonToast;
    public TMP_Text comingSoonLabel;
    public float toastVisibleSeconds = 1.5f;
    public float toastFadeSeconds = 0.25f;

    [Header("Return Target (shown when Back is pressed)")]
    public GameObject mainMenuCanvas;

    private int currentIndex = 0;
    private Coroutine toastRoutine;
    private bool wired = false;

    private void OnEnable()
    {
        WireButtons();
        currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, pages.Length - 1));
        ShowPage(currentIndex);
        if (comingSoonToast != null) comingSoonToast.SetActive(false);
    }

    private void WireButtons()
    {
        if (wired) return;
        if (leftArrowButton != null) { leftArrowButton.onClick.RemoveListener(Prev); leftArrowButton.onClick.AddListener(Prev); }
        if (rightArrowButton != null) { rightArrowButton.onClick.RemoveListener(Next); rightArrowButton.onClick.AddListener(Next); }
        if (backButton != null) { backButton.onClick.RemoveListener(Back); backButton.onClick.AddListener(Back); }
        if (pages != null)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                if (page != null && page.playButton != null)
                {
                    page.playButton.onClick.RemoveListener(PlayCurrent);
                    page.playButton.onClick.AddListener(PlayCurrent);
                }
            }
        }
        wired = true;
    }

    public void Next()
    {
        if (pages == null || pages.Length == 0) return;
        currentIndex = (currentIndex + 1) % pages.Length;
        ShowPage(currentIndex);
    }

    public void Prev()
    {
        if (pages == null || pages.Length == 0) return;
        currentIndex = (currentIndex - 1 + pages.Length) % pages.Length;
        ShowPage(currentIndex);
    }

    public void PlayCurrent()
    {
        if (pages == null || pages.Length == 0) return;
        var page = pages[currentIndex];
        if (page == null) return;

        if (string.IsNullOrEmpty(page.sceneToLoad))
        {
            ShowComingSoon(page.modeName);
            return;
        }

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadScene(page.sceneToLoad);
        }
        else
        {
            SceneManager.LoadScene(page.sceneToLoad);
        }
    }

    public void Back()
    {
        gameObject.SetActive(false);
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
    }

    private void ShowPage(int i)
    {
        for (int k = 0; k < pages.Length; k++)
        {
            if (pages[k] != null && pages[k].panel != null)
                pages[k].panel.SetActive(k == i);
        }
    }

    private void ShowComingSoon(string modeName)
    {
        if (comingSoonToast == null) return;
        if (comingSoonLabel != null)
            comingSoonLabel.text = string.IsNullOrEmpty(modeName) ? "Coming soon" : modeName + " — coming soon";

        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        comingSoonToast.SetActive(true);
        var group = comingSoonToast.GetComponent<CanvasGroup>();
        if (group == null) group = comingSoonToast.AddComponent<CanvasGroup>();

        yield return FadeGroup(group, 0f, 1f, toastFadeSeconds);
        yield return new WaitForSeconds(toastVisibleSeconds);
        yield return FadeGroup(group, 1f, 0f, toastFadeSeconds);

        comingSoonToast.SetActive(false);
        toastRoutine = null;
    }

    private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (duration <= 0f) { group.alpha = to; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }
}
