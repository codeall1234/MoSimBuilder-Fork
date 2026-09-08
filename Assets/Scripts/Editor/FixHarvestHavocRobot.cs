using UnityEngine;
using UnityEditor;
using BuilderLib;
using Util;
using System.Linq;

public class FixHarvestHavocRobot
{
    [MenuItem("Tools/Fix Harvest Havoc Robot")]
    public static void Run()
    {
        string prefabPath = "Assets/Resources/Robots/HarvestHavoc/ExampleA.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            GameObject root = editingScope.prefabContentsRoot;

            // 1. Fix BuildNodes
            BuildNode[] nodes = root.GetComponentsInChildren<BuildNode>(true);
            foreach (var node in nodes)
            {
                bool modified = false;
                
                // Force preload piece to Carrot
                SerializedObject serializedNode = new SerializedObject(node);
                SerializedProperty pieceNameProp = serializedNode.FindProperty("pieceName");
                if (pieceNameProp != null)
                {
                    pieceNameProp.enumValueIndex = (int)PieceNames.Carrot;
                    serializedNode.ApplyModifiedProperties();
                    modified = true;
                }

                if (node.Actions != null)
                {
                    foreach (var action in node.Actions)
                    {
                        // Unconditionally set PieceType to Carrot and add CarrotCake
                        action.PieceType = PieceNames.Carrot;
                        if (action.AdditionalPieceTypes != null && !action.AdditionalPieceTypes.Contains(PieceNames.CarrotCake))
                        {
                            action.AdditionalPieceTypes.Add(PieceNames.CarrotCake);
                        }
                        modified = true;

                        // Ensure correct trigger based on node type
                        if (action.Type == NodeType.Outake)
                        {
                            action.ControllerButton = ControllerInputs.RightTrigger;
                            action.Speed = 10f; // Reset launch speed to 10
                            modified = true;
                        }
                        else if (action.Type == NodeType.Intake || action.Type == NodeType.Transfer)
                        {
                            action.ControllerButton = ControllerInputs.LeftTrigger;
                            modified = true;
                        }
                    }
                }

                if (modified)
                {
                    EditorUtility.SetDirty(node);
                }
            }

            // 2. Fix JointController Setpoints
            JointController[] controllers = root.GetComponentsInChildren<JointController>(true);
            foreach (var jc in controllers)
            {
                bool modified = false;
                var setPointsList = jc.setPoints.ToList();
                for (int i = 0; i < setPointsList.Count; i++)
                {
                    var sp = setPointsList[i];
                    if (sp.setpointName == "L1") { sp.setpointName = "Level 1"; modified = true; }
                    else if (sp.setpointName == "L2") { sp.setpointName = "Level 2"; modified = true; }
                    else if (sp.setpointName == "L3") { sp.setpointName = "Level 3"; modified = true; }
                    else if (sp.setpointName == "l4" || sp.setpointName == "L4") { sp.setpointName = "Oven"; modified = true; }
                }
                
                // Remove Algae setpoints to clean up
                setPointsList.RemoveAll(sp => sp.setpointName != null && sp.setpointName.Contains("Algae"));
                
                if (setPointsList.Count != jc.setPoints.Length)
                {
                    jc.setPoints = setPointsList.ToArray();
                    modified = true;
                }
                
                if (modified) EditorUtility.SetDirty(jc);
            }
        }
        
        Debug.Log("Fixed ExampleA robot preload, bindings, piece types, and setpoints!");
    }
}
