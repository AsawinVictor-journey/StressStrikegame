using UnityEngine;

public class MouseHand : MonoBehaviour
{
    public float distance = 8f;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) return;

        Vector3 mouse = Input.mousePosition;

        // ถ้าเมาส์อยู่นอก Game View ไม่ทำงาน
        if (mouse.x < 0 || mouse.x > Screen.width ||
            mouse.y < 0 || mouse.y > Screen.height)
            return;

        Ray ray = cam.ScreenPointToRay(mouse);

        transform.position = ray.GetPoint(distance);
    }
}