using UnityEngine;

public class HandRotation : MonoBehaviour
{
    public Transform origin;

    [Tooltip("Optional. When assigned and ProvidesOrientation is true, " +
             "rotation is driven from GetOrientation() instead of the mouse. " +
             "Leave unassigned to keep today's mouse-driven behavior " +
             "(e.g. for KeyboardHandInput testing).")]
    public HandInputProvider input;

    public float sensitivity = 5f;
    public float smooth     = 15f;
    public float pitchClamp = 90f;
    public float yawClamp   = 90f;

    [Tooltip("Glove only: multiplies how far the hand rotates for a given real " +
             "hand tilt. 1 = 1:1 with the sensor. 1.5 = rotates 50% more, 2 = " +
             "double. Applied to the orientation delta from the recentered pose, " +
             "so recenter (Space) still defines 'straight ahead'. Very high values " +
             "can overshoot past 180 and flip — keep it in ~1..2.5.")]
    public float rotationGain = 1.5f;

    [Header("Glove axis correction (mirrored left hand)")]
    [Tooltip("Invert up/down (pitch). On by default because the left hand's parent " +
             "has a negative X scale, which flips this axis. Turn off if up/down is " +
             "already correct.")]
    public bool invertPitch = true;
    [Tooltip("Invert left/right (yaw). Toggle if turning left rotates the hand right.")]
    public bool invertYaw = false;
    [Tooltip("Invert twist (roll). Toggle if wrist-twist goes the wrong way.")]
    public bool invertRoll = false;

    [Tooltip("Glove only: if the incoming orientation is within this many degrees " +
             "of where the hand already is, don't move at all. Freezes out the " +
             "BNO055's few-degrees of fused-quaternion jitter (the 'shivering') " +
             "while still tracking real hand movement, which easily exceeds it. " +
             "Raise if it still shivers; lower toward 0 if small real motions feel " +
             "ignored. 0 disables the deadzone.")]
    public float holdDeadzoneDegrees = 3f;

    float accPitch, accYaw;

    void Start()
    {
        // Seed accumulators from the hand's current offset relative to origin
        // so it doesn't snap on play
        Quaternion localRot = Quaternion.Inverse(origin.rotation) * transform.rotation;
        Vector3 euler = localRot.eulerAngles;
        accPitch = euler.x > 180f ? euler.x - 360f : euler.x;
        accYaw   = euler.y > 180f ? euler.y - 360f : euler.y;
    }

    void Update()
    {
        // Lock rotation to the player/camera (origin) so fists always face forward
        if (origin != null) transform.rotation = origin.rotation;
        return;
        
        if (input != null && input.ProvidesOrientation)
        {
            // GetOrientation() is a delta from the recentered zero pose. Apply
            // it in WORLD space via origin — identical to the mouse path below —
            // NOT as localRotation. This hand's parent (LeftHand) has a negative
            // X scale (it is a mirrored copy of the right hand); applying the
            // rotation locally makes the parent mirror invert an axis, which is
            // what flipped up/down. World-space application through origin is
            // immune to the parent's mirror, exactly like the mouse rotation.
            Quaternion providerTarget = input.GetOrientation();

            // Amplify the rotation: scale the delta-from-recenter angle by
            // rotationGain so a small real tilt turns the hand further.
            if (rotationGain != 1f)
                providerTarget = Quaternion.SlerpUnclamped(Quaternion.identity, providerTarget, rotationGain);

            // Per-axis inversion to correct for the mirrored (negative-scale)
            // left-hand hierarchy. Done on the delta-from-recenter so recenter
            // still defines "straight ahead".
            if (invertPitch || invertYaw || invertRoll)
            {
                Vector3 e = providerTarget.eulerAngles;
                providerTarget = Quaternion.Euler(
                    invertPitch ? -e.x : e.x,
                    invertYaw   ? -e.y : e.y,
                    invertRoll  ? -e.z : e.z);
            }

            Quaternion worldTarget = origin.rotation * providerTarget;

            // Rotational deadzone: below this angle the target is just sensor
            // jitter, so hold completely still (no shiver). Above it, track with
            // frame-rate-independent smoothing.
            if (Quaternion.Angle(transform.rotation, worldTarget) > holdDeadzoneDegrees)
            {
                float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, worldTarget, t);
            }
            return;
        }

        accYaw   += Input.GetAxis("Mouse X") * sensitivity;
        accPitch -= Input.GetAxis("Mouse Y") * sensitivity;
        accPitch  = Mathf.Clamp(accPitch, -pitchClamp, pitchClamp);
        accYaw    = Mathf.Clamp(accYaw,   -yawClamp,   yawClamp);

        Quaternion target = origin.rotation * Quaternion.Euler(accPitch, accYaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * smooth);
    }
}