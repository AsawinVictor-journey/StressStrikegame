using UnityEngine;

/// <summary>
/// Reacts to PunchDetector's discrete punch event with three effects:
///   1. A guaranteed-reach forward lunge via HandTarget.BeginPunch() — a
///      fixed-distance, fixed-timer extension, not a velocity kick. Punching
///      is the ONLY source of forward/back motion the hand has (sustained
///      movement is left/right and up/down only), so it can't be left to the
///      mercy of damping/mass tuning the way a free-roaming velocity kick
///      could when the player could also just walk into range.
///   2. A dedicated hitbox collider enabled for the punch's duration so
///      DestructibleObject / ImpactReaction / DeformableMesh only register a
///      hit during an actual punch, not incidental brushing contact from the
///      hand's own persistent collider.
///   3. The hand retracts to its FIXED home pose — on a landed hit
///      immediately, or on timeout if nothing was hit. A thrown punch always
///      comes back, hit or miss. The destination is HandTarget's own home
///      constant rather than a position this script captures at punch time:
///      a captured position is only correct if the hand was actually at rest
///      when the punch fired, which is exactly what stops being true when
///      punches overlap. Nothing here needs to remember anything.
///
/// Note that this script is NOT the guarantee that the hand comes back — it
/// is the fast path. Every retract below is conditional on this component
/// being enabled, its hitbox being assigned, and its events firing. HandTarget
/// runs its own maxPunchDuration watchdog for the cases where one of those
/// doesn't hold, so a bug in this file can make a punch retract late, but
/// cannot make it fail to retract.
///
/// Movement and punch are separate modules on purpose: HandTarget has no
/// idea a punch happened beyond receiving BeginPunch/BeginRetract calls
/// through its public API, and PunchDetector has no idea what a "hitbox" is.
///
/// The hitbox window uses a timestamp checked in Update() rather than
/// Invoke/coroutines — zero per-punch allocation, no string-keyed dispatch.
/// </summary>
public class PunchController : MonoBehaviour
{
    [Header("References")]
    public PunchDetector punchDetector;
    public RageRoom.HandTarget handTarget;

    [Tooltip("Collider enabled only while a punch is active. Should be tagged " +
             "'Hand' so DestructibleObject/ImpactReaction/DeformableMesh detect it. " +
             "A PunchHitbox component is required on this collider's GameObject " +
             "so a connected hit can trigger the retract.")]
    public Collider hitbox;

    [Header("Hitbox Window")]
    [Tooltip("Grace held AFTER the lunge finishes before a punch that hasn't " +
             "connected is called a miss and retracted. The total live window " +
             "is the lunge duration PLUS this. Keep it small: the fist is " +
             "motionless at full extension for this entire period, so it can " +
             "only delay the retract — and a late retract is exactly what " +
             "leaves the next punch with no stroke left to throw.")]
    [Range(0f, 0.2f)]
    public float hitboxDuration = 0.05f;

    [Header("Impact VFX")]
    [Tooltip("Particle system spawned at the contact point every time this glove " +
             "lands a hit. Should be a one-shot prefab (looping off, Stop Action = " +
             "Destroy) so instances clean themselves up.")]
    public ParticleSystem impactEffectPrefab;

    [Tooltip("Local Euler offset applied on top of the surface-normal alignment. " +
             "The effect is rotated so its local +Z faces the hit surface's normal; " +
             "if the prefab's own 'forward' (e.g. its Shape module's cone direction) " +
             "doesn't match +Z, tune this until the burst looks right.")]
    public Vector3 impactEffectRotationOffset;

    PunchHitbox hitboxEvents;
    bool        hitboxPending;
    float       hitboxDisableAt;

    void OnEnable()
    {
        if (punchDetector != null) punchDetector.OnPunch += HandlePunch;

        if (hitbox != null)
        {
            hitbox.enabled = false;
            hitboxEvents   = hitbox.GetComponent<PunchHitbox>();
            if (hitboxEvents != null) hitboxEvents.OnHit += HandleHit;
        }
    }

    void OnDisable()
    {
        if (punchDetector != null) punchDetector.OnPunch -= HandlePunch;
        if (hitboxEvents  != null) hitboxEvents.OnHit    -= HandleHit;

        hitboxPending = false;
        if (hitbox != null) hitbox.enabled = false;
    }

