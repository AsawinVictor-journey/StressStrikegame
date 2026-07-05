using UnityEngine;

public class PoseTarget : MonoBehaviour
{
    bool touched = false;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("ชนแล้ว : " + other.name);

        if (other.CompareTag("Hand"))
        {
            touched = true;
            Debug.Log("PASS");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Hand"))
        {
            touched = false;
        }
    }

    public bool IsTouched()
    {
        return touched;
    }
}