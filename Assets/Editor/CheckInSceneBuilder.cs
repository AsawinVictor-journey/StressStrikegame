using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Re-runnable builder for the per-session AI check-in UI
// (CheckInManager + CheckInSceneRouter). Lives inside MainMenuScene.unity as
// its own overlay canvas, the same way the Brief-COPE survey's SurveyCanvas
// does - BriefCopeSurveyController activates it once the survey finishes,
// is skipped, or (for a returning player) immediately. Re-running this
// rebuilds the CheckIn canvas from scratch and re-adds the mode scenes to
// Build Settings without duplicating existing entries.
public static class CheckInSceneBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
    // Earlier iteration built a standalone scene for this; it's obsolete now
    // that the check-in UI lives inside MainMenuScene, so clean it up on rebuild.
    private const string ObsoleteCheckInScenePath = "Assets/Scenes/CheckIn.unity";

    [MenuItem("Tools/Check-In/Build Check-In UI (in MainMenuScene)")]
    public static void BuildCheckInUI()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        // ResultPanel is Brief-COPE's original hand-authored result screen (wordmark
        // art, layout, etc.) - preserve it across rebuilds instead of regenerating it.
        // CheckInCanvas is inactive by the end of every build, so GameObject.Find
        // (which skips inactive objects) can't be used to locate prior runs' output -
        // walk the hierarchy directly and destroy every match, not just the first.
        GameObject preservedResultPanel = null;
        foreach (var oldCanvas in FindAllInScene(scene, "CheckInCanvas"))
        {
            if (preservedResultPanel == null)
            {
                var oldResultPanel = FindDeepChild(oldCanvas.transform, "ResultPanel");
                if (oldResultPanel != null)
                {
                    preservedResultPanel = oldResultPanel.gameObject;
                    preservedResultPanel.transform.SetParent(null, true);
                }
            }
            Object.DestroyImmediate(oldCanvas);
        }

        int surveySortingOrder = 0;
        var surveyCanvasGo = GameObject.Find("SurveyCanvas");
        if (surveyCanvasGo != null)
        {
            var surveyCanvas = surveyCanvasGo.GetComponent<Canvas>();
            if (surveyCanvas != null) surveySortingOrder = surveyCanvas.sortingOrder;
        }

        var canvasGo = new GameObject("CheckInCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the survey canvas so it's never hidden behind it during handoff.
        canvas.sortingOrder = surveySortingOrder + 1;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Hidden until BriefCopeSurveyController hands off to it.
        canvasGo.SetActive(false);

        // Everything except ResultPanel/CheckInManager/CheckInSceneRouter lives under
        // CheckInBody so CheckInManager can hide the whole input UI in one SetActive
        // call once a mode is decided, leaving only the result panel visible.
        var bodyGo = CreateUIObject("CheckInBody", canvasGo.transform);
        StretchFull(bodyGo.GetComponent<RectTransform>());

        var bg = CreateUIObject("Background", bodyGo.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.06f, 0.09f, 1f);
        StretchFull(bg.GetComponent<RectTransform>());

        var title = CreateText("TitleText", bodyGo.transform, "How are you feeling right now?", 48, TextAlignmentOptions.Center);
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -120);
        titleRt.sizeDelta = new Vector2(1200, 100);

        var chipRow = CreateUIObject("MoodChipRow", bodyGo.transform);
        var chipRt = chipRow.GetComponent<RectTransform>();
        chipRt.anchorMin = new Vector2(0.5f, 0.6f);
        chipRt.anchorMax = new Vector2(0.5f, 0.6f);
        chipRt.pivot = new Vector2(0.5f, 0.5f);
        chipRt.sizeDelta = new Vector2(1200, 160);
        var hlg = chipRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Button angryChip = CreateChip(chipRow.transform, "AngryChip", "Angry");
        Button anxiousChip = CreateChip(chipRow.transform, "AnxiousChip", "Anxious");
        Button tiredChip = CreateChip(chipRow.transform, "TiredChip", "Tired");
        Button calmChip = CreateChip(chipRow.transform, "CalmChip", "Calm");

        var inputGo = CreateUIObject("FreeTextInput", bodyGo.transform);
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.5f, 0.4f);
        inputRt.anchorMax = new Vector2(0.5f, 0.4f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta = new Vector2(900, 90);
        var inputImg = inputGo.AddComponent<Image>();
        inputImg.color = new Color(1f, 1f, 1f, 0.08f);
        var inputField = inputGo.AddComponent<TMP_InputField>();

        var textArea = CreateUIObject("TextArea", inputGo.transform);
        var textAreaRt = textArea.GetComponent<RectTransform>();
        StretchFull(textAreaRt, 16, 8);
        textArea.AddComponent<RectMask2D>();

        var placeholder = CreateText("Placeholder", textArea.transform, "Or type how you're doing... (optional)", 30, TextAlignmentOptions.MidlineLeft);
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        StretchFull(placeholder.GetComponent<RectTransform>());

        var inputText = CreateText("Text", textArea.transform, "", 30, TextAlignmentOptions.MidlineLeft);
        StretchFull(inputText.GetComponent<RectTransform>());

        inputField.textViewport = textAreaRt;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;

        Button submitButton = CreateButton(bodyGo.transform, "SubmitButton", "Check In", new Vector2(0.5f, 0.26f), new Vector2(340, 90));
        // Always visible, corner-anchored so it never gets covered by the chips/input above.
        Button skipButton = CreateButton(bodyGo.transform, "SkipButton", "Skip", new Vector2(0.92f, 0.94f), new Vector2(180, 70));

        var status = CreateText("StatusText", bodyGo.transform, "", 28, TextAlignmentOptions.Center);
        // Not a click target - without this it sits on top of (and eats clicks meant
        // for) SubmitButton/SkipButton since it's later in the sibling order.
        status.raycastTarget = false;
        var statusRt = status.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0.5f, 0.16f);
        statusRt.anchorMax = new Vector2(0.5f, 0.16f);
        statusRt.pivot = new Vector2(0.5f, 0.5f);
        statusRt.sizeDelta = new Vector2(900, 60);

        var managerGo = new GameObject("CheckInManager", typeof(CheckInManager));
        managerGo.transform.SetParent(canvasGo.transform, false);
        var manager = managerGo.GetComponent<CheckInManager>();

        var managerSo = new SerializedObject(manager);
        managerSo.FindProperty("angryChip").objectReferenceValue = angryChip;
        managerSo.FindProperty("anxiousChip").objectReferenceValue = anxiousChip;
        managerSo.FindProperty("tiredChip").objectReferenceValue = tiredChip;
        managerSo.FindProperty("calmChip").objectReferenceValue = calmChip;
        managerSo.FindProperty("freeTextInput").objectReferenceValue = inputField;
        managerSo.FindProperty("submitButton").objectReferenceValue = submitButton;
        managerSo.FindProperty("skipButton").objectReferenceValue = skipButton;
        managerSo.FindProperty("checkInCanvas").objectReferenceValue = canvasGo;
        managerSo.FindProperty("checkInBody").objectReferenceValue = bodyGo;
        managerSo.FindProperty("statusText").objectReferenceValue = status;
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        var routerGo = new GameObject("CheckInSceneRouter", typeof(CheckInSceneRouter));
        routerGo.transform.SetParent(canvasGo.transform, false);
        var router = routerGo.GetComponent<CheckInSceneRouter>();
        var routerSo = new SerializedObject(router);
        routerSo.FindProperty("checkInManager").objectReferenceValue = manager;
        routerSo.ApplyModifiedPropertiesWithoutUndo();

        // Reuse Brief-COPE's original ResultPanel (ReasonText/DisclaimerText/
        // ModeWordmark/FinishButton) as the check-in's result screen instead of
        // building a new one - it's only ever found once (preserved across
        // rebuilds above), never regenerated.
        GameObject resultPanelGo = preservedResultPanel != null
            ? preservedResultPanel
            : FindInScene(scene, "ResultPanel");

        if (resultPanelGo == null)
        {
            Debug.LogWarning("CheckInSceneBuilder: no ResultPanel found to reuse - the check-in result screen was not wired.");
        }
        else
        {
            resultPanelGo.transform.SetParent(canvasGo.transform, false);
            resultPanelGo.SetActive(false);

            var resultReasonText = FindDeepChild(resultPanelGo.transform, "ReasonText")?.GetComponent<TMP_Text>();
            var resultDisclaimerText = FindDeepChild(resultPanelGo.transform, "DisclaimerText")?.GetComponent<TMP_Text>();
            var resultModeWordmark = FindDeepChild(resultPanelGo.transform, "ModeWordmark")?.GetComponent<Image>();
            var resultContinueButton = FindDeepChild(resultPanelGo.transform, "FinishButton")?.GetComponent<Button>();

            var checkInResultPanel = resultPanelGo.GetComponent<CheckInResultPanel>();
            if (checkInResultPanel == null) checkInResultPanel = resultPanelGo.AddComponent<CheckInResultPanel>();

            var resultSo = new SerializedObject(checkInResultPanel);
            resultSo.FindProperty("resultPanel").objectReferenceValue = resultPanelGo;
            resultSo.FindProperty("reasonText").objectReferenceValue = resultReasonText;
            resultSo.FindProperty("disclaimerText").objectReferenceValue = resultDisclaimerText;
            resultSo.FindProperty("modeWordmark").objectReferenceValue = resultModeWordmark;
            resultSo.FindProperty("boxingWordmark").objectReferenceValue = LoadSpriteByGuid("919ceb9d20c9aff4dbe7e037be891975");
            resultSo.FindProperty("rageRoomWordmark").objectReferenceValue = LoadSpriteByGuid("e507e11844328ba4c8e719b12f4d42f0");
            resultSo.FindProperty("yogaWordmark").objectReferenceValue = LoadSpriteByGuid("5f72bdc1840359c4780105b91f613e3c");
            resultSo.FindProperty("continueButton").objectReferenceValue = resultContinueButton;
            resultSo.ApplyModifiedPropertiesWithoutUndo();

            routerSo.FindProperty("resultPanel").objectReferenceValue = checkInResultPanel;
            routerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // MainMenuScene already has an EventSystem; only add one if it's somehow missing.
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var briefCope = Object.FindFirstObjectByType<BriefCopeSurveyController>();
        if (briefCope != null)
        {
            var briefCopeSo = new SerializedObject(briefCope);
            briefCopeSo.FindProperty("checkInCanvas").objectReferenceValue = canvasGo;
            briefCopeSo.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("CheckInSceneBuilder: no BriefCopeSurveyController found in MainMenuScene - wire its checkInCanvas field manually.");
        }

        EditorSceneManager.SaveScene(scene);

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ObsoleteCheckInScenePath) != null)
        {
            RemoveSceneFromBuildSettings(ObsoleteCheckInScenePath);
            AssetDatabase.DeleteAsset(ObsoleteCheckInScenePath);
        }

        AddSceneToBuildSettings("Assets/b-o-o-k/BoxingMenu.unity");
        AddSceneToBuildSettings("Assets/Scenes/Rage Room/Rage Room.unity");
        AddSceneToBuildSettings("Assets/Scenes/Rage Room/Rage Room Menu.unity");
        AddSceneToBuildSettings("Assets/Scenes/Yoga/Yoga.unity");
        AddSceneToBuildSettings("Assets/Scenes/Yoga/Yoga Menu.unity");

        Debug.Log("CheckInSceneBuilder: built the CheckIn UI inside MainMenuScene.unity and confirmed Boxing/Rage Room/Yoga scenes are in Build Settings.");
    }

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        var existing = scenes.FirstOrDefault(s => s.path == path);
        if (existing != null)
        {
            existing.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void RemoveSceneFromBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToArray();
        EditorBuildSettings.scenes = scenes;
    }

    // GameObject.Find skips inactive objects (ResultPanel normally is, at edit
    // time) - walk the hierarchy directly instead so it's still found.
    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindDeepChild(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static System.Collections.Generic.List<GameObject> FindAllInScene(Scene scene, string name)
    {
        var results = new System.Collections.Generic.List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            CollectDeepChildren(root.transform, name, results);
        return results;
    }

    private static void CollectDeepChildren(Transform parent, string name, System.Collections.Generic.List<GameObject> results)
    {
        if (parent.name == name) results.Add(parent.gameObject);
        foreach (Transform child in parent)
            CollectDeepChildren(child, name, results);
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Sprite LoadSpriteByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
    {
        var go = CreateUIObject(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateChip(Transform parent, string name, string label)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240, 140);

        var img = go.AddComponent<Image>();
        img.color = Color.white;

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var text = CreateText("Label", go.transform, label, 32, TextAlignmentOptions.Center);
        text.color = Color.black;
        StretchFull(text.GetComponent<RectTransform>());

        return button;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 size)
    {
        var go = CreateUIObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.35f, 0.37f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var text = CreateText("Label", go.transform, label, 30, TextAlignmentOptions.Center);
        text.color = Color.white;
        StretchFull(text.GetComponent<RectTransform>());

        return button;
    }
}
