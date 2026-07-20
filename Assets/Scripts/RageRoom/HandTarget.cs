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
///   Input provider (keyboard)
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
///       (guaranteed return to a FIXED home pose), both on their own timers
///       rather than riding on velocity/damping — since punching is the sole
///       source of Z motion, it can't be left to chance the way a free-roaming
///       velocity kick could when the player could also just walk into range.
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
    public float minPunchSpeed = 15f; // Increased for a faster punch

    [Tooltip("Fist travel speed (m/s) for a fully-charged punch (strength = 1). " +
             "Charge scales speed, not distance — every punch covers the same " +
             "Punch Distance, a charged one just gets there faster, which also " +
             "means it lands with more impact velocity (DestructibleObject / " +
             "ImpactReaction already scale damage/force off that automatically).")]
    public float punchSpeed = 25f; // Increased for a faster punch

    [Header("Punch Retract")]
    [Tooltip("How long the snap-back to the home position takes (s) once " +
             "PunchController reports a connected hit (or the punch times out " +
             "without landing one). Position-driven, not velocity-driven — a " +
             "punch should retract on a predictable timer, not coast back at " +
             "the mercy of damping.")]
    public float retractDuration = 0.12f;

    [Header("Punch Home")]
    [Tooltip("On (default): the anchor's authored scene position is captured " +
             "in Start() and becomes the home pose, so home always matches " +
             "however the hand was placed in the scene. Off: Home Local " +
             "Position below is used verbatim, for when you want a rest pose " +
             "that deliberately differs from where the object sits in-editor.")]
    public bool useAuthoredStartAsHome = true;

    [Tooltip("The ONE fixed rest pose, in origin-local space, that every " +
             "retract tweens back to. Deliberately a fixed point rather than " +
             "'wherever the hand happened to be when the punch started': a " +
             "remembered pre-punch position is only as trustworthy as the " +
             "state that produced it, so a punch thrown while a previous one " +
             "was still extended used to memorise an ALREADY-EXTENDED position " +
             "as its rest pose and 'retract' the hand to a spot half a metre " +
             "out in front of the player, permanently. A constant can't drift " +
             "like that — no matter how many punches overlap, how many hitbox " +
             "events go missing, or what the free-roam integrator was doing, " +
             "the hand converges on exactly this point. Overwritten in Start() " +
             "when Use Authored Start As Home is on.")]
    public Vector3 homeLocalPosition = Vector3.zero;

    [Tooltip("Hard ceiling (s) on how long the hand may stay extended before " +
             "it retracts itself, whether or not anyone asked it to. The " +
             "extend hold is open-ended by design — it waits for " +
             "PunchController to call BeginRetract() on a landed hit or a " +
             "closed hitbox window — which means every way that call can fail " +
             "to arrive (unassigned hitbox, a PunchHitbox whose collision " +
             "callback never fires, the controller being disabled mid-punch, " +
             "an exception upstream) leaves the hand stuck out in front of the " +
             "player forever. This timer is the backstop that makes that " +
             "outcome impossible: it lives in this script's own FixedUpdate " +
             "and depends on nothing external, so it still fires when every " +
             "other part of the punch pipeline is broken. Set generously — it " +
             "should never trigger on a healthy punch (extend lerp ~0.02-0.03s " +
             "plus a hitbox window of at most 0.3s), only rescue a stuck one. " +
             "0 or less disables it, which is not recommended.")]
    public float maxPunchDuration = 1f;

    // Read by RageRoomCameraRotation (forwarded through PhysicsHandController).
    public Vector3 LocalPosition => localPos;

    /// <summary>True while a punch is extending or retracting. Used by
    /// RageRoomCameraRotation to suppress camera flicks during a punch, so an
    /// imperfect (slightly sideways) punch doesn't also spin the camera.</summary>
    public bool IsPunching => extending || retracting;

    /// <summary>
    /// How long the current lunge takes (s), i.e. punchDistance / travel speed.
    /// Exposed so PunchController can size its hitbox window against the punch
    /// it is actually gating instead of against an independently-authored
    /// constant. A window shorter than this retracts the hand mid-swing: a tap
    /// travels at minPunchSpeed and so takes the LONGEST time to complete, which
    /// is the opposite of the intuition the two separate inspector values invite.
    /// Zero until the first BeginPunch.
    /// </summary>
    public float ExtendDuration => extendDuration;

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

    // Separate from extendElapsed on purpose. extendElapsed drives the lerp and
    // is only meaningful against extendDuration; this one measures the whole
    // uninterrupted stay in the extending state, including the open-ended hold
    // after the lerp has already finished — which is precisely the window the
    // watchdog exists to bound.
    float   extendHeldTime;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic  = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        localPos = origin.InverseTransformPoint(transform.position);

        // Home is resolved exactly once, before any punch can run, so the rest
        // pose is a compile-time-style constant from the state machine's point
        // of view — nothing that runs later is allowed to write it. Clamped
        // either way: a home outside the workspace box could never be reached,
        // and the retract lerp would appear to stall short of its target
        // forever with no obvious cause.
        homeLocalPosition = Clamp(useAuthoredStartAsHome ? localPos : homeLocalPosition);

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
            localPos = Clamp(Vector3.Lerp(retractFrom, retractTo, Ease(t)));

            if (t >= 1f)
            {
                // Snap to the exact target rather than settling for whatever
                // the last lerp step produced. At t = 1 the lerp already lands
                // on retractTo mathematically, but float error over a variable
                // number of steps can leave a sub-millimetre residue, and this
                // position is the seed for the NEXT punch's extendFrom — small
                // errors here are the kind that compound punch over punch until
                // the rest pose has visibly wandered. Assigning the constant
                // makes the hand's resting position bit-identical every time.
                localPos   = retractTo;
                retracting = false;

                // Handing control back to the free-roam integrator with stale
                // velocity in it would let the tween's leftover momentum keep
                // pushing the hand the instant the retract ends — undoing the
                // return we just guaranteed.
                velocity.Velocity = Vector3.zero;
            }
        }
        else if (extending)
        {
            // Guaranteed reach on a fixed timer, not a velocity kick that's at
            // the mercy of damping. Once the lerp reaches extendTo (t >= 1) it
            // holds there: extending stays true, so t clamps at 1 and localPos
            // stays pinned at extendTo. BeginRetract() is the only place that
            // clears extending — clearing it here on completion was the root
            // cause of the "punch doesn't come back" bug: the state machine
            // fell through to the free-roam branch below while the hitbox
            // window was still open (up to hitboxDuration, well past this
            // lerp's ~20-30ms), so any live acceleration input (a held
            // movement key, or the punch's own charge spike still decaying)
            // kept driving the anchor further forward instead of holding.
            extendElapsed  += dt;
            extendHeldTime += dt;

            float t = extendDuration > 0f ? Mathf.Clamp01(extendElapsed / extendDuration) : 1f;
            localPos = Clamp(Vector3.Lerp(extendFrom, extendTo, Ease(t)));

            // ── Watchdog ─────────────────────────────────────────────────
            // The hold above is open-ended: it ends when, and only when,
            // someone else calls BeginRetract(). That is the correct shape for
            // the feature and the wrong shape for reliability, because it makes
            // "the hand comes back" conditional on an external call that has
            // several plausible ways of never arriving (see maxPunchDuration's
            // tooltip). This check closes that hole from the inside. It is
            // deliberately here rather than in PunchController: a backstop that
            // lives in the same component as the failure it guards against is
            // one that cannot be bypassed by anything going wrong upstream.
            if (maxPunchDuration > 0f && extendHeldTime >= maxPunchDuration)
                BeginRetract();
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
    /// landed hit, or on timeout if the hitbox window closes without one),
    /// with the watchdog as a hard ceiling on that hold.
    ///
    /// Safe to call at any point in the punch cycle: a punch arriving mid-
    /// extend or mid-retract cleanly restarts the cycle from wherever the hand
    /// currently is, and cannot corrupt the rest pose it will return to.
    /// </summary>
    public void BeginPunch(float strength)
    {
        float speed = Mathf.Lerp(minPunchSpeed, punchSpeed, strength);

        // Reach is measured from HOME's forward axis, not from wherever the
        // hand currently is. Measuring from localPos made overlapping punches
        // additive on Z: punch twice in quick succession and the second lunge
        // started from the first one's full extension and reached half a metre
        // beyond it, walking the hand out to its forward bound one punch at a
        // time. Anchoring the destination to a constant makes maximum reach
        // identical for every punch no matter how they overlap. X/Y still come
        // from the live position so the player keeps their lateral aim —
        // punches land where the hand is pointed, they just always travel to
        // the same depth.
        Vector3 destination = new Vector3(
            localPos.x,
            localPos.y,
            homeLocalPosition.z + punchDistance);

        // A punch supersedes whatever the hand was doing. Clearing retracting
        // here matters: without it a punch thrown mid-retract would leave both
        // flags set, and the retract branch (checked first in FixedUpdate)
        // would keep dragging the hand backwards while the punch it was
        // supposed to throw silently never moved.
        retracting     = false;
        extendFrom     = localPos;
        extendTo       = Clamp(destination);
        extendDuration = speed > 0.01f ? punchDistance / speed : 0f;
        extendElapsed  = 0f;
        extendHeldTime = 0f;
        extending      = true;

        // Enter the tween with no stored momentum, so the position-driven lerp
        // is the only thing moving the hand for its duration.
        velocity.Velocity = Vector3.zero;
    }

    /// <summary>
    /// Retract to the fixed home DEPTH, keeping the hand where the player has
    /// aimed it laterally. This is the overload PunchController calls, and the
    /// one the watchdog calls.
    ///
    /// Only Z is forced home, and that asymmetry is the whole point. Z is the
    /// one axis with no sustained player control — GetAcceleration() never
    /// drives it, so punching is the only thing that ever moves the hand along
    /// it. That makes Z the only axis that can ratchet: a retract aimed at a
    /// remembered position re-recorded its target from an already-displaced
    /// hand, so each punch's rest pose crept further out than the last and
    /// nothing existed to pull it back (recoverySpringStrength defaults to 0).
    /// Anchoring Z to a constant breaks that feedback loop permanently.
    ///
    /// X and Y are deliberately left alone. Those axes ARE player-driven and
    /// bounded by Clamp(), so they self-correct and cannot ratchet — and
    /// dragging them home on every punch would throw away the player's aim
    /// mid-fight, forcing them to re-acquire a target after each hit.
    /// </summary>
    public void BeginRetract() =>
        BeginRetract(new Vector3(localPos.x, localPos.y, homeLocalPosition.z));

    /// <summary>
    /// Retract to an explicit target. Kept for callers that genuinely want to
    /// steer the return somewhere other than home; the parameterless overload
    /// above is the right choice for ordinary punch handling. Snaps the anchor
    /// back over retractDuration, overriding input for that window, and zeroes
    /// the integrator so leftover velocity doesn't resume carrying the hand
    /// forward once the retract ends.
    /// </summary>
    public void BeginRetract(Vector3 targetLocalPos)
    {
        extending      = false;
        extendHeldTime = 0f;
        retractFrom    = localPos;
        retractTo      = Clamp(targetLocalPos);
        retractElapsed = 0f;
        retracting     = true;
        velocity.Velocity = Vector3.zero;
    }

    /// <summary>
    /// Smoothstep. Raw Vector3.Lerp on a linear t starts and stops the hand at
    /// full speed, which reads as a mechanical snap in both directions —
    /// especially on the retract, where a hard stop at the rest pose looks like
    /// the hand hit a wall. Easing the ends gives the tween acceleration and
    /// settle without changing its duration, its endpoints, or the guarantee
    /// that t = 1 lands exactly on target. Hand-rolled by design: three float
    /// ops do not justify pulling a tweening package into the project.
    /// </summary>
    static float Ease(float t) => t * t * (3f - 2f * t);

    Vector3 Clamp(Vector3 l)
    {
        l.x = Mathf.Clamp(l.x, -maxLeft,    maxRight);
        l.y = Mathf.Clamp(l.y, -maxDown,     maxUp);
        l.z = Mathf.Clamp(l.z, -maxBackward, maxForward);
        return l;
    }
}
}
