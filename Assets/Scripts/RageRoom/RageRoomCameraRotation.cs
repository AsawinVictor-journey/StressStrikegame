using UnityEngine;

public class RageRoomCameraRotation : MonoBehaviour
{
    [Header("References")]
    public HandPosition leftHand;
    public HandPosition rightHand;

    [Header("Edge Zone")]
    [Range(0f, 1f)]
    public float edgeFraction = 0.45f;  // outer 45% of hand range triggers rotation

    [Header("Rotation")]
    public float maxRotateSpeed = 120f;
    [Range(1f, 4f)]
    public float speedCurve  = 2f;   // 1 = linear, 2 = quadratic — higher gives more precision near the edge boundary
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

    float EvaluateHand(HandPosition hand)
    {
        if (hand == null || hand.origin == null) return 0f;

        float x          = hand.origin.InverseTransformPoint(hand.transform.position).x;
        float rightStart =  hand.maxRight * (1f - edgeFraction);
        float leftStart  = -hand.maxLeft  * (1f - edgeFraction);

        if (x > rightStart)
        {
            float t = Mathf.InverseLerp(rightStart, hand.maxRight, x);
            return Mathf.Pow(t, speedCurve);
        }
        if (x < leftStart)
        {
            float t = Mathf.InverseLerp(leftStart, -hand.maxLeft, x);
            return -Mathf.Pow(t, speedCurve);
        }

        return 0f;
    }
}
