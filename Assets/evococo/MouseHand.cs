using UnityEngine;

public class MouseHand : MonoBehaviour
{
    public float distance = 5f;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (cam == null)
            return;

        if (!Application.isFocused)
            return;

        Vector3 mouse = Input.mousePosition;

        if (mouse.x < 0 ||
            mouse.y < 0 ||
            mouse.x > Screen.width ||
            mouse.y > Screen.height)
            return;

        Ray ray = cam.ScreenPointToRay(mouse);

        transform.position = ray.GetPoint(distance);
    }
}