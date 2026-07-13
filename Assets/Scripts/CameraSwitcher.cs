using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Cameras")]
    public Camera[] cameras;

    [Header("Audio Listener")]
    // Only one AudioListener should be active at a time; leave true unless you're managing it elsewhere.
    public bool moveAudioListener = true;

    private int activeIndex = 0;

    private void Start()
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null) cameras[i].gameObject.SetActive(i == activeIndex);
        }
    }

    // Hook to a Button's OnClick to cycle to the next camera.
    public void SwitchToNextCamera()
    {
        SwitchToCamera((activeIndex + 1) % cameras.Length);
    }

    // Hook to a Button's OnClick and set the index in the Inspector to jump to a specific camera.
    public void SwitchToCamera(int index)
    {
        if (cameras == null || index < 0 || index >= cameras.Length || cameras[index] == null) return;

        cameras[activeIndex].gameObject.SetActive(false);
        cameras[index].gameObject.SetActive(true);

        if (moveAudioListener)
        {
            var oldListener = cameras[activeIndex].GetComponent<AudioListener>();
            var newListener = cameras[index].GetComponent<AudioListener>();
            if (oldListener != null) oldListener.enabled = false;
            if (newListener != null) newListener.enabled = true;
        }

        activeIndex = index;
    }
}
