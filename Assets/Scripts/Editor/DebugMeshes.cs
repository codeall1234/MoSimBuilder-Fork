using UnityEngine;
using UnityEditor;
using System.IO;

public class DebugMeshes
{
    [MenuItem("Tools/Debug Meshes")]
    public static void Run()
    {
        string prefabPath = "Assets/Fields/HarvestHavocParts/BlueWall.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) {
            Debug.LogError("Prefab not found");
            return;
        }

        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null)
            {
                Debug.LogError("Missing mesh on: " + mf.gameObject.name);
            }
            else
            {
                Debug.Log("Found mesh: " + mf.sharedMesh.name + " on " + mf.gameObject.name);
            }
        }
    }
}
