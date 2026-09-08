using UnityEngine;
using UnityEditor;
using Field.SeasonSpecific;
using BuilderLib;
using Util;
using System.Linq;
using System.Collections.Generic;

public class AutoFixHeights
{
    [MenuItem("Tools/Auto-Fix Robot Heights")]
    public static void Run()
    {
        // 1. Find Heights from Prefabs
        float[] pantryHeights = new float[3];
        float ovenHeight = 0f;

        GameObject pantryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fields/HarvestHavocParts/Pantry.prefab");
        if (pantryPrefab != null)
        {
            var scorer = pantryPrefab.GetComponentInChildren<FieldScorer>(true);
            if (scorer != null && scorer.occupyColliders != null)
            {
                List<float> heights = new List<float>();
                foreach (var coll in scorer.occupyColliders)
                {
                    if (coll != null) heights.Add(coll.transform.position.y);
                }
                heights.Sort(); // Lowest to highest
                // Apply -15 offset to each calculated height
                for (int i = 0; i < Mathf.Min(3, heights.Count); i++)
                    pantryHeights[i] = heights[i] - 15f;
            }
        }

        GameObject ovenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fields/HarvestHavocParts/RedOven.prefab");
        if (ovenPrefab == null) ovenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fields/HarvestHavocParts/BlueOven.prefab");
        if (ovenPrefab != null)
        {
            var scorer = ovenPrefab.GetComponentInChildren<FieldScorer>(true);
            if (scorer != null && scorer.occupyColliders != null)
            {
                foreach (var coll in scorer.occupyColliders)
                {
                    if (coll != null)
                    {
                        // Apply the same -15 unit offset used for pantry levels
                        ovenHeight = coll.transform.position.y - 15f;
                        break;
                    }
                }
            }
        }

        Debug.Log($"Found Heights - L1: {pantryHeights[0]}, L2: {pantryHeights[1]}, L3: {pantryHeights[2]}, Oven: {ovenHeight}");

        // 2. Apply to Robot
        string robotPath = "Assets/Resources/Robots/HarvestHavoc/ExampleA.prefab";
        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(robotPath))
        {
            GameObject root = editingScope.prefabContentsRoot;
            JointController[] controllers = root.GetComponentsInChildren<JointController>(true);
            foreach (var jc in controllers)
            {
                bool modified = false;
                if (jc.setPoints != null)
                {
                    for (int i = 0; i < jc.setPoints.Length; i++)
                    {
                        var sp = jc.setPoints[i];
                        if (sp == null) continue;

                        string name = sp.setpointName?.Trim();
                        if (name == "Level 1" || name == "L1")
                        {
                            sp.SetPointValue(ToSetpointValue(sp, pantryHeights[0]));
                            modified = true;
                        }
                        else if (name == "Level 2" || name == "L2")
                        {
                            sp.SetPointValue(ToSetpointValue(sp, pantryHeights[1]));
                            modified = true;
                        }
                        else if (name == "Level 3" || name == "L3")
                        {
                            sp.SetPointValue(ToSetpointValue(sp, pantryHeights[2]));
                            modified = true;
                        }
                        else if (name == "Oven" || name == "L4" || name == "l4")
                        {
                            sp.SetPointValue(ToSetpointValue(sp, ovenHeight));
                            modified = true;
                        }
                    }
                }
                if (modified) EditorUtility.SetDirty(jc);
            }
        }
        
        Debug.Log("Successfully applied exact field heights to ExampleA robot!");
    }

    private static float ToSetpointValue(SetPoint sp, float targetMeters)
    {
        if (sp == null || !sp.shouldScaleToUnits) return targetMeters;
        return sp.units switch
        {
            Units.Inch => targetMeters / 0.0254f,
            Units.Centimeter => targetMeters / 0.01f,
            Units.Meter => targetMeters,
            Units.Millimeter => targetMeters / 0.001f,
            _ => targetMeters
        };
    }
}
