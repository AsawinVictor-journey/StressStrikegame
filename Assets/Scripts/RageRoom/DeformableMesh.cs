using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class DeformableMesh : MonoBehaviour
{
    [Header("Deformation")]
    public float deformRadius   = 0.4f;
    public float maxDentDepth   = 0.1f;
    public float minImpactSpeed = 1f;
    public float maxImpactSpeed = 14f;

    [Header("Collider Sync")]
    public bool  updateCollider       = true;
    public float colliderRebuildDelay = 0.15f;

    Mesh         mesh;
    Vector3[]    vertices;
    Vector3[]    originalVertices;
    MeshCollider meshCollider;

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

        if (meshCollider != null && meshCollider.sharedMesh == null)
            meshCollider.sharedMesh = mesh;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Hand")) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;

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

        if (updateCollider)
        {
            CancelInvoke(nameof(RebuildCollider));
            Invoke(nameof(RebuildCollider), colliderRebuildDelay);
        }
    }

    void RebuildCollider()
    {
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }
}
