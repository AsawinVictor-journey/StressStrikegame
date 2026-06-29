using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    public float Health = 5f;

    [Header("Destruction Settings")]
    public GameObject fragmentPrefab;
    public int minPieces = 5;
    public int maxPieces = 15;

    public float minScale = 0.2f;
    public float maxScale = 1.0f;

    public float explosionForce = 6f;
    public float explosionRadius = 2f;

    private bool isBroken = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;

        if (!collision.gameObject.CompareTag("Hand")) return;

        float impact = collision.relativeVelocity.magnitude;
        Health -= impact;

        if (Health <= 0)
        {
            BreakObject(collision);
        }
    }

    void BreakObject(Collision collision)
    {
        isBroken = true;

        int pieces = Random.Range(minPieces, maxPieces);

        for (int i = 0; i < pieces; i++)
        {
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * 0.2f;

            GameObject frag = Instantiate(fragmentPrefab, spawnPos, Random.rotation);

            // random size
            float scale = Random.Range(minScale, maxScale);
            frag.transform.localScale = Vector3.one * scale;

            Rigidbody rb = frag.GetComponent<Rigidbody>();

            if (rb != null)
            {
                Vector3 dir = (frag.transform.position - collision.transform.position).normalized;

                if (dir == Vector3.zero)
                    dir = Random.insideUnitSphere;

                rb.AddExplosionForce(
                    explosionForce,
                    transform.position,
                    explosionRadius
                );

                rb.AddForce(dir * Random.Range(1f, 4f), ForceMode.Impulse);
            }
        }

        Destroy(gameObject);
    }
}