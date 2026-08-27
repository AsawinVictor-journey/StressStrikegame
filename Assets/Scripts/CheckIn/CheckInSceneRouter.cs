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
        if (resultPanel != null) resultPanel.onSkip += HandleSkip;
    }

    private void OnDisable()
    {
        if (checkInManager != null) checkInManager.onModeDecided -= HandleModeDecided;
        if (resultPanel != null) resultPanel.onContinue -= LoadPendingScene;
        if (resultPanel != null) resultPanel.onSkip -= HandleSkip;
    }

    // Skipping the result screen means the player opted out after a mode was
    // already decided - reuses CheckInManager.Skip() so it closes the overlay
    // the exact same way skipping the input phase does, instead of loading
    // the decided scene.
    private void HandleSkip()
    {
        if (checkInManager != null) checkInManager.Skip();
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
}
