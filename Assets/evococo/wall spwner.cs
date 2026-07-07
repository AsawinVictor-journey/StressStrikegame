using UnityEngine;

public class WallSpawner : MonoBehaviour
{
    public GameObject wallPrefab;

    public float interval = 6f;

    float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0;

            Camera cam = Camera.main;

            Vector3 spawnPos =
                cam.transform.position +
                cam.transform.forward * 10f;

            Instantiate(
                wallPrefab,
                spawnPos,
                Quaternion.LookRotation(-cam.transform.forward)
            );
        }
    }
}
