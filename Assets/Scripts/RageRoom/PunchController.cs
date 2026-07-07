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
///   3. The hand retracts back to wherever it was right before the punch —
///      on a landed hit immediately, or on timeout if nothing was hit. A
///      thrown punch always comes back, hit or miss.
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
    public HandTarget    handTarget;

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
    Vector3     prePunchLocalPos;
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

    void Update()
    {
        if (!hitboxPending) return;
        if (Time.time < hitboxDisableAt) return;

        // Timed out without landing a hit — still retract, a thrown punch
        // always comes back whether it connected or not.
        hitbox.enabled = false;
        hitboxPending  = false;

        if (handTarget != null)
            handTarget.BeginRetract(prePunchLocalPos);
    }

    void HandlePunch(float strength)
    {
        if (handTarget != null)
        {
            prePunchLocalPos = handTarget.LocalPosition;
            handTarget.BeginPunch(strength);
        }

        if (hitbox == null)
        {
            Debug.LogWarning($"[Punch] {name}: hitbox field is unassigned!", this);
            return;
        }

        hitbox.enabled  = true;
        hitboxPending   = true;
        hitboxDisableAt = Time.time + hitboxDuration;
    }

    void HandleHit(Collision collision)
    {
        Debug.Log($"[Punch] {name}: hitbox touched {collision.gameObject.name}, hitboxPending={hitboxPending}", this);

        // Window already closed (or no punch in flight) — ignore stray events.
        if (!hitboxPending) return;

        // One retract per punch: close the window the instant it connects
        // instead of waiting for hitboxDuration, then snap back.
        hitbox.enabled = false;
        hitboxPending  = false;

        if (handTarget != null)
            handTarget.BeginRetract(prePunchLocalPos);
    }
}
