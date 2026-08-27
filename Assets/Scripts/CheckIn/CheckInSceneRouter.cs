using UnityEngine;

// Listens for CheckInManager's decision, shows the shared result panel (reused
// from Brief-COPE's original result screen - see CheckInResultPanel), and only
// loads the matching gameplay-menu scene once the player taps continue there.
public class CheckInSceneRouter : MonoBehaviour
{
    [SerializeField] private CheckInManager checkInManager;
    [SerializeField] private CheckInResultPanel resultPanel;

    private GameMode pendingGameMode;

    private void OnEnable()
    {
        if (checkInManager != null) checkInManager.onModeDecided += HandleModeDecided;
        if (resultPanel != null) resultPanel.onContinue += LoadPendingScene;
        if (resultPanel != null) resultPanel.onPickAnother += ReturnToMenu;
    }

    private void OnDisable()
    {
        if (checkInManager != null) checkInManager.onModeDecided -= HandleModeDecided;
        if (resultPanel != null) resultPanel.onContinue -= LoadPendingScene;
        if (resultPanel != null) resultPanel.onPickAnother -= ReturnToMenu;
    }

    private void HandleModeDecided(string mode, string reason)
    {
        if (!CheckInModeMapping.TryToGameMode(mode, out pendingGameMode))
            Debug.LogWarning($"CheckInSceneRouter: unrecognized mode '{mode}', defaulting to Rage Room.");

        if (resultPanel != null)
        {
            resultPanel.Show(mode, reason);
        }
        else
        {
            Debug.LogWarning("CheckInSceneRouter: no result panel wired; loading scene immediately.");
            LoadPendingScene();
        }
    }

    private void LoadPendingScene()
    {
        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogError("CheckInSceneRouter: no SceneTransitionManager.Instance in scene.");
            return;
        }

        SceneTransitionManager.Instance.LoadScene(GameModeRecommendation.SceneNames[pendingGameMode]);
    }

    private void ReturnToMenu()
    {
        if (resultPanel != null) resultPanel.Hide();

#if UNITY_2023_1_OR_NEWER
        var highlighter = FindFirstObjectByType<RecommendedModeHighlighter>();
#else
        var highlighter = FindObjectOfType<RecommendedModeHighlighter>();
#endif
        if (highlighter != null) highlighter.ClearHighlight();

        // The check-in canvas is an overlay in MainMenuScene. Hiding its root
        // returns control to the carousel without changing the selected mode.
        transform.root.gameObject.SetActive(false);
    }
}
