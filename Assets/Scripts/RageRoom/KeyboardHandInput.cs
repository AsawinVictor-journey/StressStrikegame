using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keyboard/mouse IMU simulator, with an optional ESP32Glove (BNO055) layered
/// on top when one is connected. Both sources stay live at the same time:
///
///   Rotation — HandRotation already prefers GetOrientation() whenever
///   ProvidesOrientation is true, and falls back to mouse-look otherwise.
///   ProvidesOrientation here just tracks whether a matching glove is
///   connected, so rotation is glove-driven when a glove is present and
///   mouse-driven when it isn't — no manual switching required.
///
///   Punching — the existing hold-to-charge mouse/keyboard click still
///   works unchanged. The glove's combined force reading is added on top of
///   the same Z-axis acceleration signal, so a real punch from the glove
///   triggers PunchDetector exactly like a mouse click would, and either one
///   (or both at once) can throw a punch.
///
/// Sustained left/right and up/down movement (local X/Y) always comes from
/// the keyboard — the glove hardware has no axes for that (see ESP32Glove /
/// VRGloveProcessor remarks), only fused orientation and a two-axis force
/// reading used here for punches.
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

    [Header("Glove (optional — used automatically when connected)")]
    [Tooltip("On: use the glove's rotation and punch force when one is " +
             "connected (falls back to keyboard/mouse if it isn't). " +
             "Off: always use keyboard/mouse only, even with a glove connected.")]
    public bool useHardwareInput = true;

    [Tooltip("Which glove this instance should bind to when more than one " +
             "ESP32Glove is connected. Matched by connection order. " +
             "Irrelevant with a single glove connected.")]
    public Side gloveSide = Side.Left;

    [Tooltip("Enable only if the physical glove's axes don't match Unity's " +
             "coordinate system.")]
    public bool convertToLeftHanded = true;

    [Tooltip("Lets a key re-zero the glove's \"forward\" orientation. A " +
             "runtime reference-frame offset only, not a sensor calibration.")]
    public bool allowRecenter = true;
    public KeyCode recenterKey = KeyCode.Space;

    [Tooltip("Re-zero orientation automatically the moment a glove is " +
             "acquired, so rotation is correct from the first frame instead " +
             "of requiring the player to press recenterKey first. Turn off " +
             "if you'd rather always recenter manually.")]
    public bool autoRecenterOnConnect = true;

    /// <summary>True once a matching physical glove is connected.</summary>
    public bool IsConnected => device != null;

    public override bool ProvidesOrientation => useHardwareInput && IsConnected;

    float punchTimer;
    float currentSpikeAccel;
    float pressStartTime = -1f;

    ESP32Glove device;
    Quaternion zeroOffset = Quaternion.identity;
    Quaternion lastValidOrientation = Quaternion.identity;

    void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        TryAcquireDevice();
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        device = null;
    }

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

        if (allowRecenter && Input.GetKeyDown(recenterKey))
            RecenterOrientation();
    }

    void FixedUpdate()
    {
        if (punchTimer > 0f)
            punchTimer = Mathf.Max(0f, punchTimer - Time.fixedDeltaTime);
    }

    void OnDeviceChange(InputDevice changedDevice, InputDeviceChange change)
    {
        var glove = changedDevice as ESP32Glove;
        if (glove == null) return;

        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Reconnected:
                if (device == null) TryAcquireDevice();
                break;

            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                if (ReferenceEquals(glove, device)) device = null;
                break;
        }
    }

    void TryAcquireDevice()
    {
        int matchIndex = (int)gloveSide;
        int seen = 0;

        foreach (var d in InputSystem.devices)
        {
            var glove = d as ESP32Glove;
            if (glove == null) continue;

            if (seen == matchIndex)
            {
                device = glove;
                if (autoRecenterOnConnect) RecenterOrientation();
                return;
            }
            seen++;
        }
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

        // Mouse/keyboard hold-to-charge punch spike.
        if (punchTimer > 0f)
            accel += Vector3.forward * currentSpikeAccel;

        // Glove punch: raw combined force magnitude, added on top so either
        // source can trigger PunchDetector independently (or both at once).
        if (useHardwareInput && device != null)
        {
            // The InputSystem SHRT control returns a normalized [-1, 1] value.
            // Multiply by 327.67f to restore the raw 16-bit range so it reaches 
            // the 60f - 150f acceleration thresholds expected by PunchDetector.
            float forceY = device.forceY.ReadValue() * 327.67f;
            float forceZ = device.forceZ.ReadValue() * 327.67f;

            if (!float.IsNaN(forceY) && !float.IsNaN(forceZ))
                accel += Vector3.forward * new Vector2(forceY, forceZ).magnitude;
        }

        return accel;
    }

    public override Quaternion GetOrientation()
    {
        if (!TryReadRawOrientation(out Quaternion raw)) return lastValidOrientation;

        lastValidOrientation = Quaternion.Inverse(zeroOffset) * raw;
        return lastValidOrientation;
    }

    /// <summary>
    /// Re-zeroes "forward" to whatever orientation the glove is currently
    /// reporting. A runtime reference-frame offset only — does not touch the
    /// sensor's own onboard fusion calibration.
    /// </summary>
    public void RecenterOrientation()
    {
        if (TryReadRawOrientation(out Quaternion raw))
            zeroOffset = raw;
    }

    bool TryReadRawOrientation(out Quaternion raw)
    {
        raw = Quaternion.identity;
        if (device == null) return false;

        float qX = device.x.ReadValue();
        float qY = device.y.ReadValue();
        float qZ = device.z.ReadValue();
        float qW = device.w.ReadValue();

        raw = convertToLeftHanded
            ? new Quaternion(-qY, -qZ, qX, qW)
            : new Quaternion(qX, qY, qZ, qW);

        // A zero (or corrupt) packet would make normalization divide by zero
        // and silently produce NaN, which then corrupts the hand transform
        // permanently. Guard against that here rather than trust the sensor
        // never sends one.
        float sqrMag = raw.x * raw.x + raw.y * raw.y + raw.z * raw.z + raw.w * raw.w;
        if (sqrMag < 0.0001f) return false;

        raw = raw.normalized;
        return true;
    }
}
