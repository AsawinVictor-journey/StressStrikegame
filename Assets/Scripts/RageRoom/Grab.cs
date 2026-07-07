using UnityEngine;

public class GrabHand : MonoBehaviour
{
    public Transform holdPoint;
    public Rigidbody handRigidbody;

    [Header("Grab Joint — Position")]
    [Tooltip("Spring pulling the grabbed object toward holdPoint (N/m). " +
             "Higher = faster pull-in. Light objects snap instantly; heavy objects pull slowly.")]
    public float grabPositionSpring = 4000f;

    [Tooltip("Damping on the position drive (N·s/m). " +
             "Prevents oscillation as object converges to holdPoint.")]
    public float grabPositionDamper = 300f;

    [Tooltip("Maximum force the grab joint can apply (N). " +
             "Matches hand maximumForce so constraint budgets stay consistent. " +
             "Heavy objects resist grab when their inertia exceeds this per step.")]
    public float grabMaxForce = 300f;

    [Header("Grab Joint — Rotation")]
    [Tooltip("Spring driving grabbed object to match hand orientation (N·m/rad).")]
    public float grabRotationSpring = 400f;

    [Tooltip("Damping on the rotation drive (N·m·s/rad).")]
    public float grabRotationDamper = 60f;

    GameObject        nearby;
    Rigidbody         grabbed;
    ConfigurableJoint grabJoint;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (grabbed == null) Grab();
            else                 Release();
        }
    }

    void Grab()
    {
        if (nearby == null) return;

        Rigidbody rb = nearby.GetComponent<Rigidbody>();
        if (rb == null) return;

        grabbed = rb;

        Rigidbody anchor = handRigidbody != null
            ? handRigidbody
            : GetComponentInParent<Rigidbody>();

        // ConfigurableJoint on the grabbed object, connected to the hand Rigidbody.
        // This enters the grab into the same PhysX solver graph as the hand's own
        // ConfigurableJoint. Both constraints — hand-to-anchor and object-to-hand —
        // are resolved simultaneously with all contact constraints.
        // No state (position, velocity) is modified. PhysX converges naturally.
        grabJoint = grabbed.gameObject.AddComponent<ConfigurableJoint>();
        grabJoint.connectedBody = anchor;

        // Object center is driven to holdPoint in the hand's local space.
        grabJoint.autoConfigureConnectedAnchor = false;
        grabJoint.anchor          = Vector3.zero;
        grabJoint.connectedAnchor = anchor.transform.InverseTransformPoint(holdPoint.position);

        // Free motion in all axes — drives own all tracking.
        grabJoint.xMotion        = ConfigurableJointMotion.Free;
        grabJoint.yMotion        = ConfigurableJointMotion.Free;
        grabJoint.zMotion        = ConfigurableJointMotion.Free;
        grabJoint.angularXMotion = ConfigurableJointMotion.Free;
        grabJoint.angularYMotion = ConfigurableJointMotion.Free;
        grabJoint.angularZMotion = ConfigurableJointMotion.Free;

        // Position drive: pulls object toward holdPoint.
        // grabMaxForce is the force budget — heavy objects resist when their
        // inertia requires more than this to accelerate toward the hand.
        var posDrive = new JointDrive
        {
            positionSpring = grabPositionSpring,
            positionDamper = grabPositionDamper,
            maximumForce   = grabMaxForce
        };
        grabJoint.xDrive = posDrive;
        grabJoint.yDrive = posDrive;
        grabJoint.zDrive = posDrive;

        // Rotation drive: object smoothly aligns with hand orientation.
        // targetRotation = identity drives the object to match the connected
        // body's (hand's) axes. Angular inertia of heavy objects naturally
        // slows this rotation, making them feel physically weighted on grab.
        grabJoint.rotationDriveMode = RotationDriveMode.Slerp;
        grabJoint.slerpDrive = new JointDrive
        {
            positionSpring = grabRotationSpring,
            positionDamper = grabRotationDamper,
            maximumForce   = grabMaxForce
        };
        grabJoint.targetPosition = Vector3.zero;
        grabJoint.targetRotation = Quaternion.identity;

        grabJoint.projectionMode = JointProjectionMode.None;
    }

    void Release()
    {
        if (grabbed == null) return;

        Destroy(grabJoint);
        grabJoint = null;

        // No impulse applied. The object retains whatever velocity the grab joint
        // gave it while tracking the moving hand — physically correct throw velocity.
        grabbed = null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody)
            nearby = other.gameObject;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject == nearby)
            nearby = null;
    }
}
