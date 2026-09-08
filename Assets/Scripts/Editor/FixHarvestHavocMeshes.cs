using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class FixHarvestHavocMeshes
{
    [MenuItem("Tools/Fix Harvest Havoc Meshes")]
    public static void Run()
    {
        string basePath = "Assets/Fields/HarvestHavocParts/";
        string fbxPath = "Assets/Imports/FieldParts/HarvestHavoc/";

        string[] prefabPaths = Directory.GetFiles(basePath, "*.prefab");

        foreach (string prefabPath in prefabPaths)
        {
            string p = Path.GetFileName(prefabPath);
            string fbxFile = fbxPath + p.Replace(".prefab", ".fbx");
            if (p == "FieldPerimeter.prefab") fbxFile = fbxPath + "Field Perimeter.fbx";

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxFile);

            if (prefab == null || fbx == null)
            {
                Debug.LogError($"Missing prefab or FBX for {p}");
                continue;
            }

            Object[] fbxAssets = AssetDatabase.LoadAllAssetsAtPath(fbxFile);
            Mesh[] fbxMeshes = fbxAssets.OfType<Mesh>().ToArray();
            Material[] fbxMaterials = fbxAssets.OfType<Material>().ToArray();

            if (fbxMeshes.Length == 0)
            {
                Debug.LogWarning($"No meshes found in {fbxFile}");
                continue;
            }

            // We need to modify the prefab contents, so we open it in edit mode
            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject prefabContents = editingScope.prefabContentsRoot;

                MeshFilter[] filters = prefabContents.GetComponentsInChildren<MeshFilter>(true);
                MeshCollider[] colliders = prefabContents.GetComponentsInChildren<MeshCollider>(true);
                MeshRenderer[] renderers = prefabContents.GetComponentsInChildren<MeshRenderer>(true);

                foreach (var mf in filters)
                {
                    Mesh match = fbxMeshes.FirstOrDefault(m => m.name == mf.gameObject.name) ?? fbxMeshes[0];
                    mf.sharedMesh = match;
                    EditorUtility.SetDirty(mf);
                }

                foreach (var mc in colliders)
                {
                    Mesh match = fbxMeshes.FirstOrDefault(m => m.name == mc.gameObject.name) ?? fbxMeshes[0];
                    mc.sharedMesh = match;
                    EditorUtility.SetDirty(mc);
                }
                
                foreach (var mr in renderers)
                {
                    // For materials, try to find a matching material in the FBX or project
                    if (mr.sharedMaterial == null || mr.sharedMaterial.name == "Default-Material")
                    {
                        Material matMatch = fbxMaterials.FirstOrDefault(m => m.name == mr.gameObject.name) ?? fbxMaterials.FirstOrDefault();
                        if (matMatch != null)
                        {
                            mr.sharedMaterial = matMatch;
                            EditorUtility.SetDirty(mr);
                        }
                    }
                }
            }
            Debug.Log($"Fixed meshes for {p}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
