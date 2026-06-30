using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PunchCollider : MonoBehaviour
{
    public float minDamageVelocity = 2f; // Minimum hand speed to register damage
    public float maxDamageVelocity = 15f; 
    public float maxDamage = 25f;

    private Rigidbody rb;
    private CombatHudController combatHud;
    private float lastImpactTime = 0f;
    private float impactCooldown = 0.3f; // Prevent rapid-fire hits on single punch

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        combatHud = FindObjectOfType<CombatHudController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time - lastImpactTime < impactCooldown) return;

        // Check if we hit the opponent (assuming the opponent has a specific component)
        if (collision.gameObject.GetComponentInParent<DummyRockingDoll>() != null || collision.gameObject.GetComponentInParent<BotAnimationControll>() != null)
        {
            float impactVelocity = collision.relativeVelocity.magnitude;

            if (impactVelocity >= minDamageVelocity)
            {
                lastImpactTime = Time.time;
                
                // Calculate damage based on velocity (linear mapping)
                float velocityT = Mathf.InverseLerp(minDamageVelocity, maxDamageVelocity, impactVelocity);
                float damageDealt = Mathf.Lerp(5f, maxDamage, velocityT); // Min 5 damage up to maxDamage

                if (combatHud != null)
                {
                    combatHud.DrainOpponentHealth(damageDealt);
                    combatHud.DrainPlayerStamina(5f); // Optional: drain stamina on physical hit
                    
                    // Call RegisterPunch for special ability meter manually since we bypassed animation controller
                    // Wait, we can just update the special bar directly if needed, or leave it for now.
                }

                // Add physical punch reaction to dummy
                Rigidbody enemyRb = collision.rigidbody;
                if (enemyRb != null)
                {
                    enemyRb.AddForce(collision.relativeVelocity * 50f, ForceMode.Impulse);
                }

                Debug.Log($"Punch landed! Velocity: {impactVelocity:F2}, Damage: {damageDealt:F2}");
            }
        }
    }
}
