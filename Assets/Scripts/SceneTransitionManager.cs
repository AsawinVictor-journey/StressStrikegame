using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    private static SceneTransitionManager _instance;

    /// <summary>
    /// Lazily creates the manager if none exists yet. Any scene can carry a
    /// SceneButton, but this manager only ever spawned via a hand-placed
    /// instance's own Awake() — so entering a scene directly (in the Editor,
    /// or any flow that skips whatever scene first placed one) left Instance
    /// null and NRE'd every SceneButton in it. CreateTransitionUI() builds its
    /// canvas entirely from code with no external prefab dependency, so
    /// there's nothing stopping it from running on a manager created here
    /// instead of one hand-placed in a scene — this makes Instance work no
    /// matter which scene is entered first.
    /// </summary>
    public static SceneTransitionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SceneTransitionManager (auto-created)");
                _instance = go.AddComponent<SceneTransitionManager>();
            }
            return _instance;
        }
    }

    [Header("Transition")]
    public float fadeDuration = 0.5f;
    public Color fadeColor = Color.black;

    private Canvas transitionCanvas;
    private Image fadeImage;
    private bool isTransitioning;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
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

        // Slow-mo effects (KO freeze-frame, TriggerSlowMotionMode, etc.) can leave
        // Time.timeScale below 1 if a scene switch interrupts their own restore logic.
        // Reset it here so the next scene never loads into a paused/slowed state.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        // Fade to black first so the player always sees a transition,
        // regardless of how long the target scene takes to load in the background.
        yield return Fade(1f);

        // Start loading the scene, but don't activate it yet. LoadSceneAsync throws
        // (rather than returning null) if sceneName isn't in the active build
        // profile/scene list — without this guard that exception kills the coroutine
        // right here, leaving the screen stuck black and isTransitioning stuck true
        // forever (every later LoadScene call silently no-ops). Fade back in and
        // recover instead of freezing.
        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SceneTransitionManager: couldn't load scene '{sceneName}': {e.Message}");
        }

        if (operation == null)
        {
            yield return Fade(0f);
            isTransitioning = false;
            yield break;
        }

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