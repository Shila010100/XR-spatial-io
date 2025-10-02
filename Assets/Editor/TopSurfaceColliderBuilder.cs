#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class TopSurfaceColliderBuilder : EditorWindow
{
    // Einstellungen
    float maxSlopeDeg = 35f;           // maximal erlaubte Steigung für begehbare Flächen
    float skin = 0.002f;               // hauchdünn „nach oben“ extrudieren für stabile Physik
    float minTriArea = 0.01f;          // sehr kleine Dreiecke ignorieren (m²)
    bool markStatic = true;            // Umgebung als Static markieren
    bool convex = false;               // für dynamische RBs true, für Level false
    bool removeExistingColliders = true; // vorhandene Collider im Auswahlbaum vorher löschen
    bool onlyWalkableLayer = true;     // nur Objekte auf Layer "Walkable" berücksichtigen
    string walkableLayerName = "Walkable";
    bool ignoreDisabledRenderers = true;

    float minUpDot; // berechnet aus maxSlopeDeg

    [MenuItem("Tools/Physics/Build Top Surface Colliders (from Mesh)")]
    public static void Open() => GetWindow<TopSurfaceColliderBuilder>("Top Surface Colliders");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Top-Surface-MeshCollider (nur nach oben gerichtete Dreiecke)", EditorStyles.boldLabel);
        maxSlopeDeg = EditorGUILayout.Slider("Max Slope (deg)", maxSlopeDeg, 0f, 60f);
        skin = EditorGUILayout.Slider("Thin Extrusion", skin, 0f, 0.01f);
        minTriArea = EditorGUILayout.Slider("Min Triangle Area (m²)", minTriArea, 0f, 0.1f);
        EditorGUILayout.Space(4);
        markStatic = EditorGUILayout.Toggle("Mark as Static", markStatic);
        convex = EditorGUILayout.Toggle("MeshCollider Convex", convex);
        removeExistingColliders = EditorGUILayout.Toggle("Remove existing Colliders in Selection", removeExistingColliders);
        EditorGUILayout.Space(4);
        onlyWalkableLayer = EditorGUILayout.Toggle("Only process layer", onlyWalkableLayer);
        if (onlyWalkableLayer)
            walkableLayerName = EditorGUILayout.TextField("Layer Name", walkableLayerName);
        ignoreDisabledRenderers = EditorGUILayout.Toggle("Ignore disabled Renderers", ignoreDisabledRenderers);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Process Selected GameObjects"))
        {
            minUpDot = Mathf.Cos(maxSlopeDeg * Mathf.Deg2Rad);
            ProcessSelection();
        }

        EditorGUILayout.HelpBox(
            "Wähle in der Hierarchy die Container mit begehbaren Meshes (z. B. Floors/Rampen). " +
            "Optional zuvor Layer 'Walkable' vergeben. Dann klicken.",
            MessageType.Info
        );
    }

    void ProcessSelection()
    {
        var roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            Debug.LogWarning("No GameObjects selected.");
            return;
        }

        Undo.IncrementCurrentGroup();

        // Optional: vorhandene Collider entfernen
        if (removeExistingColliders)
        {
            int removed = 0;
            foreach (var r in roots)
            {
                var cols = r.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols) { Undo.DestroyObjectImmediate(c); removed++; }
            }
            if (removed > 0) Debug.Log($"[TopSurface] Removed {removed} existing collider(s) in selection.");
        }

        int totalBuilt = 0;
        foreach (var root in roots)
            totalBuilt += BuildForRoot(root);

        Debug.Log($"[TopSurface] Built {totalBuilt} MeshCollider(s).");
    }

    int BuildForRoot(GameObject root)
    {
        int walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        var invRoot = root.transform.worldToLocalMatrix;

        // Sammel-Listen für EIN MeshCollider am Root
        var outVerts = new List<Vector3>(4096);
        var outTris = new List<int>(8192);

        // Alle MeshFilter im Baum einsammeln
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        int addedTris = 0;

        foreach (var mf in filters)
        {
            if (onlyWalkableLayer && mf.gameObject.layer != walkableLayer) continue;

            var mr = mf.GetComponent<MeshRenderer>();
            if (ignoreDisabledRenderers && (!mr || !mr.enabled || !mr.gameObject.activeInHierarchy))
                continue;

            var mesh = mf.sharedMesh;
            if (!mesh) continue;

            // Sicherstellen, dass Normals vorhanden sind
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length == 0)
            {
                // temporäre Kopie mit recalculierten Normals
                var temp = Object.Instantiate(mesh);
                temp.RecalculateNormals();
                verts = temp.vertices;
                tris = temp.triangles;
                normals = temp.normals;
            }

            var L2W = mf.transform.localToWorldMatrix;

            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];

                // Weltkoordinaten pro Dreieck
                var w0 = L2W.MultiplyPoint3x4(verts[i0]);
                var w1 = L2W.MultiplyPoint3x4(verts[i1]);
                var w2 = L2W.MultiplyPoint3x4(verts[i2]);

                // Face-Normal + Up-Ausrichtung checken
                var faceNormal = Vector3.Normalize(Vector3.Cross(w1 - w0, w2 - w0));
                float dotUp = Vector3.Dot(faceNormal, Vector3.up);
                if (dotUp < minUpDot) continue;

                // Winzige Dreiecke ignorieren (Rauschen)
                float triArea = 0.5f * Vector3.Cross(w1 - w0, w2 - w0).magnitude;
                if (triArea < minTriArea) continue;

                // hauchdünn „nach oben“ verschieben
                w0 += faceNormal * skin;
                w1 += faceNormal * skin;
                w2 += faceNormal * skin;

                // zurück in lokale Koordinaten des ROOT-Objekts
                var l0 = invRoot.MultiplyPoint3x4(w0);
                var l1 = invRoot.MultiplyPoint3x4(w1);
                var l2 = invRoot.MultiplyPoint3x4(w2);

                int baseIdx = outVerts.Count;
                outVerts.Add(l0); outVerts.Add(l1); outVerts.Add(l2);
                outTris.Add(baseIdx); outTris.Add(baseIdx + 1); outTris.Add(baseIdx + 2);
                addedTris++;
            }
        }

        if (outTris.Count == 0) return 0;

        var colMesh = new Mesh();
        colMesh.name = "TopSurfaceCollider";
        colMesh.indexFormat = (outVerts.Count > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16;
        colMesh.SetVertices(outVerts);
        colMesh.SetTriangles(outTris, 0, true);
        colMesh.RecalculateBounds();

        // Einen MeshCollider am Root anbringen/verwerten
        var mc = root.GetComponent<MeshCollider>();
        if (!mc) mc = Undo.AddComponent<MeshCollider>(root);
        else
        {
            // alte Mesh freigeben, wenn von uns erzeugt
            if (mc.sharedMesh && mc.sharedMesh.name == "TopSurfaceCollider")
                Object.DestroyImmediate(mc.sharedMesh);
        }

        mc.sharedMesh = colMesh;
        mc.convex = convex; // für Level i.d.R. false

        if (markStatic)
            GameObjectUtility.SetStaticEditorFlags(root,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        Debug.Log($"[TopSurface] '{root.name}': {addedTris} triangles → MeshCollider verts:{outVerts.Count}");
        return 1;
    }
}
#endif
