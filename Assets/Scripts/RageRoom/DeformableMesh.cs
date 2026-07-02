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
            float distSqr = (vertices[i] - localPoint).sqrMagnitude;
            if (distSqr >= radiusSqr) continue;

            float   t         = 1f - (distSqr / radiusSqr);
            Vector3 candidate = vertices[i] + localDir * (dentDepth * t * t);

            Vector3 totalDisp = candidate - originalVertices[i];
            if (totalDisp.sqrMagnitude > maxDentDepth * maxDentDepth)
                candidate = originalVertices[i] + totalDisp.normalized * maxDentDepth;

            vertices[i] = candidate;
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
