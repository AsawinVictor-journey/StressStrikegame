using UnityEngine;

public class CameraSwitch : MonoBehaviour
{
    public Camera mainCam;
    public Camera otherCam;

    void Start()
    {
        mainCam.enabled = true;
        otherCam.enabled = false;
    }
}