using UnityEngine;
using UnityEngine.EventSystems;

public class RotateConstantly : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private bool pauseWhileDragging = true;

    [Header("Manual Drag")]
    [SerializeField] private float dragSensitivity = 0.3f;

    private bool isDragging = false;
    private Vector3 lastMousePosition;

    void Update()
    {
        HandleMouseDrag();

        if (pauseWhileDragging && isDragging) return;
        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);
    }

    private void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            SetDragging(true);
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            SetDragging(false);
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            lastMousePosition = Input.mousePosition;

            transform.Rotate(Vector3.up, -delta.x * dragSensitivity, Space.World);
            transform.Rotate(Vector3.right, delta.y * dragSensitivity, Space.World);
        }
    }

    public void SetDragging(bool dragging) => isDragging = dragging;
}