using UnityEngine;

public class RingMove : MonoBehaviour
{
    private Transform hitPoint;
    public float speed = 3f;

    void Start()
    {
        hitPoint = GameObject.Find("HitPoint").transform;
    }

    void Update()
    {
        if (hitPoint == null) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            hitPoint.position,
            speed * Time.deltaTime
        );
    }
}