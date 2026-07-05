using UnityEngine;

public class DualHandSimulator : MonoBehaviour
{
    [Header("Left Hand (WASD)")]
    public Transform leftHand;
    public float leftHandSpeed = 10f;
    private Vector2 leftHandPos = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f);

    [Header("Right Hand (Mouse)")]
    public Transform rightHand;
    
    [Header("Settings")]
    public float distance = 8f;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        
        // Disable old mouse hand if exists
        var oldHand = GameObject.Find("MouseHand");
        if (oldHand != null) oldHand.SetActive(false);
    }

    void Update()
    {
        if (cam == null) return;

        // --- Left Hand (WASD) ---
        if (leftHand != null)
        {
            float h = Input.GetAxis("Horizontal"); // A/D
            float v = Input.GetAxis("Vertical");   // W/S
            
            leftHandPos.x += h * leftHandSpeed * Time.deltaTime * 100f;
            leftHandPos.y += v * leftHandSpeed * Time.deltaTime * 100f;
            
            // Clamp to screen
            leftHandPos.x = Mathf.Clamp(leftHandPos.x, 0, Screen.width);
            leftHandPos.y = Mathf.Clamp(leftHandPos.y, 0, Screen.height);

            Ray leftRay = cam.ScreenPointToRay(leftHandPos);
            leftHand.position = leftRay.GetPoint(distance);
        }

        // --- Right Hand (Mouse) ---
        if (rightHand != null)
        {
            Vector3 mouse = Input.mousePosition;
            if (mouse.x >= 0 && mouse.x <= Screen.width && mouse.y >= 0 && mouse.y <= Screen.height)
            {
                Ray rightRay = cam.ScreenPointToRay(mouse);
                rightHand.position = rightRay.GetPoint(distance);
            }
        }
    }
}
