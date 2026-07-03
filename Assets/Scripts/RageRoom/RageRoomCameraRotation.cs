using UnityEngine;

public class RageRoomCameraRotation : MonoBehaviour
{
    [Header("References")]
    // Point these at the hand GameObjects (LeftHand / RightHand) —
    // the same objects that were wired up before the refactor.
    public PhysicsHandController leftHand;
    public PhysicsHandController rightHand;

    [Header("Edge Zone")]
    [Range(0f, 1f)]
    public float edgeFraction = 0.45f;

    [Header("Rotation")]
    public float maxRotateSpeed = 120f;
    [Range(1f, 4f)]
    public float speedCurve  = 2f;
    public float accelSmooth = 10f;
    public float decelSmooth = 22f;

    float currentSpeed;

    void Update()
    {
        float raw    = Mathf.Clamp(EvaluateHand(leftHand) + EvaluateHand(rightHand), -1f, 1f);
        float target = raw * maxRotateSpeed;
        float s      = (Mathf.Abs(target) < Mathf.Abs(currentSpeed)) ? decelSmooth : accelSmooth;
        currentSpeed = Mathf.Lerp(currentSpeed, target, Time.deltaTime * s);
        transform.Rotate(0f, currentSpeed * Time.deltaTime, 0f, Space.World);
    }

    float EvaluateHand(PhysicsHandController hand)
    {
        if (hand == null) return 0f;

        // TargetLocalPosition is forwarded from HandTarget — it is where the player
        // INTENDS the hand to be, not where physics placed it.  This means camera
        // rotation responds to player input, not to collision knockback.
        float x          =  hand.TargetLocalPosition.x;
        float rightStart =  hand.MaxRight * (1f - edgeFraction);
        float leftStart  = -hand.MaxLeft  * (1f - edgeFraction);

        if (x > rightStart)
        {
            float t = Mathf.InverseLerp(rightStart, hand.MaxRight, x);
            return Mathf.Pow(t, speedCurve);
        }
        if (x < leftStart)
        {
            float t = Mathf.InverseLerp(leftStart, -hand.MaxLeft, x);
            return -Mathf.Pow(t, speedCurve);
        }

        return 0f;
    }
}
