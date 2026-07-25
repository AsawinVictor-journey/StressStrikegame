using UnityEngine;

/// <summary>
/// Flick-to-turn camera control. Watches how FAST the hand moves and snaps the
/// camera a fixed number of degrees when it crosses a speed threshold; the
/// signal must slow back down before another flick fires, so one flick = one
/// snap. Slow, deliberate movement never turns the camera.
///
/// Two trigger sources (see TriggerSource):
///   GloveTurnSpeed  — how fast you TURN the hand left/right (glove yaw rate).
///                     This is what responds to physically moving the glove.
///   HandSlideSpeed  — how fast the in-game hand slides sideways (local X),
///                     which is driven by the movement keys (A/D / arrows).
///
/// Uses velocity, not position, so it's immune to the small orientation jitter
/// that made 1:1 hand rotation shiver — only a deliberate fast motion crosses
/// the threshold.
/// </summary>
public class RageRoomCameraRotation : MonoBehaviour
{
    public enum TriggerSource { GloveTurnSpeed, HandSlideSpeed }

    [Header("References")]
    // Point these at the hand GameObjects (LeftHand / RightHand).
    public PhysicsHandController leftHand;
    public PhysicsHandController rightHand;

    [Header("Trigger")]
    [Tooltip("GloveTurnSpeed: fires on how fast you turn the hand (glove yaw). " +
             "HandSlideSpeed: fires on the in-game hand sliding sideways (A/D keys).")]
    public TriggerSource triggerSource = TriggerSource.GloveTurnSpeed;

    [Tooltip("GloveTurnSpeed mode: hand turn speed (deg/sec) needed to fire. " +
             "Sensor jitter is well under ~60; a deliberate flick is several hundred.")]
    public float turnSpeedThreshold = 150f;

    [Tooltip("HandSlideSpeed mode: sideways hand speed (local X units/sec) to fire.")]
    public float slideSpeedThreshold = 8f;

    [Tooltip("Signal must fall below this fraction of the threshold before another " +
             "flick can fire (hysteresis).")]
    [Range(0f, 0.9f)]
    public float rearmFraction = 0.4f;

    [Tooltip("Minimum time between flicks (s).")]
    public float cooldown = 0.3f;

    [Tooltip("Suppress camera flicks for this long (s) while/after a hand is " +
             "punching, so a slightly-sideways punch doesn't also spin the " +
             "camera. A punch takes priority over a turn.")]
    public float punchSuppressTime = 0.35f;

    [Header("Snap")]
    [Tooltip("Degrees the camera turns per flick.")]
    public float stepDegrees = 45f;

    [Tooltip("How long each snap takes (s). 0 = instant.")]
    public float turnDuration = 0.2f;

    [Tooltip("Flip if a left turn/slide rotates the camera the wrong way.")]
    public bool invertDirection = false;

    float prevLeft, prevRight;
    bool  hasLeft, hasRight;
    bool  armed = true;
    float cooldownTimer;
    float pending; // remaining signed degrees still to rotate this snap
    float punchSuppressTimer;

    static bool IsHandPunching(PhysicsHandController hand) =>
        hand != null && hand.target != null && hand.target.IsPunching;

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (cooldownTimer > 0f) cooldownTimer -= dt;

        // A punch takes priority: while a hand is punching (and briefly after),
        // suppress flick detection so an imperfect, slightly-sideways punch
        // doesn't also spin the camera. Signal() still runs below so the yaw
        // baseline keeps tracking through the punch — no spurious flick fires
        // the instant suppression ends.
        if (IsHandPunching(leftHand) || IsHandPunching(rightHand))
            punchSuppressTimer = punchSuppressTime;
        else if (punchSuppressTimer > 0f)
            punchSuppressTimer -= dt;
        bool suppressed = punchSuppressTimer > 0f;

        float vLeft  = Signal(leftHand,  ref prevLeft,  ref hasLeft,  dt);
        float vRight = Signal(rightHand, ref prevRight, ref hasRight, dt);

        // Whichever hand is moving fastest drives the flick.
        float v     = Mathf.Abs(vLeft) >= Mathf.Abs(vRight) ? vLeft : vRight;
        float speed = Mathf.Abs(v);

        float threshold = triggerSource == TriggerSource.GloveTurnSpeed
            ? turnSpeedThreshold
            : slideSpeedThreshold;

        if (!suppressed && armed && cooldownTimer <= 0f && speed > threshold)
        {
            float dir = Mathf.Sign(v) * (invertDirection ? -1f : 1f);
            pending      += dir * stepDegrees;
            armed         = false;
            cooldownTimer = cooldown;
        }
        else if (!armed && speed < threshold * rearmFraction)
        {
            armed = true;
        }

        // Smoothly consume the pending snap.
        if (Mathf.Abs(pending) > 0.001f)
        {
            float maxStep = turnDuration > 0f ? (stepDegrees / turnDuration) * dt : Mathf.Abs(pending);
            float step    = Mathf.Sign(pending) * Mathf.Min(Mathf.Abs(pending), maxStep);
            transform.Rotate(0f, step, 0f, Space.World);
            pending -= step;
        }
    }

    /// <summary>
    /// Per-hand speed signal in the current mode. Returns 0 (and re-seeds) on the
    /// first valid frame or whenever the source is unavailable, so an init jump or
    /// a glove (dis)connect can never be mistaken for a fast flick.
    /// </summary>
    float Signal(PhysicsHandController hand, ref float prev, ref bool has, float dt)
    {
        if (hand == null) { has = false; return 0f; }

        if (triggerSource == TriggerSource.GloveTurnSpeed)
        {
            HandInputProvider input = hand.target != null ? hand.target.input : null;
            if (input == null || !input.ProvidesOrientation) { has = false; return 0f; }

            float yaw = input.GetOrientation().eulerAngles.y;
            if (!has) { prev = yaw; has = true; return 0f; }

            float dYaw = Mathf.DeltaAngle(prev, yaw); // wraps 360 correctly
            prev = yaw;
            return dYaw / dt; // deg/sec
        }
        else
        {
            float x = hand.TargetLocalPosition.x;
            if (!has) { prev = x; has = true; return 0f; }

            float v = (x - prev) / dt;
            prev = x;
            return v; // local units/sec
        }
    }
}
