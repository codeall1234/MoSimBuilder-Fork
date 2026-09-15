using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class InspectFieldGeometry
{
    static InspectFieldGeometry()
    {
        Run();
    }
    public static void Run()
    {
        Debug.Log("=== INSPECT FIELD GEOMETRY START ===");

        // 1. Inspect RedOven and BlueOven
        InspectFBX("Assets/Imports/FieldParts/HarvestHavoc/RedOven.fbx");
        InspectFBX("Assets/Imports/FieldParts/HarvestHavoc/BlueOven.fbx");
        InspectFBX("Assets/Imports/FieldParts/HarvestHavoc/Pantry.fbx");

        // 2. Inspect Prefabs in Assets/Fields/HarvestHavocParts
        InspectPrefab("Assets/Fields/HarvestHavocParts/RedOven.prefab");
        InspectPrefab("Assets/Fields/HarvestHavocParts/BlueOven.prefab");
        InspectPrefab("Assets/Fields/HarvestHavocParts/Pantry.prefab");

        Debug.Log("=== INSPECT FIELD GEOMETRY END ===");
    }

    private static void InspectFBX(string path)
    {
        Debug.Log($"--- FBX: {path} ---");
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in assets)
        {
            if (a is Mesh m)
            {
                Debug.Log($"Mesh: '{m.name}' Bounds: Center={m.bounds.center.ToString("F3")}, Size={m.bounds.size.ToString("F3")}, Min={m.bounds.min.ToString("F3")}, Max={m.bounds.max.ToString("F3")}");
            }
        }
    }

    private static void InspectPrefab(string path)
    {
        Debug.Log($"--- PREFAB: {path} ---");
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
        {
            Debug.LogError($"Prefab null: {path}");
            return;
        }

        var mfs = go.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in mfs)
        {
            var m = mf.sharedMesh;
            string meshName = m != null ? m.name : "null";
            Vector3 worldPos = mf.transform.position;
            Vector3 center = m != null ? mf.transform.TransformPoint(m.bounds.center) : Vector3.zero;
            Vector3 min = m != null ? mf.transform.TransformPoint(m.bounds.min) : Vector3.zero;
            Vector3 max = m != null ? mf.transform.TransformPoint(m.bounds.max) : Vector3.zero;
            Debug.Log($"GO '{mf.gameObject.name}' (Mesh: '{meshName}'): Pos={worldPos.ToString("F3")}, Transformed Bounds Center={center.ToString("F3")}, Min={min.ToString("F3")}, Max={max.ToString("F3")}");
        }
    }
}
