using UnityEngine;

public class RingColor : MonoBehaviour
{
    Renderer rd;

    void Start()
    {
        rd = GetComponent<Renderer>();
        rd.material.color = Color.red;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<MouseHand>() != null)
        {
            Debug.Log("Perfect");
            rd.material.color = Color.green;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<MouseHand>() != null)
        {
            rd.material.color = Color.red;
        }
    }
}