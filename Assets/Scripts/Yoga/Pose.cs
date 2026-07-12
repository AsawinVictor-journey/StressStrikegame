using UnityEngine;

public class PoseButton : MonoBehaviour
{
    public YogaPose pose;
    public YogaManager manager;


    public void ClickPose()
    {
        manager.SelectPose(pose);
    }
}