    // FixedUpdate, not Update: this timer decides when HandTarget's state machine
    // leaves its extend branch, and that machine runs on the fixed clock. Polling
    // it on the render clock meant that whenever the frame rate dropped, the
    // retract was issued late by up to a whole frame while the hand sat pinned at
    // full extension — so the laggier a session got, the worse the punches felt,
    // and the wider the window for a second punch to overlap the first.
    void FixedUpdate()
    {
        if (!hitboxPending) return;
        if (Time.time < hitboxDisableAt) return;

        // Timed out without landing a hit — still retract, a thrown punch
        // always comes back whether it connected or not.
        CloseWindow();

        if (handTarget != null)
            handTarget.BeginRetract();
    }

    void HandlePunch(float strength)
    {
        // A punch already in flight: close its window first so the hand is never
        // left with an open hitbox belonging to a punch that has been superseded.
        // Without this the older, longer deadline stayed authoritative and kept
        // extending the total time the hand spent out front on every rapid combo.
        if (hitboxPending) CloseWindow();

        // Check the hitbox BEFORE committing to the lunge. Bailing out after
        // BeginPunch used to leave the hand extended with hitboxPending never
        // set, so the timeout path below could never run and only HandTarget's
        // watchdog eventually recovered it — a visible one-second stall.
        if (hitbox == null)
        {
            Debug.LogWarning($"[Punch] {name}: hitbox field is unassigned!", this);
            return;
        }

        if (handTarget != null)
            handTarget.BeginPunch(strength);

        // The window is the lunge plus a short grace, and nothing more. Once
        // the lerp reaches full extension the fist has STOPPED — it cannot land
        // a new contact from a standstill, so every extra millisecond the
        // window stays open is pure cost: the hand is pinned out front, and the
        // next punch (legal again after PunchDetector.cooldown) starts from a
        // position already AT extendTo and therefore travels ~0 m. Holding a
        // flat 0.3 s here is what made rapid punches silently do nothing.
        // Sizing the window off the lunge still guarantees the swing completes,
        // while starting the retract early enough that the following punch has
        // real stroke left to throw.
        float lunge  = handTarget != null ? handTarget.ExtendDuration : 0f;
        float window = lunge + hitboxDuration;

        hitbox.enabled  = true;
        hitboxPending   = true;
        hitboxDisableAt = Time.time + window;
    }

    void CloseWindow()
    {
        hitboxPending = false;
        if (hitbox != null) hitbox.enabled = false;
    }

    // Unity delivers collision callbacks to the GameObject owning the
    // RIGIDBODY, not the one owning the collider. The punch Hitbox is a child
    // collider of this hand's Rigidbody with no Rigidbody of its own, so
    // PunchHitbox.OnCollisionEnter sitting on it was never called — which
    // silently killed retract-on-hit: every punch, including one that connected
    // on its first frame, held full extension for the whole window instead of
    // snapping back. That left the hand out front when the next punch became
    // legal, and a punch thrown from full extension travels nowhere.
    //
    // This component lives on the Rigidbody's GameObject, so the message does
    // arrive here. All that is left is to confirm the contact actually involved
    // the hitbox and not the hand's own persistent collider.
    //
    // PunchHitbox is left in place and still subscribed: it is harmless (a
    // duplicate HandleHit is a no-op once CloseWindow has cleared
    // hitboxPending) and it remains correct for any setup where the hitbox is
    // given its own Rigidbody.
    void OnCollisionEnter(Collision collision)
    {
        if (!hitboxPending || hitbox == null) return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).thisCollider == hitbox)
            {
                HandleHit(collision);
                return;
            }
        }
    }

    void HandleHit(Collision collision)
    {
        // Window already closed (or no punch in flight) — ignore stray events.
        if (!hitboxPending) return;

        // One retract per punch: close the window the instant it connects
        // instead of waiting out the full duration, then snap back.
        CloseWindow();

        SpawnImpactEffect(collision);

        if (handTarget != null)
            handTarget.BeginRetract();
    }

    /// <summary>
    /// Spawns impactEffectPrefab at the collision's contact point. Public so
    /// DestructibleObject/ImpactReaction can trigger it directly from their own
    /// OnCollisionEnter — the dedicated punch Hitbox's own OnCollisionEnter does
    /// not reliably fire even when its collider is the one that made contact
    /// (confirmed: the receiving object's Collision.collider resolves to the
    /// Hitbox, but PunchHitbox on that same GameObject never receives the
    /// message), so HandleHit()/OnHit can't be trusted as the sole trigger.
    /// </summary>
    public void SpawnImpactEffect(Collision collision)
    {
        if (impactEffectPrefab == null || collision.contactCount == 0) return;

        ContactPoint contact = collision.GetContact(0);
        Quaternion rotation  = Quaternion.LookRotation(contact.normal) * Quaternion.Euler(impactEffectRotationOffset);
        Instantiate(impactEffectPrefab, contact.point, rotation).Play();
    }
}
