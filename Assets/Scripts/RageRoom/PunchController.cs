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
    [Tooltip("How long the hitbox stays live if nothing gets hit before the " +
             "punch is called a miss and retracted anyway.")]
    [Range(0.1f, 0.3f)]
    public float hitboxDuration = 0.2f;

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

        // The window must outlast the lunge itself. hitboxDuration alone is
        // authored independently of punchDistance/punchSpeed, so a slow (tap)
        // punch can take longer to extend than the window stays open, and the
        // hand would be told to retract while still travelling outward — the
        // punch visibly stalls and reverses mid-swing. Taking the max of the two
        // keeps hitboxDuration meaningful as a floor for fast punches while
        // guaranteeing the swing always completes.
        float lunge  = handTarget != null ? handTarget.ExtendDuration : 0f;
        float window = Mathf.Max(hitboxDuration, lunge + hitboxDuration * 0.5f);

        hitbox.enabled  = true;
        hitboxPending   = true;
        hitboxDisableAt = Time.time + window;
    }

    void CloseWindow()
    {
        hitboxPending = false;
        if (hitbox != null) hitbox.enabled = false;
    }

    void HandleHit(Collision collision)
    {
        // Window already closed (or no punch in flight) — ignore stray events.
        if (!hitboxPending) return;

        // One retract per punch: close the window the instant it connects
        // instead of waiting out the full duration, then snap back.
        CloseWindow();

        if (handTarget != null)
            handTarget.BeginRetract();
    }
}
