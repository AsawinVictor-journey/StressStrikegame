using UnityEngine;
using TMPro;

public class YogaUI : MonoBehaviour
{
    public YogaManager yogaManager;
    public TMP_Text descriptionText;


    public void ShowPose()
    {
        if(yogaManager.selectedPose == null)
            return;

        descriptionText.text = yogaManager.selectedPose.description;
    }
}