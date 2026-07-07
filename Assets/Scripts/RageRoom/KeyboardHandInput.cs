using UnityEngine;

/// <summary>
/// Simulated IMU signal from keyboard/mouse input: a small sustained
/// acceleration while a movement key is held (like tilting/pushing a real
/// sensor), plus a punch spike layered on top when the punch button is
/// released.
///
/// Sustained movement only ever covers left/right and up/down (local X/Y).
/// Forward/back (local Z) has no held-key control at all — the fist can't be
/// steered toward or away from a target by hand, it can only get there by
/// throwing a punch. That's the one and only source of Z-axis motion.
///
/// This still reports ONLY acceleration — never position. HandTarget is the
/// one place that turns this into a bounded, damped position; nothing here
/// tracks where the hand "really" is.
///
/// Punch strength is hold-to-charge: holding the punch button down winds
/// the fist back (further hold = more "cocked"), and releasing throws the
/// punch with a spike magnitude between minPunchAccel and maxPunchAccel
/// based on how long it was held, capped at chargeMaxTime. A quick tap
/// still throws a punch — just a weak one.
/// </summary>
public class KeyboardHandInput : HandInputProvider
{
    public enum Side { Left, Right }
    public Side side;

    [Header("Sustained Movement")]
    [Tooltip("Acceleration magnitude (m/s²) while a left/right or up/down " +
             "movement key is held. Forward/back has no held-key control — " +
             "see class summary.")]
    public float moveAccel = 20f;

    [Header("Punch Input")]
    [Tooltip("Button that triggers this hand's punch. Assign Mouse0 (left " +
             "click) to the left hand and Mouse1 (right click) to the right " +
             "hand so each hand punches independently.")]
    public KeyCode punchKey = KeyCode.Mouse0;

    [Header("Punch Charge")]
    [Tooltip("Spike magnitude (m/s²) thrown on a quick tap with no charge-up. " +
             "Must sit above PunchDetector's threshold so even a light tap " +
             "reliably registers as a punch.")]
    public float minPunchAccel = 70f;

    [Tooltip("Spike magnitude (m/s²) thrown after holding for chargeMaxTime. " +
             "Should match PunchDetector's fullStrengthAccel so a full charge " +
             "reads as exactly strength 1.0.")]
    public float maxPunchAccel = 150f;

    [Tooltip("How long you can hold the button before charge maxes out (s). " +
             "Holding longer has no further effect.")]
    public float chargeMaxTime = 0.6f;

    [Tooltip("How long the spike holds before cutting off (s). Real punch " +
             "accelerometer transients are roughly 0.1–0.2s.")]
    [Range(0.1f, 0.2f)]
    public float punchSpikeDuration = 0.15f;

    float punchTimer;
    float currentSpikeAccel;
    float pressStartTime = -1f;

    void Update()
    {
        // GetKeyDown/GetKeyUp must be polled once per rendered frame —
        // FixedUpdate runs on its own cadence and can miss the single frame
        // the button changed state, silently dropping presses/releases.
        if (Input.GetKeyDown(punchKey))
        {
            pressStartTime = Time.time;
        }

        if (Input.GetKeyUp(punchKey) && pressStartTime >= 0f)
        {
            float heldTime = Time.time - pressStartTime;
            float chargeT  = Mathf.Clamp01(heldTime / chargeMaxTime);

            currentSpikeAccel = Mathf.Lerp(minPunchAccel, maxPunchAccel, chargeT);
            punchTimer        = punchSpikeDuration;
            pressStartTime    = -1f;
        }
    }

    void FixedUpdate()
    {
        if (punchTimer > 0f)
            punchTimer = Mathf.Max(0f, punchTimer - Time.fixedDeltaTime);
    }

    public override Vector3 GetAcceleration()
    {
        KeyCode right, left, up, down;

        if (side == Side.Left)
        {
            right = KeyCode.D; left = KeyCode.A;
            up    = KeyCode.E; down = KeyCode.Q;
        }
        else
        {
            right = KeyCode.RightArrow; left = KeyCode.LeftArrow;
            up    = KeyCode.PageUp;     down = KeyCode.PageDown;
        }

        // No sustained forward/back (local Z) input — that axis is reserved
        // for punching. Left/right and up/down repositioning is still free.
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(right)) dir += Vector3.right;
        if (Input.GetKey(left))  dir -= Vector3.right;
        if (Input.GetKey(up))    dir += Vector3.up;
        if (Input.GetKey(down))  dir -= Vector3.up;

        Vector3 accel = dir.normalized * moveAccel;

        // The only forward/back (Z) motion the hand ever gets is the punch
        // spike — thrown, not steered.
        if (punchTimer > 0f)
            accel += Vector3.forward * currentSpikeAccel;

        return accel;
    }
}
