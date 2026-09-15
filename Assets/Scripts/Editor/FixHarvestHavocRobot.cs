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
                            action.Direction = Direction.forward;
                            modified = true;
                        }
                        else if (action.Type == NodeType.Intake || action.Type == NodeType.Transfer)
                        {
                            action.ControllerButton = ControllerInputs.LeftTrigger;
                            modified = true;
                        }
                    }
                }

                if (node.gameObject.name == "Stow" || node.gameObject.name == "l1Outake")
                {
                    node.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    modified = true;
                }

                if (modified)
                {
                    EditorUtility.SetDirty(node);
                }
            }

            // 2. Fix BuildArm Setpoints (Level 1, 2, 3 set to -25 deg, Score to -40 deg, Oven to -35 deg)
            BuildArm[] arms = root.GetComponentsInChildren<BuildArm>(true);
            foreach (var arm in arms)
            {
                arm.transform.localRotation = Quaternion.Euler(0f, -90f, 90f);
                EditorUtility.SetDirty(arm.transform);

                SerializedObject so = new SerializedObject(arm);
                SerializedProperty sps = so.FindProperty("setPoints");
                if (sps != null)
                {
                    for (int i = 0; i < sps.arraySize; i++)
                    {
                        var elem = sps.GetArrayElementAtIndex(i);
                        var nameProp = elem.FindPropertyRelative("setpointName");
                        var pointProp = elem.FindPropertyRelative("point");
                        var seqProp = elem.FindPropertyRelative("sequenceTo");
                        if (nameProp != null && pointProp != null)
                        {
                            string sName = nameProp.stringValue.ToLower();
                            if (sName.Contains("score"))
                            {
                                pointProp.floatValue = -40f;
                            }
                            else if (sName.Contains("l4") || sName.Contains("oven"))
                            {
                                pointProp.floatValue = -35f;
                            }
                            else if (sName.Contains("l1") || sName.Contains("l2") || sName.Contains("l3") || sName.Contains("level"))
                            {
                                pointProp.floatValue = -25f;
                                if (sName.Contains("l1") && seqProp != null)
                                {
                                    seqProp.stringValue = "Score";
                                }
                            }
                        }
                    }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(arm);
                }
            }

            // Fix Buildelevator Setpoints (Oven/L4 lowered to 5 inches)
            Buildelevator[] elevators = root.GetComponentsInChildren<Buildelevator>(true);
            foreach (var elev in elevators)
            {
                SerializedObject so = new SerializedObject(elev);
                SerializedProperty sps = so.FindProperty("setPoints");
                if (sps != null)
                {
                    for (int i = 0; i < sps.arraySize; i++)
                    {
                        var elem = sps.GetArrayElementAtIndex(i);
                        var nameProp = elem.FindPropertyRelative("setpointName");
                        var pointProp = elem.FindPropertyRelative("point");
                        if (nameProp != null && pointProp != null)
                        {
                            string sName = nameProp.stringValue.ToLower();
                            if (sName.Contains("l4") || sName.Contains("oven"))
                            {
                                pointProp.floatValue = 0f;
                            }
                            else if (sName == "l1" || sName == "level 1")
                            {
                                pointProp.floatValue = 14f;
                            }
                            else if (sName == "l2" || sName == "level 2")
                            {
                                pointProp.floatValue = 28f;
                            }
                            else if (sName == "l3" || sName == "level 3")
                            {
                                pointProp.floatValue = 42f;
                            }
                        }
                    }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(elev);
                }
            }

            // Disable AutoAlign on the robot since Harvest Havoc has no reef branches
            AutoAlign autoAlign = root.GetComponentInChildren<AutoAlign>(true);
            if (autoAlign != null && autoAlign.enabled)
            {
                autoAlign.enabled = false;
                EditorUtility.SetDirty(autoAlign);
            }

            // 3. Fix JointController Setpoints
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
