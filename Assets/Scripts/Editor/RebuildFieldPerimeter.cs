using UnityEngine;
using UnityEditor;
using System.IO;

public class RebuildFieldPerimeter
{
    [MenuItem("Tools/Rebuild Field Perimeter")]
    public static void Run()
    {
        string fbxPath = "Assets/Imports/FieldParts/HarvestHavoc/Field Perimeter.fbx";
        string prefabPath = "Assets/Fields/HarvestHavocParts/FieldPerimeter.prefab";

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null)
        {
            Debug.LogError("Could not find FBX at: " + fbxPath);
            return;
        }

        // Instantiate the FBX into the scene temporarily
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // Add MeshColliders to everything with a MeshFilter
        MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            if (mf.gameObject.GetComponent<MeshCollider>() == null)
            {
                mf.gameObject.AddComponent<MeshCollider>();
            }
        }

        // Save over the existing prefab
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

        // Clean up the temporary instance
        GameObject.DestroyImmediate(instance);

        Debug.Log("Successfully rebuilt FieldPerimeter.prefab from FBX and added MeshColliders!");
    }
}
