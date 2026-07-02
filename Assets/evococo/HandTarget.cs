using UnityEngine;

public class HandTarget : MonoBehaviour
{
    public Transform hand;      // มือผู้เล่น
    public Renderer target;     // Renderer ของ Sphere

    public float hitDistance = 0.10f;

    void Update()
    {
        float distance = Vector3.Distance(hand.position, transform.position);

        if (distance <= hitDistance)
        {
            target.material.color = Color.green;
        }
        else
        {
            target.material.color = Color.red;
        }
    }
}