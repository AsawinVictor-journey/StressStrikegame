using UnityEngine;

public class CameraTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("===CAMERA===");
        
        foreach (var device in WebCamTexture.devices)
        {
            Debug.Log($"Camera: {device.name}");
        }
    }
}