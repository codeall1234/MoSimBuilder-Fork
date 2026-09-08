using UnityEngine;
using UnityEditor;

public class DiagnoseAndFixHarvestHavoc
{
    [MenuItem("Tools/Diagnose Harvest Havoc")]
    public static void DiagnoseAndFix()
    {
        string path = "Assets/Fields/HarvestHavoc.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Could not find HarvestHavoc.prefab at " + path);
            return;
        }

        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            GameObject root = editingScope.prefabContentsRoot;
            Debug.Log("Loaded " + root.name);

            // 1. Check for Walls and Perimeter
            CheckVisibility(root, "FieldPerimeter");
            CheckVisibility(root, "BlueWall");
            CheckVisibility(root, "RedWall");

            // 2. Check for SpawnPoint and fix it
            var fms = root.GetComponent<FMS>();
            if (fms != null)
            {
                if (fms.defaultSpawn == null)
                {
                    Debug.LogWarning("FMS defaultSpawn is null! Searching for existing DefaultSpawn...");
                    Transform existingSpawn = root.transform.Find("DefaultSpawn");
                    if (existingSpawn == null)
                    {
                        Debug.Log("Creating new DefaultSpawn object.");
                        GameObject spawnObj = new GameObject("DefaultSpawn");
                        spawnObj.transform.SetParent(root.transform);
                        spawnObj.transform.localPosition = new Vector3(-4.81f, 0.15f, -1.571f);
                        spawnObj.transform.localRotation = Quaternion.Euler(0, 90, 0);
                        existingSpawn = spawnObj.transform;
                    }
                    fms.defaultSpawn = existingSpawn;
                    Debug.Log("Assigned DefaultSpawn to FMS!");
                }
                else
                {
                    Debug.Log("FMS defaultSpawn is already assigned to " + fms.defaultSpawn.name);
                }
            }
        }
        
        Debug.Log("DiagnoseAndFix completed.");
    }

    private static void CheckVisibility(GameObject root, string name)
    {
        Transform t = root.transform.Find(name);
        if (t == null)
        {
            // Try recursive search
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains(name))
                {
                    t = child;
                    break;
                }
            }
        }

        if (t == null)
        {
            Debug.LogError("Could not find " + name + " in prefab!");
            return;
        }

        Debug.Log("Checking " + t.name + "...");
        Debug.Log(" - Active In Hierarchy: " + t.gameObject.activeInHierarchy);
        Debug.Log(" - Active Self: " + t.gameObject.activeSelf);
        Debug.Log(" - Local Scale: " + t.localScale);
        Debug.Log(" - Local Position: " + t.localPosition);

        MeshRenderer[] renderers = t.GetComponentsInChildren<MeshRenderer>(true);
        Debug.Log(" - MeshRenderers found: " + renderers.Length);
        foreach (var r in renderers)
        {
            Debug.Log("   - Renderer " + r.name + " Enabled: " + r.enabled);
            var filter = r.GetComponent<MeshFilter>();
            if (filter != null)
                Debug.Log("   - Mesh: " + (filter.sharedMesh != null ? filter.sharedMesh.name : "NULL"));
            else
                Debug.Log("   - No MeshFilter found!");

            if (r.sharedMaterials != null)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    Debug.Log("   - Material: " + (mat != null ? mat.name : "NULL"));
                }
            }
        }
    }
}
