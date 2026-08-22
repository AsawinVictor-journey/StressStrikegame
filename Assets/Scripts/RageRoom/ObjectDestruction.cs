using System.Diagnostics;
using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    public float Health = 5f;

    [Header("Damage")]
    [Tooltip("Power curve applied to impact speed. 2 = damage scales with speed² so fast hits deal disproportionately more damage than slow ones.")]
    public float damageExponent = 2f;
    [Tooltip("Multiplier on the final damage value. Lower this when using exponent > 1 to rebalance.")]
    public float damageMultiplier = 0.3f;

    [Header("Destruction Settings")]
    public GameObject fragmentPrefab;
    public int minPieces = 5;
    public int maxPieces = 15;

    public float minScale = 0.2f;
    public float maxScale = 1.0f;
    public float hitCooldown = 0.15f;

    [Tooltip("Seconds a spawned fragment stays live before returning to the pool " +
             "(or being destroyed, if no FragmentPool is in the scene). Fragments " +
             "were never cleaned up before, so a long session's worth of broken " +
             "objects accumulated Rigidbodies forever and the physics solver got " +
             "steadily heavier the longer you played.")]
    public float fragmentLifetime = 5f;

    public float explosionForce = 6f;
    public float explosionRadius = 2f;

    private bool isBroken = false;
    private ObjectScore objectScore;
    private float lastHitTime;

    void Awake()
    {
        objectScore = GetComponent<ObjectScore>();
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;

        if (!collision.gameObject.CompareTag("Hand")) return;

        float velocity = collision.relativeVelocity.magnitude;

        if (velocity < 7f) return;

        if (Time.time - lastHitTime < hitCooldown)
            return;
        lastHitTime = Time.time;

        // The dedicated punch Hitbox's own collision messages aren't reliable
        // (see PunchController.SpawnImpactEffect), so the VFX is triggered from
        // here instead — this OnCollisionEnter is what actually fires on every
        // landed punch.
        collision.rigidbody?.GetComponent<PunchController>()?.SpawnImpactEffect(collision);

        // Null-conditional to match GameManager's usage. A destructible can
        // outlive the ScoreSystem during scene teardown, and an unguarded call
        // throws there — killing the rest of this method, so the object takes
        // no damage and never breaks.
        ScoreSystem.Instance?.AddHit(velocity);

        float impact = Mathf.Pow(collision.relativeVelocity.magnitude, damageExponent) * damageMultiplier;
        Health -= impact;

        if (Health <= 0)
        {
            BreakObject(collision);
        }
    }

    void BreakObject(Collision collision)
    {
        isBroken = true;

        if (objectScore != null)
        {
            ScoreSystem.Instance?.AddScore(objectScore.score);
        }

        int pieces = Random.Range(minPieces, maxPieces);

        for (int i = 0; i < pieces; i++)
        {
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * 0.2f;

            // Pooled: reuses a deactivated fragment instead of Instantiate()'ing a fresh
            // GameObject every break. Falls back to a plain Instantiate if no FragmentPool
            // exists in the scene, so this still works without one wired up.
            GameObject frag = FragmentPool.Instance != null
                ? FragmentPool.Instance.Get(fragmentPrefab, spawnPos, Random.rotation)
                : Instantiate(fragmentPrefab, spawnPos, Random.rotation);

            float scale = Random.Range(minScale, maxScale);
            frag.transform.localScale = Vector3.one * scale;

            Rigidbody rb = frag.GetComponent<Rigidbody>();

            if (rb != null)
            {
                // Speculative, not ContinuousDynamic. Full CCD is the most
                // expensive collision mode there is, and a destroyed desk spawns
                // up to 15 of these at once — several objects broken in quick
                // succession put 40+ CCD bodies in the solver at the same time,
                // for debris the player only ever sees tumbling. Speculative
                // still prevents tunnelling through the floor at a fraction of
                // the cost, and interpolation keeps them smooth above 50 Hz.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.interpolation          = RigidbodyInterpolation.Interpolate;
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

            // Pooled fragments are deactivated and returned to FragmentPool instead of
            // destroyed, so their GameObjects get reused by the next break instead of
            // triggering a fresh Instantiate/GC cycle.
            if (FragmentPool.Instance != null)
                FragmentPool.Instance.Release(frag, fragmentPrefab, fragmentLifetime);
            else
                Destroy(frag, fragmentLifetime);
        }
        GameManager.Instance.ObjectDestroyed();

        Destroy(gameObject);
    }
}