using UnityEngine;

public class RingSpawner : MonoBehaviour
{
    public GameObject ringPrefab;

    public float spawnDistance = 5f;
    public float spawnInterval = 3f;

    float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0;

            Camera cam = Camera.main;

            if (cam == null) return;

            float randomX = Random.Range(-0.8f, 0.8f);
            float randomY = Random.Range(-0.5f, 0.5f);

            Vector3 spawnPos =
                cam.transform.position +
                cam.transform.forward * spawnDistance +
                cam.transform.right * randomX +
                cam.transform.up * randomY;

            Quaternion rot =
                Quaternion.LookRotation(-cam.transform.forward) *
                Quaternion.Euler(90, 0, 0);

            Instantiate(
                ringPrefab,
                spawnPos,
                rot
            );
        }
    }
}