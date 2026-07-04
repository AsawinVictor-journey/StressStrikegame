using UnityEngine;

public class RingMove : MonoBehaviour
{
    public float speed = 2f;

    Transform player;

    void Start()
    {
        player = Camera.main.transform;
    }

    void Update()
    {
        if (player == null) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            player.position,
            speed * Time.deltaTime
        );

        // ???????????????????
        if (Vector3.Distance(transform.position, player.position) < 0.2f)
        {
            Destroy(gameObject);
        }
    }
}