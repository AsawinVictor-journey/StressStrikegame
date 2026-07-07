using UnityEngine;

namespace RageRoom
{

/// <summary>
/// Kinematic anchor — the target that PhysicsHandController's ConfigurableJoint chases.
///
/// ── Why this layer exists ─────────────────────────────────────────────────
///   The joint is physically correct. What was wrong was the target it chased:
///   a mathematically perfect point with no momentum, no history, no physical
///   character. A cursor, not an arm.
///
///   This script makes the target itself behave like an inertial object, but
///   it does NOT track real hand position — it has no way to, since an IMU
///   cannot measure position. It only ever integrates a motion-intent signal
///   (acceleration) into a velocity, then walks a virtual anchor around a
///   bounded workspace with that velocity. The anchor's position is a
///   simulation of "where the hand-shaped object currently is inside its box,"
///   never a reconstruction of "where the real hand is in the room."
///
/// ── Signal chain ─────────────────────────────────────────────────────────
///   Input provider (keyboard / BNO055)
///     → acceleration = GetAcceleration(), m/s², local axes — left/right and
///       up/down sustained movement only; NO forward/back. Punching is the
///       only thing that ever moves the hand along that axis.
///   IMU processing (ImuVelocityIntegrator, a plain class owned below)
///     → deadzone → smoothing → velocity += accel × dt → × dampingFactor
///       → clamp to maxSpeed
///   Movement / workspace layer (this script)
///     → localPos += velocity × dt, plus an optional recovery spring pulling
///       toward the workspace center when input is weak
///     → clamped to the workspace box; any axis that clamps has its velocity
///       zeroed so stored momentum can't yank the anchor off a wall the
///       instant input stops
///     → PunchController drives two position-based overrides of the above,
///       BeginPunch() (guaranteed-distance forward lunge) and BeginRetract()
///       (guaranteed return), both on their own timers rather than riding on
///       velocity/damping — since punching is the sole source of Z motion,
///       it can't be left to chance the way a free-roaming velocity kick
///       could when the player could also just walk into range.
///   ConfigurableJoint (PhysicsHandController, unchanged)
///     → physical hand follows this anchor via spring-damper inside PhysX
///   RageRoomCameraRotation (unchanged)
///     → reads LocalPosition against the bounds to rotate the camera when
///       the anchor is pinned at an edge and input keeps pushing past it
///
/// ── Tuning guide ─────────────────────────────────────────────────────────
///   velocity.dampingFactor — per-step velocity multiplier (on the integrator)
///                            0.85-0.95: lower = snappier stop, higher = looser coast.
///   velocity.maxSpeed      — top speed of the anchor in m/s (on the integrator)
///   recoverySpringStrength — 0 disables. Higher pulls the anchor back toward
///                            the workspace center more aggressively once
///                            input drops out.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HandTarget : MonoBehaviour
{
    [Header("References")]
    public Transform         origin;
    public HandInputProvider input;

    [Header("IMU Processing (acceleration → velocity)")]
    public ImuVelocityIntegrator velocity = new ImuVelocityIntegrator();

    [Header("Movement Bounds (local to origin)")]
    public float maxForward  = 0.8f;
    public float maxBackward = 0.8f;
    public float maxUp       = 0.8f;
    public float maxDown     = 0.8f;
    public float maxLeft     = 0.8f;
    public float maxRight    = 0.8f;

    [Header("Recovery")]
    [Tooltip("Optional spring pulling the anchor back toward the workspace " +
             "center (local origin) as an extra acceleration term every step. " +
             "0 disables it entirely — pure damping-coast-to-a-stop instead.")]
    public float recoverySpringStrength = 0f;

    [Header("Hand Separation")]
    public float maxPushDist = 0.15f;

    [Header("Punch Extend")]
    [Tooltip("How far forward (local +Z, m) EVERY punch lunges, regardless of " +
             "charge — reach is constant. Position-driven, not velocity/" +
             "damping-driven — since punching is the ONLY source of Z motion " +
             "now that forward/back movement is disabled, it needs guaranteed " +
             "reach regardless of mass/spring/damping tuning.")]
    public float punchDistance = 0.5f;

    [Tooltip("Fist travel speed (m/s) for an uncharged/tap punch (strength ≈ 0).")]
    public float minPunchSpeed = 3f;

    [Tooltip("Fist travel speed (m/s) for a fully-charged punch (strength = 1). " +
             "Charge scales speed, not distance — every punch covers the same " +
             "Punch Distance, a charged one just gets there faster, which also " +
             "means it lands with more impact velocity (DestructibleObject / " +
             "ImpactReaction already scale damage/force off that automatically).")]
    public float punchSpeed = 6.25f;

    [Header("Punch Retract")]
    [Tooltip("How long the snap-back to the pre-punch position takes (s) once " +
             "PunchController reports a connected hit (or the punch times out " +
             "without landing one). Position-driven, not velocity-driven — a " +
             "punch should retract on a predictable timer, not coast back at " +
             "the mercy of damping.")]
    public float retractDuration = 0.12f;
    // Read by RageRoomCameraRotation (forwarded through PhysicsHandController).
    public Vector3 LocalPosition => localPos;

    [HideInInspector] public Collider handCollider;

    Vector3      localPos;
    HandTarget[] otherTargets;
    Rigidbody    rb;

    bool    retracting;
    Vector3 retractFrom;
    Vector3 retractTo;
    float   retractElapsed;

    bool    extending;
    Vector3 extendFrom;
    Vector3 extendTo;
    float   extendElapsed;
    float   extendDuration;

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

        if (retracting)
        {
            // Position-driven snap-back, not physics. A landed (or missed)
            // punch needs to retract on a predictable timer regardless of how
            // much velocity/damping happen to be left over — input is
            // ignored for the duration so nothing fights the retraction.
            retractElapsed += dt;
            float t = Mathf.Clamp01(retractElapsed / retractDuration);
            localPos = Clamp(Vector3.Lerp(retractFrom, retractTo, t));

            if (t >= 1f)
                retracting = false;
        }
        else if (extending)
        {
            // Same reasoning as retract, mirrored: guaranteed reach on a fixed
            // timer, not a velocity kick that's at the mercy of damping and
            // may not cover any real distance in 0.1s.
            extendElapsed += dt;
            float t = extendDuration > 0f ? Mathf.Clamp01(extendElapsed / extendDuration) : 1f;
            localPos = Clamp(Vector3.Lerp(extendFrom, extendTo, t));

            if (t >= 1f)
                extending = false; // holds at full extension until BeginRetract is called
        }
        else
        {
            // ── IMU processing: acceleration → damped velocity ────────────
            // GetAcceleration() is a motion-intent signal, not a position —
            // sustained movement and a punch spike both arrive through this
            // same channel. The integrator handles deadzone, smoothing,
            // damping, and clamping; this script only adds the optional
            // recovery spring on top before integrating.
            Vector3 accel = input.GetAcceleration();

            if (recoverySpringStrength > 0f)
                accel += (Vector3.zero - localPos) * recoverySpringStrength;

            Vector3 currentVelocity = velocity.Step(accel, dt);

            // ── Integrate position ─────────────────────────────────────────
            Vector3 newPos  = localPos + currentVelocity * dt;
            Vector3 clamped = Clamp(newPos);

            // Kill velocity in any axis that just hit a bound.
            // Without this, stored velocity would snap the anchor away from the
            // wall the moment input stops — the joint would follow and the hand
            // would jerk away from whatever object it was pressing against.
            // This is also the signal RageRoomCameraRotation rides on: the anchor
            // stays pinned at the boundary while input keeps pushing, which is
            // what lets it read "still trying to move past the edge" from position.
            if (Mathf.Abs(clamped.x - newPos.x) > 0.0001f) currentVelocity.x = 0f;
            if (Mathf.Abs(clamped.y - newPos.y) > 0.0001f) currentVelocity.y = 0f;
            if (Mathf.Abs(clamped.z - newPos.z) > 0.0001f) currentVelocity.z = 0f;
            velocity.Velocity = currentVelocity;

            localPos = clamped;
        }

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

    /// <summary>
    /// Instantaneous velocity kick in local axes. Goes straight into the same
    /// integrator as steady input, so it decays via the normal damping — no
    /// separate force system needed. General-purpose (e.g. external knockback).
    /// </summary>
    public void AddImpulse(Vector3 localVelocityKick) => velocity.AddImpulse(localVelocityKick);

    /// <summary>
    /// Called by PunchController on a punch event. Lunges the anchor forward
    /// by the same punchDistance every time — charge strength scales speed
    /// (minPunchSpeed..punchSpeed), not reach, so a fully-charged punch
    /// covers identical ground faster than a tap, landing with more impact
    /// velocity. Holds at full extension until BeginRetract is called (on a
    /// landed hit, or on timeout if the hitbox window closes without one).
    /// </summary>
    public void BeginPunch(float strength)
    {   
        float speed = Mathf.Lerp(minPunchSpeed, punchSpeed, strength);

        extendFrom     = localPos;
        extendTo       = Clamp(localPos + Vector3.forward * punchDistance);
        extendDuration = speed > 0.01f ? punchDistance / speed : 0f;
        extendElapsed  = 0f;
        extending      = true;
        velocity.Velocity = Vector3.zero;
        Debug.Log($"Punch distance = {punchDistance}");
        Debug.Log($"From = {localPos}");
        Debug.Log($"To = {extendTo}");
    }

    /// <summary>
    /// Called by PunchController when the hitbox lands a hit, or when the
    /// punch's hitbox window times out without one. Snaps the anchor back to
    /// targetLocalPos (the position captured right before the punch) over
    /// retractDuration, overriding input for that window. Also zeroes the
    /// integrator so leftover velocity doesn't resume carrying the hand
    /// forward once the retract ends.
    /// </summary>
    public void BeginRetract(Vector3 targetLocalPos)
    {
        Debug.Log($"[Punch] {name}: BeginRetract from {localPos} to {targetLocalPos}", this);
        extending      = false;
        retractFrom    = localPos;
        retractTo      = targetLocalPos;
        retractElapsed = 0f;
        retracting     = true;
        velocity.Velocity = Vector3.zero;
    }

    Vector3 Clamp(Vector3 l)
    {
        l.x = Mathf.Clamp(l.x, -maxLeft,    maxRight);
        l.y = Mathf.Clamp(l.y, -maxDown,     maxUp);
        l.z = Mathf.Clamp(l.z, -maxBackward, maxForward);
        return l;
    }
}
}
