using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class AddScoreManager {
    [MenuItem("Window/Add ScoreManager to Scenes")]
    public static void AddScoreManagerToScenes() {
        string[] scenePaths = {
            "Assets/Scenes/Level 1.unity",
            "Assets/Scenes/Level 2.unity",
            "Assets/Scenes/Level 3.unity",
            "Assets/Scenes/copy of current.unity"
        };

        foreach (string scenePath in scenePaths) {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            
            // Find or create ScoreManager GameObject
            GameObject scoreManagerGO = GameObject.Find("ScoreManager");
            if (scoreManagerGO == null) {
                scoreManagerGO = new GameObject("ScoreManager");
            }
            
            // Add ScoreManager component if not present
            if (scoreManagerGO.GetComponent<ScoreManager>() == null) {
                scoreManagerGO.AddComponent<ScoreManager>();
            }
            
            // Save the scene
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Added ScoreManager to: " + scenePath);
        }
    }
}
