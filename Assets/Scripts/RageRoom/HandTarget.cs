using UnityEngine;

/// <summary>
/// Kinematic anchor — the target that PhysicsHandController's ConfigurableJoint chases.
///
/// ── Why this layer exists ─────────────────────────────────────────────────
///   The joint is physically correct. What was wrong was the target it chased:
///   a mathematically perfect point with no momentum, no history, no physical
///   character. A cursor, not an arm.
///
///   This script makes the target itself behave like an inertial object. Input
///   sets a desired velocity. A bounded acceleration rate controls how quickly
///   the target velocity reaches that desire. When input stops, drag decays the
///   velocity — the anchor coasts to a stop instead of snapping.
///
///   When direction reverses under full speed, the accumulated velocity persists
///   for the duration of the deceleration phase, carrying the anchor (and thus
///   the joint + hand) past the intended point before recovering. This is the
///   source of natural overshoot and "weighted" feel — not physics hacks, just
///   inertia in the target generation layer.
///
/// ── Signal chain ─────────────────────────────────────────────────────────
///   Input provider (keyboard / BNO055)
///     → desiredVelocity = GetMoveDirection() × maxSpeed
///   Inertia system (this script)
///     → currentVelocity accelerates toward desiredVelocity at ≤ acceleration m/s²
///     → when no input: currentVelocity decays exponentially at drag rate
///   Integrator (this script)
///     → localPos += currentVelocity × dt
///   ConfigurableJoint (PhysicsHandController, unchanged)
///     → physical hand follows this anchor via spring-damper inside PhysX
///
/// ── Tuning guide ─────────────────────────────────────────────────────────
///   maxSpeed      — top velocity of the anchor in m/s
///
///   acceleration  — m/s² rate at which velocity ramps up or changes direction
///                   Lower → heavier, more overshoot, slower reversal
///                   Higher → snappier, less overshoot
///                   Overshoot distance on full reversal ≈ maxSpeed² / (2 × acceleration)
///                   For subtle inertia with 0.8 m bounds: keep above 60
///                   For Boneworks-weight feel:            30–50
///
///   drag          — exponential decay rate when no input (per second)
///                   Higher → hand stops quickly on key release
///                   Lower  → hand coasts longer
///                   Half-life in seconds ≈ 0.693 / drag
///                   Snappy stop: 10–12.  Inertial coast: 4–6.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HandTarget : MonoBehaviour
{
    [Header("References")]
    public Transform         origin;
    public HandInputProvider input;

    [Header("Speed")]
    [Tooltip("Maximum velocity the anchor can reach (m/s).")]
    public float maxSpeed = 6f;

    [Header("Inertia")]
    [Tooltip("How quickly velocity changes toward the desired direction (m/s²). " +
             "Lower = heavier inertia, more overshoot on reversal. " +
             "Overshoot ≈ maxSpeed² / (2 × acceleration). " +
             "Balanced preset: 80.  Heavy / Boneworks-like: 35.")]
    public float acceleration = 80f;

    [Tooltip("Exponential velocity decay rate when no key is held (per second). " +
             "Half-life in seconds ≈ 0.693 / drag. " +
             "Snappy stop: 10.  Natural coast: 5.")]
    public float drag = 8f;

    [Header("Movement Bounds (local to origin)")]
    public float maxForward  = 0.8f;
    public float maxBackward = 0.8f;
    public float maxUp       = 0.8f;
    public float maxDown     = 0.8f;
    public float maxLeft     = 0.8f;
    public float maxRight    = 0.8f;

    [Header("Hand Separation")]
    public float maxPushDist = 0.15f;

    // Read by RageRoomCameraRotation (forwarded through PhysicsHandController).
    public Vector3 LocalPosition => localPos;

    [HideInInspector] public Collider handCollider;

    Vector3      localPos;
    Vector3      currentVelocity;   // anchor's current velocity in world-space axes
    HandTarget[] otherTargets;
    Rigidbody    rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic  = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        localPos = origin.InverseTransformPoint(transform.position);

        var all = FindObjectsByType<HandTarget>(FindObjectsSortMode.None);
        otherTargets = System.Array.FindAll(all, t => t != this);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // ── Desired velocity ──────────────────────────────────────────────
        // GetMoveDirection() returns a normalised world-axis vector (or zero).
        // Multiplying by maxSpeed gives the velocity the player is requesting.
        Vector3 desiredVelocity = input.GetMoveDirection() * maxSpeed;
        bool    hasInput        = desiredVelocity.sqrMagnitude > 0.001f;

        // ── Inertial velocity update ──────────────────────────────────────
        if (hasInput)
        {
            // Accelerate toward desired velocity, bounded per step.
            // When the player reverses direction, the clamp keeps the old
            // velocity alive for (speed / acceleration) seconds — this is
            // the deceleration phase that creates positional overshoot and
            // the "weighted arm" feel. No extra logic is needed; inertia
            // emerges from the bounded rate of change alone.
            Vector3 delta = desiredVelocity - currentVelocity;
            currentVelocity += Vector3.ClampMagnitude(delta, acceleration * dt);
        }
        else
        {
            // Exponential drag: hand coasts to a natural stop.
            // Multiply by the per-step decay factor (exact solution to dx/dt = -drag*x).
            currentVelocity *= Mathf.Exp(-drag * dt);

            // Zero out near-zero velocity to prevent indefinite drift.
            if (currentVelocity.sqrMagnitude < 0.0001f)
                currentVelocity = Vector3.zero;
        }

        // ── Integrate position ────────────────────────────────────────────
        Vector3 newPos   = localPos + currentVelocity * dt;
        Vector3 clamped  = Clamp(newPos);

        // Kill velocity in any axis that just hit a bound.
        // Without this, stored velocity would snap the anchor away from the
        // wall the moment input stops — the joint would follow and the hand
        // would jerk away from whatever object it was pressing against.
        if (Mathf.Abs(clamped.x - newPos.x) > 0.0001f) currentVelocity.x = 0f;
        if (Mathf.Abs(clamped.y - newPos.y) > 0.0001f) currentVelocity.y = 0f;
        if (Mathf.Abs(clamped.z - newPos.z) > 0.0001f) currentVelocity.z = 0f;

        localPos = clamped;

        Vector3 worldPos = origin.TransformPoint(localPos);

        // ── Hand-to-hand separation ───────────────────────────────────────
        if (handCollider != null)
        {
            foreach (var other in otherTargets)
            {
                if (other == null || other.handCollider == null) continue;

                if (Physics.ComputePenetration(
                        handCollider,       worldPos,                transform.rotation,
                        other.handCollider, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float depth))
                {
                    worldPos += dir * Mathf.Min(depth, maxPushDist);
                    localPos  = origin.InverseTransformPoint(worldPos);
                }
            }
        }

        rb.MovePosition(worldPos);
    }

    Vector3 Clamp(Vector3 l)
    {
        l.x = Mathf.Clamp(l.x, -maxLeft,    maxRight);
        l.y = Mathf.Clamp(l.y, -maxDown,     maxUp);
        l.z = Mathf.Clamp(l.z, -maxBackward, maxForward);
        return l;
    }
}
