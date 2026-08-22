using System.Collections.Generic;
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

    [Tooltip("Seconds between a qualifying hit and the collider rebuild it schedules. " +
             "Raised from 0.15s to 0.3s (punch cooldown is 0.25s) so consecutive punches " +
             "that each cross the rebuild threshold coalesce into one rebuild instead of " +
             "each getting its own — at 0.15s the delay always finished before the next " +
             "punch could land, so it never actually batched anything.")]
    public float colliderRebuildDelay = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Collider rebuild (a synchronous MeshCollider convex re-cook — expensive, " +
             "shows up as a Physics.Simulate spike) only fires once deformation " +
             "accumulated since the last rebuild reaches this fraction of |maxDentDepth|. " +
             "Small/glancing hits still deform the visible mesh immediately; they just " +
             "don't each pay for their own collider re-cook. TRADEOFF: the collision " +
             "shape can lag a few small hits behind the visual dent — an object may show " +
             "2-3 small dents before the collider catches up to match them.")]
    public float rebuildThresholdFraction = 0.15f;

    [Header("Collision Proxy")]
    [Tooltip("Quads per box face on the generated low-poly collision proxy (verts per face " +
             "= (segments+1)²; 6 faces total, minus a little for shared edges isn't tracked — " +
             "close enough). Measured on this scene's actual meshes: 3 of 5 deformable " +
             "objects are 36-50k triangles, and MeshCollider.sharedMesh reassignment re-cooks " +
             "a convex hull from whatever mesh it points at — profiled at ~196ms for one of " +
             "those. The collider now bakes a small procedurally-generated box-cage sized to " +
             "the object's bounds instead of the full visual mesh; segments=3 is 96 verts " +
             "regardless of the visual mesh's complexity. The visual mesh is untouched and " +
             "keeps deforming/rendering at full detail — only the collision source changed.\n\n" +
             "TRADEOFF: the proxy is coarse, so it won't visually dent to match every small " +
             "hit — a glancing hit that lands between two proxy grid vertices may not move " +
             "the collision shape at all, even though the visual mesh dents right where you " +
             "hit it. \"Close enough\" collision feel, not pixel-accurate. Raise segments if " +
             "that reads as too coarse; cost stays trivial even at, say, 6 (~300 verts).")]
    public int proxySegments = 3;

    Mesh         mesh;
    Vector3[]    vertices;
    Vector3[]    originalVertices;
    MeshCollider meshCollider;
    float        lastHitTime = float.NegativeInfinity;

    // Low-poly stand-in for the MeshCollider bake — see proxySegments' tooltip. Deformed in
    // lockstep with the visual mesh (same falloff math, far fewer vertices), never rendered.
    Mesh      proxyMesh;
    Vector3[] proxyVertices;
    Vector3[] proxyOriginalVertices;

    // Sum of the peak per-vertex increment actually applied on each hit since the last
    // collider rebuild (not the requested dentDepth — that can be clamped down to near
    // nothing once a vertex is near its per-hit cap). Reset whenever RebuildCollider() runs.
    float accumulatedDeformationSinceRebuild;

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

        BuildCollisionProxy();

        // Assign unconditionally. Guarding on "== null" meant that whenever the
        // collider already referenced the shared FBX asset mesh (which it does in
        // the authored scene), collision kept using the pristine asset while the
        // renderer showed a deformed instance — two different meshes — until the
        // first rebuild swapped it wholesale. Uses the proxy, not the visual mesh —
        // see proxySegments' tooltip.
        if (meshCollider != null)
            meshCollider.sharedMesh = proxyMesh;
    }

    // Builds a coarse box-cage mesh sized to the visual mesh's rest-pose local bounds, used
    // as the MeshCollider's bake source instead of the full-detail visual mesh. Since these
    // colliders are convex (m_Convex on the MeshCollider), PhysX's convex hull bake only
    // depends on vertex POSITIONS — triangle winding/connectivity is irrelevant to the
    // resulting hull — so the triangle indices below just need to be non-degenerate, not
    // outward-facing-correct.
    void BuildCollisionProxy()
    {
        Bounds localBounds = new Bounds(originalVertices[0], Vector3.zero);
        for (int i = 1; i < originalVertices.Length; i++)
            localBounds.Encapsulate(originalVertices[i]);

        int segments = Mathf.Max(1, proxySegments);
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        AddBoxFace(verts, tris, localBounds, new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), segments);
        AddBoxFace(verts, tris, localBounds, new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), segments);
        AddBoxFace(verts, tris, localBounds, new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), segments);
        AddBoxFace(verts, tris, localBounds, new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), segments);
        AddBoxFace(verts, tris, localBounds, new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), segments);
        AddBoxFace(verts, tris, localBounds, new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), segments);

        proxyMesh = new Mesh { name = mesh.name + "_CollisionProxy" };
        proxyMesh.MarkDynamic();
        proxyMesh.SetVertices(verts);
        proxyMesh.SetTriangles(tris, 0);
        proxyMesh.RecalculateBounds();

        proxyOriginalVertices = verts.ToArray();
        proxyVertices          = (Vector3[])proxyOriginalVertices.Clone();
    }

    // One face of the box cage: a (segments+1)×(segments+1) grid spanning localBounds along
    // axisA/axisB, offset to the given face's extent along normal. axisA/axisB don't need to
    // form a particular handedness with normal — see BuildCollisionProxy's convex-bake note.
    static void AddBoxFace(List<Vector3> verts, List<int> tris, Bounds localBounds,
        Vector3 normal, Vector3 axisA, Vector3 axisB, int segments)
    {
        int baseIndex  = verts.Count;
        Vector3 center = localBounds.center + Vector3.Scale(normal, localBounds.extents);

        for (int j = 0; j <= segments; j++)
        {
            float v = (float)j / segments;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;

                Vector3 pos = center
                    + Vector3.Scale(axisA, localBounds.extents) * Mathf.Lerp(-1f, 1f, u)
                    + Vector3.Scale(axisB, localBounds.extents) * Mathf.Lerp(-1f, 1f, v);

                verts.Add(pos);
            }
        }

        int rowLength = segments + 1;
        for (int j = 0; j < segments; j++)
        {
            for (int i = 0; i < segments; i++)
            {
                int i0 = baseIndex + j * rowLength + i;
                int i1 = i0 + 1;
                int i2 = i0 + rowLength;
                int i3 = i2 + 1;

                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
        }
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
        Vector3 localPoint  = transform.InverseTransformPoint(worldPoint);
        Vector3 localDir    = transform.InverseTransformDirection(-worldNormal).normalized;
        float   localRadius = deformRadius / transform.lossyScale.x;
        float   radiusSqr   = localRadius * localRadius;
        float   dentDepth   = maxDentDepth * normalizedForce;

        bool anyChanged = DeformVertexArray(vertices, originalVertices, localPoint, localDir,
            radiusSqr, dentDepth, out float peakIncrement);

        // Proxy deformed with the same math against its own (far sparser) vertex set — see
        // proxySegments' tooltip for why a hit may not move any proxy vertex at all.
        DeformVertexArray(proxyVertices, proxyOriginalVertices, localPoint, localDir,
            radiusSqr, dentDepth, out _);

        if (!anyChanged) return;

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        proxyMesh.SetVertices(proxyVertices);
        proxyMesh.RecalculateBounds();

        // Threshold-gated: a rebuild is a synchronous MeshCollider convex re-cook (expensive
        // — see colliderRebuildDelay's tooltip), so only schedule one once accumulated
        // deformation since the last rebuild actually amounts to something. Small/glancing
        // hits still deform the visible mesh above; they just don't each pay for a re-cook.
        accumulatedDeformationSinceRebuild += peakIncrement;
        float rebuildThreshold = Mathf.Abs(maxDentDepth) * rebuildThresholdFraction;

        // Arm the rebuild deadline once and leave it alone. Re-arming it on each
        // subsequent hit is what starved it before.
        if (updateCollider && !rebuildPending && accumulatedDeformationSinceRebuild >= rebuildThreshold)
        {
            rebuildPending = true;
            rebuildAt      = Time.time + colliderRebuildDelay;
        }
    }

    // Applies the dent falloff to one vertex array (the visual mesh's, or the proxy's) in
    // place. Returns whether any vertex was in range, and the largest single-vertex increment
    // actually applied (magnitude, not signed — see its call site).
    bool DeformVertexArray(Vector3[] verts, Vector3[] originalVerts, Vector3 localPoint,
        Vector3 localDir, float radiusSqr, float dentDepth, out float peakIncrement)
    {
        bool  anyChanged   = false;
        float peak         = 0f;

        for (int i = 0; i < verts.Length; i++)
        {
            // Falloff distance measured from the REST-POSE vertex instead of
            // the current/deformed vertex, so the influence neighbourhood is
            // fixed in rest space rather than drifting as the vertex sinks.
            float distSqr = (originalVerts[i] - localPoint).sqrMagnitude;
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
            // directly to verts[i] - the existing accumulated vector is
            // never rescaled or reprojected against originalVerts[i].
            float currentDepth    = (verts[i] - originalVerts[i]).magnitude;
            float thisHitCap       = maxDentDepth * t * t;
            float headroom         = Mathf.Max(0f, thisHitCap - currentDepth);
            float desiredIncrement = dentDepth * t * t;
            float actualIncrement  = Mathf.Min(desiredIncrement, headroom);

            verts[i]   += localDir * actualIncrement;
            anyChanged  = true;

            // Magnitude, not signed value: maxDentDepth (and therefore actualIncrement) is
            // authored negative in this scene, so comparing the raw signed value against a
            // peak starting at 0 would never update past 0 and the threshold below would
            // never be crossed.
            float incrementMagnitude = Mathf.Abs(actualIncrement);
            if (incrementMagnitude > peak)
                peak = incrementMagnitude;
        }

        peakIncrement = peak;
        return anyChanged;
    }

    void Update()
    {
        if (!rebuildPending || Time.time < rebuildAt) return;

        rebuildPending = false;
        accumulatedDeformationSinceRebuild = 0f;
        RebuildCollider();
    }

    void RebuildCollider()
    {
        if (meshCollider == null) return;

        // Reassigning the same Mesh re-cooks the PhysX shape from its current vertices.
        // Nulling first was removing the shape from the actor outright for a step, which
        // dropped every contact against it and let anything resting on or pressed into the
        // object re-penetrate and get ejected by depenetration on the next step — a visible
        // lurch mid-punch. Bakes proxyMesh (the low-poly cage), not the full visual mesh —
        // see proxySegments' tooltip.
        meshCollider.sharedMesh = proxyMesh;
    }
}
