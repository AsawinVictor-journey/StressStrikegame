using UnityEngine;

public class TargetHit : MonoBehaviour
{
    private Transform hand;
    private Renderer ring;

    public float hitDistance = 0.15f;

    void Start()
    {
        hand = GameObject.Find("MouseHand").transform;
        ring = GetComponent<Renderer>();
    }

    void Update()
    {
        if (hand == null) return;

        float d = Vector3.Distance(hand.position, transform.position);

        if (d <= hitDistance)
            ring.material.color = Color.green;
        else
            ring.material.color = Color.red;
    }
}