using UnityEngine;

/// <summary>
/// Connects the physical hand to the HandTarget kinematic anchor via a
/// ConfigurableJoint. This script has no FixedUpdate — the joint runs entirely
/// inside PhysX's constraint solver alongside contact constraints.
///
/// ── Why every previous approach was architecturally wrong ────────────────
///
///   VelocityChange, capped AddForce, and the braking/driving split all share
///   one structural flaw: they run in FixedUpdate, which executes BEFORE
///   PhysX's constraint solver. The order every physics step is:
///
///     1. FixedUpdate scripts run  → controller applies corrective force/velocity
///     2. PhysX integrates forces  → rigidbodies move
///     3. PhysX detects contacts
///     4. Constraint solver runs   → resolves contacts and joints simultaneously
///
///   The controller always acts in step 1, then the solver reacts in step 4.
///   The solver balances its contact forces against what the controller already
///   decided. They run sequentially, one frame apart. This is the root cause of
///   the "fighting physics" feel and the reason object mass appeared irrelevant —
///   the controller could always undo the solver's contact impulse next frame.
///
/// ── Why ConfigurableJoint is the correct architecture ────────────────────
///
///   A ConfigurableJoint is not a script — it is a PhysX constraint. It runs
///   entirely inside step 4, alongside all contact constraints. The solver sees
///   both the joint drive force and the contact force at the same time, against
///   the same velocity state, in the same iteration. They are balanced together.
///
///   When the hand presses against a heavy object:
///     contact resistance  >  joint maximumForce
///     → solver gives more force to the contact, less to the joint
///     → hand stalls naturally, with no script intervention
///
///   This is not a heuristic. It is how the physics engine works internally.
///   No FixedUpdate logic, no force hacking, no braking/driving separation.
///
/// ── Tuning guide ─────────────────────────────────────────────────────────
///
///   Rigidbody mass (Inspector on the hand object):
///     How much momentum the hand carries on impact. Start at 1–2 kg.
///
///   positionSpring (N/m):
///     Stiffness of the joint. Higher = hand tracks anchor faster in free air.
///     Safe range: 2000–5000. Higher values risk PhysX numerical instability.
///
///   positionDamper (N·s/m):
///     Prevents oscillation. At critical damping: 2 × √(positionSpring × rb.mass).
///       rb.mass = 1  → critical damper ≈ 110
///       rb.mass = 2  → critical damper ≈ 155
///     Use slightly below critical for a snappier feel without oscillation.
///
///   maximumForce (N):
///     The hand's push budget. This is the single resistance parameter.
///     Lower = heavier objects stall the hand more convincingly.
///     Start at 150–300 N for rb.mass = 1.
///     Unlike force-cap approaches, this cap is enforced inside the solver,
///     so it interacts correctly with contact constraints.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PhysicsHandController : MonoBehaviour
{
    [Header("References")]
    public HandTarget target;

    [Header("Joint Drive")]
    [Tooltip("Spring stiffness (N/m). Higher = faster free-air tracking. Safe range: 2000–5000.")]
    public float positionSpring = 3000f;

    [Tooltip("Damping (N·s/m). Critical (no oscillation): 2 × √(positionSpring × rb.mass). " +
             "mass=1 → 110.  mass=2 → 155.")]
    public float positionDamper = 110f;

    [Tooltip("Maximum force the joint can apply (N). This is the hand's push budget. " +
             "Heavy objects stall the hand when contact resistance exceeds this value. " +
             "Enforced inside the PhysX solver — not a post-hoc script clamp.")]
    public float maximumForce = 250f;

    // Forwarded for RageRoomCameraRotation so it can keep its existing reference
    // on the original hand GameObjects, unchanged from before the refactor.
    public Vector3 TargetLocalPosition => target != null ? target.LocalPosition : Vector3.zero;
    public float   MaxRight            => target != null ? target.maxRight      : 0f;
    public float   MaxLeft             => target != null ? target.maxLeft       : 0f;

    [HideInInspector] public Collider col;

    void Start()
    {
        var rb = GetComponent<Rigidbody>();
        col    = GetComponent<Collider>();

        rb.useGravity             = false;
        rb.freezeRotation         = true;
        rb.linearDamping          = 0f;
        rb.angularDamping         = 0f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (target == null) return;

        target.handCollider = col;
        SetupJoint();
    }

    void SetupJoint()
    {
        Rigidbody anchorRb = target.GetComponent<Rigidbody>();
        if (anchorRb == null)
        {
            Debug.LogError(
                $"[PhysicsHandController] HandTarget '{target.name}' has no Rigidbody. " +
                "HandTarget uses [RequireComponent(typeof(Rigidbody))], so the component " +
                "should be present. Verify the script compiled correctly.", target);
            return;
        }

        var joint = gameObject.AddComponent<ConfigurableJoint>();

        // ── Anchor to the kinematic HandTarget ────────────────────────────
        joint.connectedBody = anchorRb;

        // Both anchors at object origins. The drive target is zero relative
        // offset — hand should be at the same position as the anchor.
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor          = Vector3.zero;
        joint.connectedAnchor = Vector3.zero;

        // ── Free translation — the drive handles all positional tracking ──
        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Free;

        // ── Lock rotation — HandRotation.cs owns the hand's orientation ───
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;

        // ── Position drive ────────────────────────────────────────────────
        // Spring-damper that runs inside PhysX. maximumForce is enforced by
        // the solver as a constraint budget, not clipped by a script.
        // When contact resistance exceeds maximumForce, the solver lets the
        // contact win and the hand stalls — naturally, without code logic.
        // X/Y behave normally.
        var xyDrive = new JointDrive
        {
            positionSpring = positionSpring,
            positionDamper = positionDamper,
            maximumForce   = maximumForce
        };

        // Z is much stiffer so objects can't easily push the hand backward.
        var zDrive = new JointDrive
        {
            positionSpring = positionSpring * 8f,
            positionDamper = positionDamper * 3f,
            maximumForce   = maximumForce * 8f
        };

        joint.xDrive = xyDrive;
        joint.yDrive = xyDrive;
        joint.zDrive = zDrive;
        joint.targetPosition = Vector3.zero;

        // Never teleport the hand to the anchor. The gap between hand and anchor
        // under load is physically meaningful — it represents the hand being
        // genuinely resisted. Projecting would hide that behavior.
        joint.projectionMode = JointProjectionMode.None;
    }

}
