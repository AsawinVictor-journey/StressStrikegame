using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class BriefCopeEditorUtils
{
    [MenuItem("Tools/Brief-COPE/Reset Survey State")]
    public static void ResetSurveyState()
    {
        // 1. Delete PlayerPrefs key
        PlayerPrefs.DeleteKey("BriefCope_LastResult");
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs: Deleted BriefCope_LastResult.");

        // 2. Find and activate SurveyCanvas in the active scene
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject surveyCanvas = null;
        foreach (var go in allObjects)
        {
            if (go.name == "SurveyCanvas")
            {
                surveyCanvas = go;
                break;
            }
        }

        if (surveyCanvas != null)
        {
            surveyCanvas.SetActive(true);
            
            // Reactivate the controller script GameObject if it's separate
            var controller = GameObject.FindAnyObjectByType<BriefCopeSurveyController>(FindObjectsInactive.Include);
            if (controller != null)
            {
                controller.gameObject.SetActive(true);
            }
            
            // Enable the backdrop and set raycast blocking
            var backdrop = surveyCanvas.transform.Find("Backdrop")?.gameObject;
            if (backdrop != null)
            {
                backdrop.SetActive(true);
                var img = backdrop.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.raycastTarget = true;
                }
            }

            // Reset sub-panels to intro state
            var introPanel = surveyCanvas.transform.Find("IntroPanel")?.gameObject;
            if (introPanel != null)
            {
                introPanel.SetActive(true);
            }
            var questionPanel = surveyCanvas.transform.Find("QuestionPanel")?.gameObject;
            if (questionPanel != null) questionPanel.SetActive(false);
            var halfwayPanel = surveyCanvas.transform.Find("HalfwayPanel")?.gameObject;
            if (halfwayPanel != null) halfwayPanel.SetActive(false);
            var resultPanel = surveyCanvas.transform.Find("ResultPanel")?.gameObject;
            if (resultPanel != null) resultPanel.SetActive(false);

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("SurveyCanvas was reactivated and initialized in the active scene.");
        }
        else
        {
            Debug.LogWarning("SurveyCanvas not found in the current active scene!");
        }

        // Clean up old Recommendation highlights
        var oldGlows = new System.Collections.Generic.List<GameObject>();
        foreach (var g in GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (g.name.StartsWith("RecommendedGlow") || g.name.StartsWith("RecommendedStar"))
            {
                oldGlows.Add(g);
            }
        }
        foreach (var og in oldGlows)
        {
            GameObject.DestroyImmediate(og);
        }
        if (oldGlows.Count > 0)
        {
            Debug.Log($"Cleaned up {oldGlows.Count} legacy highlight effects.");
        }
    }
}
