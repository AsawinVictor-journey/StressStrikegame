using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Escape inside a gameplay scene returns the player to that mode's own menu --
/// Yoga to Yoga Menu, Rage Room to Rage Room Menu, any Boxing level to BoxingMenu --
/// rather than to the global MainMenuScene.
///
/// Spawns itself via RuntimeInitializeOnLoadMethod instead of living as a hand-placed
/// object in each scene. Six gameplay scenes would otherwise each need their own copy
/// (and stay in sync), and entering one directly in the Editor -- which is how these
/// scenes actually get tested -- would still work only if that copy happened to be
/// present. Routing through SceneTransitionManager keeps the existing fade and its
/// Time.timeScale reset, so leaving mid-KO-freeze doesn't carry slow-mo into the menu.
/// </summary>
public class EscapeToModeMenu : MonoBehaviour
{
    // Gameplay scene name -> the menu scene that mode goes back to. Scenes absent from
    // this map (MainMenuScene, the mode menus themselves) ignore Escape entirely.
    private static readonly Dictionary<string, string> MenuByGameplayScene = new Dictionary<string, string>
    {
        { "Level 1",   "BoxingMenu" },
        { "Level 2",   "BoxingMenu" },
        { "Level 3",   "BoxingMenu" },
        { "Training",  "BoxingMenu" },
        { "Rage Room", "Rage Room Menu" },
        { "Yoga",      "Yoga Menu" },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject go = new GameObject("EscapeToModeMenu (auto-created)");
        go.AddComponent<EscapeToModeMenu>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        string activeScene = SceneManager.GetActiveScene().name;
        if (!MenuByGameplayScene.TryGetValue(activeScene, out string menuScene)) return;

        SceneTransitionManager.Instance.LoadScene(menuScene);
    }
}
