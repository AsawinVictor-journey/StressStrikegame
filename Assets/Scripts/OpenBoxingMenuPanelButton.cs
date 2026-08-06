using UnityEngine;
using UnityEngine.UI;

// Attach alongside a Button (same GameObject) that needs to leave the
// current scene, land on BoxingMenu.unity, and have it show a specific rig
// (e.g. "Train") instead of its default MainMenu rig. Wires itself to the
// sibling Button's onClick so no manual Inspector hookup is required. Sets
// PendingBoxingMenuPanel before handing off to SceneTransitionManager, which
// MenuRigSwitcher reads on Start().
[RequireComponent(typeof(Button))]
public class OpenBoxingMenuPanelButton : MonoBehaviour
{
    [SerializeField] private string sceneName = "BoxingMenu";
    [SerializeField] private string rigToOpen = "Train";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(LoadBoxingMenuPanel);
    }

    public void LoadBoxingMenuPanel()
    {
        PendingBoxingMenuPanel.RigToOpen = rigToOpen;
        SceneTransitionManager.Instance.LoadScene(sceneName);
    }
}
