using UnityEngine;

public class WallMove : MonoBehaviour
{
    public float speed = 2f;

    private Transform player;

    void Start()
    {
        player = Camera.main.transform;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            player.position,
            speed * Time.deltaTime
        );

        // ลบเมื่อถึงผู้เล่น
        if (Vector3.Distance(transform.position, player.position) < 0.5f)
        {
            Destroy(gameObject);
        }
    }
}