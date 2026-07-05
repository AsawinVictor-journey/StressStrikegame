#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class SetupMeditationUI
{
    [MenuItem("Tools/Setup Meditation UI")]
    public static void Setup()
    {
        var hudCanvas = GameObject.Find("MeditationHUDCanvas");
        if (hudCanvas == null) {
            Debug.LogError("HUD Canvas not found!");
            return;
        }
        var hudScript = hudCanvas.GetComponent<MeditationHUD>();

        // 1. Hands System
        var simObj = GameObject.Find("DualHandSimulator");
        if (simObj == null) simObj = new GameObject("DualHandSimulator");
        var sim = simObj.GetComponent<DualHandSimulator>();
        if (sim == null) sim = simObj.AddComponent<DualHandSimulator>();

        var leftHand = GameObject.Find("LeftHand");
        if (leftHand == null) leftHand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftHand.name = "LeftHand";
        leftHand.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        leftHand.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.blue };

        var rightHand = GameObject.Find("MouseHand");
        if (rightHand == null) rightHand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightHand.name = "MouseHand";
        rightHand.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        rightHand.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.red };

        sim.leftHand = leftHand.transform;
        sim.rightHand = rightHand.transform;

        // 2. Combo & Grade Text
        var comboObj = GameObject.Find("ComboText");
        if (comboObj == null) {
            comboObj = new GameObject("ComboText");
            comboObj.transform.SetParent(hudCanvas.transform, false);
            var text = comboObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 48;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.text = "Combo: 0";
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(150, -120);
            hudScript.comboText = text;
        }

        var gradeObj = GameObject.Find("GradeText");
        if (gradeObj == null) {
            gradeObj = new GameObject("GradeText");
            gradeObj.transform.SetParent(hudCanvas.transform, false);
            var text = gradeObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 80;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "Perfect!";
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.6f);
            rect.anchorMax = new Vector2(0.5f, 0.6f);
            rect.anchoredPosition = Vector2.zero;
            hudScript.gradeText = text;
            gradeObj.SetActive(false);
        }

        // 3. Start Button
        var btnObj = GameObject.Find("StartButton");
        if (btnObj == null) {
            btnObj = new GameObject("StartButton");
            btnObj.transform.SetParent(hudCanvas.transform, false);
            var img = btnObj.AddComponent<Image>();
            img.color = Color.white;
            var btn = btnObj.AddComponent<Button>();
            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200, 80);
            
            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "START";
            btnText.fontSize = 40;
            btnText.color = Color.black;
            btnText.alignment = TextAlignmentOptions.Center;
            var tr = btnText.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            
            hudScript.startButton = btn;
        }

        // 4. End Screen Text
        var endScreenTextObj = GameObject.Find("EndScreenText");
        if (endScreenTextObj == null && hudScript.endScreenPanel != null) {
            endScreenTextObj = new GameObject("EndScreenText");
            endScreenTextObj.transform.SetParent(hudScript.endScreenPanel.transform, false);
            var text = endScreenTextObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 60;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = "Finished!";
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            hudScript.endScreenText = text;
        }

        // 5. Test Obstacle Wall
        var testWall = GameObject.Find("TestObstacle");
        if (testWall == null) {
            testWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testWall.name = "TestObstacle";
            testWall.transform.position = new Vector3(0, 1, 2);
            testWall.transform.localScale = new Vector3(2, 2, 0.2f);
            testWall.AddComponent<ObstacleWall>();
            testWall.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.magenta };
            testWall.SetActive(false);
            
            var sessionScript = Object.FindAnyObjectByType<MeditationSessionManager>();
            if (sessionScript != null && sessionScript.spawnSequence != null) {
                if (!sessionScript.spawnSequence.Contains(testWall)) {
                    sessionScript.spawnSequence.Add(testWall);
                }
            }
        }

        // Apply changes
        EditorUtility.SetDirty(hudScript);
        var manager = Object.FindAnyObjectByType<MeditationSessionManager>();
        if(manager != null) EditorUtility.SetDirty(manager);
        
        Debug.Log("UI and Test Objects Setup Successfully!");
    }
}
#endif
