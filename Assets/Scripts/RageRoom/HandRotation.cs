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
        if (input != null && input.ProvidesOrientation)
        {
            // Matches VRGloveProcessor's (Boxing) approach: GetOrientation()
            // is already a delta from the recentered zero pose, so it's
            // applied as local rotation directly and left to Unity's normal
            // parent/child hierarchy to compose with origin's world rotation,
            // instead of re-multiplying by origin.rotation by hand.
            Quaternion providerTarget = input.GetOrientation();
            transform.localRotation = Quaternion.Slerp(transform.localRotation, providerTarget, Time.deltaTime * smooth);
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