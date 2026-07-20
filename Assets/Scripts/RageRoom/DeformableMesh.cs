using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class DeformableMesh : MonoBehaviour
{
    [Header("Deformation")]
    public float deformRadius   = 0.4f;
    public float maxDentDepth   = 0.1f;
    public float minImpactSpeed = 1f;
    public float maxImpactSpeed = 14f;

    [Tooltip("Minimum time (s) between two deformations of this mesh. Deforming is " +
             "not cheap — it walks every vertex, re-uploads the vertex buffer, " +
             "recalculates normals and bounds, and schedules a convex re-cook of the " +
             "MeshCollider. Without this gate that whole chain ran on EVERY qualifying " +
             "contact, and contacts arrive far faster than once per punch (a hand with " +
             "two colliders touching an object with two colliders reports up to four). " +
             "Mirrors DestructibleObject.hitCooldown, which already had this guard.")]
    public float hitCooldown = 0.15f;

    [Header("Collider Sync")]
    public bool  updateCollider       = true;
    public float colliderRebuildDelay = 0.15f;

    Mesh         mesh;
    Vector3[]    vertices;
    Vector3[]    originalVertices;
    MeshCollider meshCollider;
    float        lastHitTime = float.NegativeInfinity;

    // Pending collider rebuild, tracked as a timestamp rather than Invoke().
    // The old CancelInvoke/Invoke pair re-armed the delay on every hit, so a
    // sustained barrage postponed the rebuild indefinitely and collision shape
    // never caught up with the visible dents until the player stopped punching.
    // A deadline that is only ever set, never pushed back, actually fires.
    bool  rebuildPending;
    float rebuildAt;

    void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            mf = GetComponentInChildren<MeshFilter>();

        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError($"[DeformableMesh] {name}: No MeshFilter with a mesh found on this object or any child.");
            enabled = false;
            return;
        }

        mesh = mf.mesh;
        mesh.MarkDynamic();
        vertices = mesh.vertices;

        if (vertices.Length == 0)
        {
            Debug.LogError($"[DeformableMesh] {name}: Mesh '{mf.sharedMesh.name}' is not CPU-readable. " +
                           "Select the model asset → Model tab → enable Read/Write → Apply.");
            enabled = false;
            return;
        }

        originalVertices = (Vector3[])vertices.Clone();
        meshCollider     = GetComponent<MeshCollider>();

        // Assign unconditionally. Guarding on "== null" meant that whenever the
        // collider already referenced the shared FBX asset mesh (which it does in
        // the authored scene), collision kept using the pristine asset while the
        // renderer showed a deformed instance — two different meshes — until the
        // first rebuild swapped it wholesale.
        if (meshCollider != null)
            meshCollider.sharedMesh = mesh;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Hand")) return;

        if (Time.time - lastHitTime < hitCooldown) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;

        lastHitTime = Time.time;

        float force = Mathf.Clamp01((speed - minImpactSpeed) / (maxImpactSpeed - minImpactSpeed));

        ContactPoint contact = collision.GetContact(0);
        Deform(contact.point, contact.normal, force);
    }

    void Deform(Vector3 worldPoint, Vector3 worldNormal, float normalizedForce)
    {
        Vector3 localPoint   = transform.InverseTransformPoint(worldPoint);
        Vector3 localDir     = transform.InverseTransformDirection(-worldNormal).normalized;
        float   localRadius  = deformRadius / transform.lossyScale.x;
        float   radiusSqr    = localRadius * localRadius;
        float   dentDepth    = maxDentDepth * normalizedForce;
        bool    anyChanged   = false;

        for (int i = 0; i < vertices.Length; i++)
        {
            // Falloff distance measured from the REST-POSE vertex instead of
            // the current/deformed vertex, so the influence neighbourhood is
            // fixed in rest space rather than drifting as the vertex sinks.
            float distSqr = (originalVertices[i] - localPoint).sqrMagnitude;
            if (distSqr >= radiusSqr) continue;

            float t = 1f - (distSqr / radiusSqr);

            // Increment-clamped accumulation instead of clamping the
            // reprojected total. currentDepth is the magnitude already
            // accumulated (read-only, no new storage). thisHitCap is this
            // hit's own falloff-derived ceiling. headroom is whatever's left
            // between them, floored at zero so a smaller ceiling from an
            // off-center later hit can only stop further growth, never claw
            // back displacement that a previous, better-centered hit already
            // earned. The increment actually applied is clamped, then added
            // directly to vertices[i] - the existing accumulated vector is
            // never rescaled or reprojected against originalVertices[i].
            float currentDepth    = (vertices[i] - originalVertices[i]).magnitude;
            float thisHitCap       = maxDentDepth * t * t;
            float headroom         = Mathf.Max(0f, thisHitCap - currentDepth);
            float desiredIncrement = dentDepth * t * t;
            float actualIncrement  = Mathf.Min(desiredIncrement, headroom);

            vertices[i] += localDir * actualIncrement;
            anyChanged   = true;
        }

        if (!anyChanged) return;

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Arm the rebuild deadline once and leave it alone. Re-arming it on each
        // subsequent hit is what starved it before.
        if (updateCollider && !rebuildPending)
        {
            rebuildPending = true;
            rebuildAt      = Time.time + colliderRebuildDelay;
        }
    }

    void Update()
    {
        if (!rebuildPending || Time.time < rebuildAt) return;

        rebuildPending = false;
        RebuildCollider();
    }

    void RebuildCollider()
    {
        if (meshCollider == null) return;

        // Reassigning the same Mesh re-cooks the PhysX shape from its current
        // vertices. Nulling first was removing the shape from the actor outright
        // for a step, which dropped every contact against it and let anything
        // resting on or pressed into the object re-penetrate and get ejected by
        // depenetration on the next step — a visible lurch mid-punch.
        meshCollider.sharedMesh = mesh;
    }
}
