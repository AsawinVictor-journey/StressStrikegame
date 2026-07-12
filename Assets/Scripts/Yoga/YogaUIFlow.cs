using UnityEngine;

public class YogaUIFlow : MonoBehaviour
{
    public UIFade fadeUI;

    public CanvasGroup poseSelection;
    public CanvasGroup description;

    public void SelectPose()
    {
        fadeUI.SwitchUI(poseSelection, description);
    }

    public void StartPose()
    {
        fadeUI.HideUI(description);
    }
}