using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MenuSettingsBinder : MonoBehaviour
{
    [SerializeField] private string optionsButtonName = "options";
    [SerializeField] private string playButtonName = "play";
    [SerializeField] private string storeButtonName = "Shop";
    [SerializeField] private string exitButtonName = "Exit";
    [SerializeField] private string arenaButtonName = "Arena";
    [SerializeField] private string settingsPanelName = "Settings";
    [SerializeField] private string arenaPanelName = "ArenaMenu";
    [SerializeField] private string levelPanelName = "Level";
    [SerializeField] private string storePanelName = "ShopMenu";
    [SerializeField] private string closeAreaName = "SettingsClickArea";

    private GameObject settingsPanel;
    private GameObject arenaPanel;
    private GameObject levelPanel;
    private GameObject storePanel;

    private void Start()
    {
        BindMenu();
        HideSettings();
    }

    private void BindMenu()
    {
        settingsPanel = FindInLoadedScene(settingsPanelName);
        arenaPanel = FindInLoadedScene(arenaPanelName);
        levelPanel = FindInLoadedScene(levelPanelName);
        storePanel = FindInLoadedScene(storePanelName);

        BindButton(optionsButtonName, ToggleSettings);
        BindButton(playButtonName, ToggleArenaMenu);
        BindButton(arenaButtonName, ToggleLevelCanvas);
        BindButton(storeButtonName, ToggleStoreMenu);
        BindButton(storePanel, exitButtonName, CloseStoreMenu);

        if (settingsPanel != null)
        {
            EnsureCloseArea(settingsPanel.transform);
        }
    }

    public void ToggleSettings()
    {
        if (settingsPanel == null)
        {
            settingsPanel = FindInLoadedScene(settingsPanelName);
        }

        if (settingsPanel == null)
        {
            Debug.LogWarning("Settings panel not found.");
            return;
        }

        bool nextState = !settingsPanel.activeSelf;
        settingsPanel.SetActive(nextState);
        if (nextState)
        {
            SetHierarchyActive(settingsPanel.transform, true);
        }
    }

    public void ToggleArenaMenu()
    {
        TogglePanel(arenaPanel, arenaPanelName);
    }

    public void ToggleLevelCanvas()
    {
        if (levelPanel == null)
        {
            levelPanel = FindInLoadedScene(levelPanelName);
        }

        if (levelPanel == null)
        {
            Debug.LogWarning("Level canvas was not found.");
            return;
        }

        bool nextState = !levelPanel.activeSelf;
        levelPanel.SetActive(nextState);
        if (nextState && arenaPanel != null)
        {
            arenaPanel.SetActive(false);
        }
    }

    public void ToggleStoreMenu()
    {
        TogglePanel(storePanel, storePanelName);
    }

    public void CloseStoreMenu()
    {
        if (storePanel == null)
        {
            storePanel = FindInLoadedScene(storePanelName);
        }

        if (storePanel != null)
        {
            storePanel.SetActive(false);
        }

        RestoreMainMenuBackdrop();
    }

    // The Shop button hides the Boxing/RageRoom/Yoga preview triptych ("Buttons") and its
    // feeder cameras to reveal ShopCam full-screen; closing the shop must bring them back.
    private void RestoreMainMenuBackdrop()
    {
        GameObject buttons = FindInLoadedScene("Buttons");
        if (buttons != null)
        {
            buttons.SetActive(true);
        }

        // Leftover duplicate triptych canvas (root-level "Canvas", separate from ComicPanelCanvas)
        // that also feeds off RT_Left/RT_Center/RT_Right and must be hidden/shown alongside "Buttons".
        GameObject duplicateCanvas = FindInLoadedScene("Canvas");
        if (duplicateCanvas != null)
        {
            duplicateCanvas.SetActive(true);
        }

        GameObject shopCam = FindInLoadedScene("ShopCam");
        if (shopCam != null)
        {
            shopCam.SetActive(false);
        }

        SetCameraActiveByNameAndLayer("Main Camera", 11, true); // Boxing
        SetCameraActiveByNameAndLayer("Main Camera", 6, true);  // Yoga
        SetCameraActiveByName("Camera", true);                  // RageRoom
        SetCameraActiveByName("Center Camera", true);
        SetCameraActiveByName("Right Camera", true);
        SetCameraActiveByName("Background Camera", true);

        GameObject top = FindInLoadedScene("Top");
        if (top != null)
        {
            top.SetActive(true);
        }
    }

    private void SetCameraActiveByName(string cameraName, bool active)
    {
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.gameObject.name == cameraName)
            {
                camera.gameObject.SetActive(active);
            }
        }
    }

    private void SetCameraActiveByNameAndLayer(string cameraName, int layer, bool active)
    {
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.gameObject.name == cameraName && camera.gameObject.layer == layer)
            {
                camera.gameObject.SetActive(active);
            }
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void HideSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void EnsureCloseArea(Transform settingsRoot)
    {
        Transform existing = settingsRoot.Find(closeAreaName);
        if (existing == null)
        {
            GameObject closeArea = new GameObject(closeAreaName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeArea.transform.SetParent(settingsRoot, false);

            RectTransform rect = closeArea.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = closeArea.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            Button button = closeArea.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(CloseSettings);
        }
        else
        {
            Button button = existing.GetComponent<Button>();
            if (button == null)
            {
                button = existing.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(CloseSettings);
        }
    }

    private void SetHierarchyActive(Transform root, bool active)
    {
        foreach (Transform child in root)
        {
            child.gameObject.SetActive(active);
            SetHierarchyActive(child, active);
        }
    }

    private void TogglePanel(GameObject panel, params string[] panelNames)
    {
        if (panel == null)
        {
            panel = FindFirstPanel(panelNames);
        }

        if (panel == null)
        {
            Debug.LogWarning(string.Join("/", panelNames) + " canvas was not found.");
            return;
        }

        panel.SetActive(!panel.activeSelf);
    }

    private void BindButton(string buttonLabel, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButtonByLabel(buttonLabel);
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void BindButton(GameObject root, string buttonLabel, UnityEngine.Events.UnityAction action)
    {
        if (root == null)
        {
            return;
        }

        Button button = FindButtonByLabel(root.transform, buttonLabel);
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private GameObject FindInLoadedScene(string targetName)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject found = FindDeepChild(root.transform, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private GameObject FindFirstPanel(params string[] targetNames)
    {
        foreach (string targetName in targetNames)
        {
            GameObject panel = FindInLoadedScene(targetName);
            if (panel != null)
            {
                return panel;
            }
        }

        return null;
    }

    private Button FindButtonByLabel(string label)
    {
        GameObject menuRoot = FindInLoadedScene("MainMenu (important)") ?? FindInLoadedScene("MainMenu");
        if (menuRoot != null)
        {
            Button byLabel = FindButtonByLabel(menuRoot.transform, label);
            if (byLabel != null)
            {
                return byLabel;
            }
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (LooksLikeLabel(button.gameObject, label))
            {
                return button;
            }
        }

        return null;
    }

    private Button FindButtonByLabel(Transform root, string label)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (LooksLikeLabel(button.gameObject, label))
            {
                return button;
            }
        }

        return null;
    }

    private bool LooksLikeLabel(GameObject gameObject, string label)
    {
        if (gameObject.name.IndexOf(label, System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        foreach (TMP_Text tmp in gameObject.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp != null && tmp.text != null && tmp.text.IndexOf(label, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        foreach (Text text in gameObject.GetComponentsInChildren<Text>(true))
        {
            if (text != null && text.text != null && text.text.IndexOf(label, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject FindDeepChild(Transform parent, string targetName)
    {
        if (parent.name == targetName)
        {
            return parent.gameObject;
        }

        foreach (Transform child in parent)
        {
            GameObject found = FindDeepChild(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
