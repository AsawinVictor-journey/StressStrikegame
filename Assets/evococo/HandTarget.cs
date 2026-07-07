using UnityEngine;

namespace Evococo
{
public class HandTarget : MonoBehaviour
{
    private Renderer rd;

    void Start()
    {
        rd = GetComponentInChildren<Renderer>();

        if (rd != null)
            rd.material.color = Color.red;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<MouseHand>() != null)
        {
            if (rd != null)
                rd.material.color = Color.green;

            Debug.Log(name + " OK");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<MouseHand>() != null)
        {
            if (rd != null)
                rd.material.color = Color.red;
        }
    }
}
}