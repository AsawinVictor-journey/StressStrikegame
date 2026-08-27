using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tab opens/closes a pause overlay in gameplay scenes.
///
/// Tab rather than Escape deliberately: Escape already means "leave this mode"
/// (see EscapeToModeMenu) and re-binding it would silently change a control that
/// already works. The two are complementary -- Tab suspends, Escape exits.
///
/// Attach this to the pause panel's root in a scene and assign 'panel'. It is NOT
/// auto-spawned like GameAudioSettings, because a pause overlay is scene UI: it
/// needs a Canvas, a scrim and the Options panel, none of which can be conjured
/// from code without guessing at the art.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Tooltip("The whole pause overlay -- scrim plus panel. Hidden on start, toggled by Tab.")]
    public GameObject panel;

    [Tooltip("Optional. Shown from the pause panel's Options button and closed with Back.")]
    public GameObject optionsPanel;

    [Tooltip("Scenes this may open in. Empty = any scene. Leave empty unless a scene needs to opt out.")]
    public List<string> allowedScenes = new List<string>();

    [Tooltip("Freeze the game while paused. Off for Yoga, where the webcam pipeline and the " +
             "breathing/heart-rate timers keep running on unscaled time anyway.")]
    public bool freezeTime = true;

    public bool IsPaused { get; private set; }

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Tab)) return;

        if (allowedScenes.Count > 0)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!allowedScenes.Contains(scene)) return;
        }

        // While Options is up, Tab backs out of Options rather than unpausing --
        // otherwise it closes the whole overlay and strands unsaved-looking sliders.
        if (IsPaused && optionsPanel != null && optionsPanel.activeSelf)
        {
            CloseOptions();
            return;
        }

        SetPaused(!IsPaused);
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (panel != null) panel.SetActive(paused);
        if (!paused && optionsPanel != null) optionsPanel.SetActive(false);

        // Restored to 1 rather than to whatever it was: slow-mo effects elsewhere
        // (the K.O. freeze-frame) can leave timeScale below 1, and capturing that
        // here would make unpausing preserve a stale slow-motion state.
        if (freezeTime) Time.timeScale = paused ? 0f : 1f;
    }

    public void Resume() { SetPaused(false); }

    public void OpenOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    // Safety net: a scene unloading while paused would otherwise leave timeScale
    // at 0 and freeze whatever loads next.
    void OnDisable()
    {
        if (freezeTime && IsPaused) Time.timeScale = 1f;
    }
}
