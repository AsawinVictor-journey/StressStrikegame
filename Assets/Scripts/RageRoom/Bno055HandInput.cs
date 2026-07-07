using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HandInputProvider backed by a physical ESP32Glove HID device (BNO055 IMU).
/// Reads ESP32Glove exactly as it exposes data today — no changes to the HID
/// layer, byte layout, or sensor calibration. All defensive handling
/// (disconnects, corrupt/zero packets, missing hardware) lives here so
/// KeyboardHandInput and PunchDetector/HandTarget/PunchController stay
/// input-source-agnostic, same as the rest of this pipeline.
///
/// Hardware limitation (cannot be changed — see project constraints): the
/// firmware's HID report only carries a fused orientation quaternion plus two
/// force-derived scalars (forceY, forceZ), not a true 3-axis accelerometer
/// reading. There is no left/right/up/down sustained-motion signal available
/// from this hardware layout, so GetAcceleration() maps the combined force
/// magnitude onto local Z only — the axis PunchDetector/HandTarget already
/// treat as "the punch axis" for every input source — and leaves X/Y at zero.
/// Sustained hand repositioning from the glove itself isn't possible unless
/// the firmware later exposes more axes.
///
/// Left/right disambiguation: without a hardware-side device identity
/// (serial number), two connected gloves can only be told apart by
/// connection order, which is the best option available without touching
/// hardware code. With a single glove connected (today's setup) this is moot.
/// </summary>
public class Bno055HandInput : HandInputProvider
{
    public enum Side { Left, Right }

    [Header("Device Selection")]
    [Tooltip("Which glove this instance should bind to when more than one " +
             "ESP32Glove is connected. Assigned by connection order only — " +
             "see class remarks. Irrelevant with a single glove connected.")]
    public Side side = Side.Left;

    [Header("Orientation")]
    [Tooltip("Enable only if the physical glove's axes don't match Unity's " +
             "coordinate system. Can't be fixed in firmware, so it's a " +
             "runtime toggle instead.")]
    public bool convertToLeftHanded = true;

    [Header("Recenter (software zero-offset only)")]
    [Tooltip("Lets a key re-zero \"forward\" for testing. This is a runtime " +
             "reference-frame offset, not a sensor calibration.")]
    public bool allowKeyboardRecenter = true;
    public Key recenterKey = Key.Space;

    /// <summary>True once a matching physical glove is connected.</summary>
    public bool IsConnected => device != null;

    public override bool ProvidesOrientation => true;

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
        if (allowKeyboardRecenter && Keyboard.current != null &&
            Keyboard.current[recenterKey].wasPressedThisFrame)
        {
            RecenterOrientation();
        }
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
        int matchIndex = (int)side;
        int seen = 0;

        foreach (var d in InputSystem.devices)
        {
            var glove = d as ESP32Glove;
            if (glove == null) continue;

            if (seen == matchIndex)
            {
                device = glove;
                return;
            }
            seen++;
        }
    }

    public override Vector3 GetAcceleration()
    {
        if (device == null) return Vector3.zero;

        float forceY = device.forceY.ReadValue();
        float forceZ = device.forceZ.ReadValue();

        if (float.IsNaN(forceY) || float.IsNaN(forceZ)) return Vector3.zero;

        // See class remarks: no X/Y signal exists from this hardware, so the
        // combined force reading is reported entirely on local Z.
        float forceMagnitude = new Vector2(forceY, forceZ).magnitude;
        return new Vector3(0f, 0f, forceMagnitude);
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
