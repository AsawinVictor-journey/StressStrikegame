using UnityEngine;

public class HandRotation : MonoBehaviour
{
    public Transform origin;
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
        accYaw   += Input.GetAxis("Mouse X") * sensitivity;
        accPitch -= Input.GetAxis("Mouse Y") * sensitivity;
        accPitch  = Mathf.Clamp(accPitch, -pitchClamp, pitchClamp);
        accYaw    = Mathf.Clamp(accYaw,   -yawClamp,   yawClamp);

        Quaternion target = origin.rotation * Quaternion.Euler(accPitch, accYaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * smooth);
    }
}