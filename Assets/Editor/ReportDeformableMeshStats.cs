using UnityEditor;
using UnityEngine;

/// <summary>
/// One-off diagnostic: reports the vertex/triangle count of every DeformableMesh's visual
/// mesh AND its MeshCollider's collision mesh, to confirm (or rule out) whether the
/// Physics.BakePhysXCollisionMeshData cost profiled on RebuildCollider() is a mesh-complexity
/// problem. Run with Rage Room.unity open — works in Edit mode, no need to press Play, since
/// DeformableMesh.Start() only clones the sharedMesh into a runtime instance; the sharedMesh
/// itself (what this reads) is already the real authored/collision mesh either way.
///
/// Menu: Tools > Rage Room > Report DeformableMesh Mesh Stats
/// </summary>
public static class ReportDeformableMeshStats
{
    [MenuItem("Tools/Rage Room/Report DeformableMesh Mesh Stats")]
    public static void Report()
    {
        DeformableMesh[] deformables = Object.FindObjectsByType<DeformableMesh>(FindObjectsSortMode.None);

        if (deformables.Length == 0)
        {
            Debug.LogWarning("[DeformableMesh Stats] No DeformableMesh components found in the currently open scene(s). " +
                              "Open Assets/Scenes/Rage Room/Rage Room.unity first, then run this again.");
            return;
        }

        Debug.Log($"[DeformableMesh Stats] Found {deformables.Length} DeformableMesh instance(s).");

        foreach (DeformableMesh d in deformables)
        {
            MeshFilter mf = d.GetComponent<MeshFilter>();
            MeshCollider mc = d.GetComponent<MeshCollider>();

            Mesh visualMesh = mf != null ? mf.sharedMesh : null;
            Mesh colliderMesh = mc != null ? mc.sharedMesh : null;

            string visualInfo = visualMesh != null
                ? $"'{visualMesh.name}' — {visualMesh.vertexCount} verts, {visualMesh.triangles.Length / 3} tris"
                : "NONE (no MeshFilter.sharedMesh)";

            string colliderInfo = colliderMesh != null
                ? $"'{colliderMesh.name}' — {colliderMesh.vertexCount} verts, {colliderMesh.triangles.Length / 3} tris"
                : "NONE (no MeshCollider.sharedMesh)";

            bool sameMesh = visualMesh != null && colliderMesh != null && visualMesh == colliderMesh;

            Debug.Log(
                $"[DeformableMesh Stats] GameObject '{d.name}':\n" +
                $"    Visual mesh   : {visualInfo}\n" +
                $"    Collider mesh : {colliderInfo}\n" +
                $"    Same mesh object (collider bakes the full visual mesh)? {(sameMesh ? "YES" : "no")}",
                d.gameObject);
        }
    }
}
