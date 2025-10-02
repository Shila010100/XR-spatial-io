#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class TopSurfaceColliderBuilder_Lite
{
    // Ein-Klick-Variante: baut für jede Auswahl einen Top-Surface-MeshCollider
    [MenuItem("Tools/Physics/Build Top Collider (Lite)")]
    public static void BuildLite()
    {
        var roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            Debug.LogWarning("Select at least one root GameObject that contains your walkable meshes.");
            return;
        }

        foreach (var root in roots)
        {
            int trisAdded = 0;
            var invRoot = root.transform.worldToLocalMatrix;
            var outVerts = new List<Vector3>(4096);
            var outTris = new List<int>(8192);

            // Alle MeshFilter im Baum durchgehen
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                var mesh = mf.sharedMesh;
                if (!mesh) continue;

                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"[TopSurfaceLite] Mesh '{mesh.name}' on '{mf.name}' is NOT readable → enable Read/Write in the importer to include it.");
                    continue;
                }

                var verts = mesh.vertices;
                var tris = mesh.triangles;
                var l2w = mf.transform.localToWorldMatrix;

                // default: bis 50° geneigte Flächen gelten als begehbar
                float minUpDot = Mathf.Cos(50f * Mathf.Deg2Rad);
                float skin = 0.001f;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    var w0 = l2w.MultiplyPoint3x4(verts[tris[i]]);
                    var w1 = l2w.MultiplyPoint3x4(verts[tris[i + 1]]);
                    var w2 = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);

                    var n = Vector3.Normalize(Vector3.Cross(w1 - w0, w2 - w0));
                    if (Vector3.Dot(n, Vector3.up) < minUpDot) continue; // nicht nach oben → ignorieren

                    // hauchdünn nach oben schieben für stabile Kollision
                    w0 += n * skin; w1 += n * skin; w2 += n * skin;

                    var l0 = invRoot.MultiplyPoint3x4(w0);
                    var l1 = invRoot.MultiplyPoint3x4(w1);
                    var l2 = invRoot.MultiplyPoint3x4(w2);

                    int b = outVerts.Count;
                    outVerts.Add(l0); outVerts.Add(l1); outVerts.Add(l2);
                    outTris.Add(b); outTris.Add(b + 1); outTris.Add(b + 2);
                    trisAdded++;
                }
            }

            if (outTris.Count == 0)
            {
                Debug.LogWarning($"[TopSurfaceLite] {root.name}: no upward-facing triangles found. Check selection and Read/Write settings.");
                continue;
            }

            var colMesh = new Mesh { name = "TopSurfaceCollider_Lite" };
            colMesh.indexFormat = (outVerts.Count > 65535)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            colMesh.SetVertices(outVerts);
            colMesh.SetTriangles(outTris, 0, true);
            colMesh.RecalculateBounds();

            var mc = root.GetComponent<MeshCollider>() ?? Undo.AddComponent<MeshCollider>(root);
            mc.sharedMesh = colMesh;
            mc.convex = false;       // statisches Level
            mc.isTrigger = false;

            Debug.Log($"[TopSurfaceLite] {root.name}: built collider with {trisAdded} triangles (verts: {outVerts.Count}).");
        }
    }
}
#endif
