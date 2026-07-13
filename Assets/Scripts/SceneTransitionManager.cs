using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("Transition")]
    public float fadeDuration = 0.5f;
    public Color fadeColor = Color.black;

    private Canvas transitionCanvas;
    private Image fadeImage;
    private bool isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateTransitionUI();
    }

    private void CreateTransitionUI()
    {
        GameObject canvasObj = new GameObject("TransitionCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.raycastTarget = false;

        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void LoadScene(string sceneName)
    {
        if (!isTransitioning)
            StartCoroutine(Transition(sceneName));
    }

    private IEnumerator Transition(string sceneName)
    {
        isTransitioning = true;

        // Fade to black first so the player always sees a transition,
        // regardless of how long the target scene takes to load in the background.
        yield return Fade(1f);

        // Start loading the scene, but don't activate it yet.
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        // Wait until the scene has finished loading (90%).
        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        // Activate the new scene.
        operation.allowSceneActivation = true;

        // Wait until activation is complete.
        while (!operation.isDone)
        {
            yield return null;
        }

        // Let the new scene render one frame.
        yield return null;

        // Fade back in.
        yield return Fade(0f);

        isTransitioning = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        fadeImage.raycastTarget = true;

        float startAlpha = fadeImage.color.a;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            // Clamp the step so a big frame hitch (e.g. activating a large scene)
            // can't jump the timer past fadeDuration in a single frame.
            timer += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

            Color c = fadeImage.color;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, timer / fadeDuration);
            fadeImage.color = c;

            yield return null;
        }

        Color final = fadeImage.color;
        final.a = targetAlpha;
        fadeImage.color = final;

        fadeImage.raycastTarget = targetAlpha > 0f;
    }
}