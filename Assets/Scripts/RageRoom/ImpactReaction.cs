using UnityEngine;

/// <summary>
/// Adds physically correct shake and wobble to any Rigidbody on impact.
///
/// How the shake works:
///   AddForceAtPosition at the contact point decomposes into linear push +
///   angular torque automatically. The torque magnitude is:
///     τ = (contactPoint − centerOfMass) × force
///   The further from center, the more spin. No separate AddTorque needed.
///
///   ForceMode.Impulse divides by rb.mass, so heavy objects react less than
///   light ones from the same impact — no manual mass compensation required.
///
///   Rocking / shake is emergent: rotation + surface friction + gravity make
///   the object wobble and settle. Tune angularDrag on the Rigidbody (2–4)
///   for a satisfying settle without endless spinning.
///
/// Setup:
///   Add this component to any object you want to react to punches.
///   Make sure the object's Rigidbody angularDrag is 2–4 (Inspector).
///   The object must be tagged "Hand" for the filter to match by default,
///   OR set requireHandTag = false to react to any collision.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ImpactReaction : MonoBehaviour
{
    [Tooltip("Scales the final impulse. Tune this after setting forceExponent.\n" +
             "This value is in N·s, and ForceMode.Impulse divides it by mass to get " +
             "a velocity change directly — so at forceExponent 2 and maxImpactSpeed 12, " +
             "a multiplier of 1 produces a 144 N·s impulse, which throws a 10 kg monitor " +
             "at 14 m/s. Keep it small: 0.05-0.2 is the useful range.")]
    public float forceMultiplier = 0.1f;

    [Tooltip("Power curve applied to impact speed before multiplying by forceMultiplier.\n" +
             "1 = linear (force grows with speed)\n" +
             "2 = quadratic (force grows with speed²) — matches kinetic energy, fast hits feel brutal\n" +
             "1.5 = between the two, good starting point for a rage room.")]
    public float forceExponent = 2f;

    [Tooltip("Minimum relative velocity (m/s) between the colliding bodies required " +
             "to trigger a reaction. Filters resting contacts and gentle brushes.")]
    public float minImpactSpeed = 1.5f;

    [Tooltip("Impact speed is clamped to this before force calculation. Prevents a " +
             "single extreme hit from sending the object to infinity.")]
    public float maxImpactSpeed = 12f;

    [Tooltip("If true, only react to collisions from objects tagged 'Hand'. " +
             "Set false to also react to flying debris for chain reactions.")]
    public bool requireHandTag = true;

    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (requireHandTag && !collision.gameObject.CompareTag("Hand")) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed) return;

        ContactPoint contact = collision.GetContact(0);

        // Clamp speed before force calculation to keep extreme hits stable.
        float clampedSpeed = Mathf.Min(impactSpeed, maxImpactSpeed);

        // Power curve: slow taps stay weak, fast punches scale up sharply.
        // forceExponent = 2 means twice the speed = four times the force.
        float forceMag = Mathf.Pow(clampedSpeed, forceExponent) * forceMultiplier;

        // contact.normal points from the incoming collider into this object —
        // the correct push direction. No negation needed.
        Vector3 force = contact.normal * forceMag;

        // Single call: linear push + angular torque from lever arm at contact point.
        //
        // ForceMode.Impulse applies force / rb.mass as ΔV — note there is NO
        // Time.fixedDeltaTime term (that is ForceMode.Force). An earlier comment
        // here claimed otherwise, and forceMultiplier was tuned against that wrong
        // model, so every impulse landed 1/0.02 = 50x harder than intended. That
        // is what launched objects across the room and spun them wildly. Mass IS
        // still respected automatically, which is why heavier objects move less.
        rb.AddForceAtPosition(force, contact.point, ForceMode.Impulse);
    }
}
