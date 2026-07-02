using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsHandController : MonoBehaviour
{
    [Header("Target Tracking")]
    public Transform targetTransform;
    
    [Header("Physics Settings")]
    [Range(1f, 50f)]
    public float positionSpring = 25f;
    [HideInInspector]
    public float positionDamper = 0f; // Not used anymore
    [Range(1f, 50f)]
    public float rotationSpring = 25f;
    [HideInInspector]
    public float rotationDamper = 0f; // Not used anymore

    private Rigidbody rb;
    private Quaternion rotationOffset;

    void OnValidate()
    {
        positionSpring = Mathf.Clamp(positionSpring, 1f, 50f);
        rotationSpring = Mathf.Clamp(rotationSpring, 1f, 50f);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (targetTransform != null) 
        {
            // Save the initial rotation difference so the glove keeps its original angled orientation!
            rotationOffset = Quaternion.Inverse(targetTransform.rotation) * transform.rotation;
        }
        rb.useGravity = false; // We want the hands to float to their target
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Important for fast punches
        
        // Prevent hand from falling asleep
        rb.sleepThreshold = 0f;
    }

    void FixedUpdate()
    {
        if (targetTransform == null) return;

        // Position Tracking using direct velocity (mathematically cannot explode)
        Vector3 positionDifference = targetTransform.position - rb.position;
        rb.velocity = positionDifference * positionSpring;

        // Rotation Tracking using direct angular velocity (with original offset!)
        Quaternion targetRot = targetTransform.rotation * rotationOffset;
        Quaternion rotationDifference = targetRot * Quaternion.Inverse(rb.rotation);
        
        // Convert quaternion difference to angle-axis
        rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        
        if (angle != 0f && !float.IsInfinity(axis.x) && !float.IsNaN(axis.x)) 
        {
            Vector3 angularVelocity = axis * (angle * Mathf.Deg2Rad * rotationSpring);
            if (!float.IsNaN(angularVelocity.x))
            {
                rb.angularVelocity = angularVelocity;
            }
        }
    }
}
