#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ColliderBatchTools
{
    [MenuItem("Tools/Physics/Remove ALL Colliders in Selection")]
    static void RemoveAllCollidersInSelection()
    {
        var gos = Selection.gameObjects;
        if (gos == null || gos.Length == 0) { Debug.Log("No selection."); return; }

        Undo.IncrementCurrentGroup();
        int removed = 0;
        foreach (var go in gos)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                Undo.DestroyObjectImmediate(c);
                removed++;
            }
        }
        Debug.Log($"Removed {removed} collider(s) in selection.");
    }

    [MenuItem("Tools/Physics/Remove ONLY MeshColliders in Selection")]
    static void RemoveMeshCollidersInSelection()
    {
        var gos = Selection.gameObjects;
        if (gos == null || gos.Length == 0) { Debug.Log("No selection."); return; }

        Undo.IncrementCurrentGroup();
        int removed = 0;
        foreach (var go in gos)
        {
            var mcs = go.GetComponentsInChildren<MeshCollider>(true);
            foreach (var c in mcs)
            {
                Undo.DestroyObjectImmediate(c);
                removed++;
            }
        }
        Debug.Log($"Removed {removed} MeshCollider(s) in selection.");
    }

    [MenuItem("Tools/Physics/Toggle Convex on SELECTED MeshColliders")]
    static void ToggleConvexOnSelectedMeshColliders()
    {
        var mcs = Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<MeshCollider>(true)).ToArray();
        if (mcs.Length == 0) { Debug.Log("No MeshColliders in selection."); return; }

        Undo.RecordObjects(mcs, "Toggle Convex");
        bool newValue = !mcs[0].convex;
        foreach (var mc in mcs) mc.convex = newValue;
        Debug.Log($"Set convex = {newValue} on {mcs.Length} MeshCollider(s).");
    }
}
#endif
