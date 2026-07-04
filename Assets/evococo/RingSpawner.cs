using UnityEngine;

public class RingSpawner : MonoBehaviour
{
    public GameObject ringPrefab;
    public Transform spawnPoint;
    public Transform hitPoint;

    public float spawnInterval = 2f;

    float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0;

            GameObject ring =
                Instantiate(ringPrefab,
                            spawnPoint.position,
                            spawnPoint.rotation);

            RingMove move = ring.GetComponent<RingMove>();

            if (move != null)
                move.hitPoint = hitPoint;
        }
    }
}