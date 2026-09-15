using UnityEngine;
using UnityEditor;
using BuilderLib;
using Util;

public class AutoFixHeights
{
    [MenuItem("Tools/Auto-Fix Robot Heights")]
    public static void Run()
    {
        string robotPath = "Assets/Resources/Robots/HarvestHavoc/ExampleA.prefab";
        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(robotPath))
        {
            GameObject root = editingScope.prefabContentsRoot;

            // 1. Elevator JointController & Buildelevator
            Buildelevator elevator = root.GetComponentInChildren<Buildelevator>(true);
            if (elevator != null)
            {
                SerializedObject so = new SerializedObject(elevator);
                SerializedProperty sps = so.FindProperty("setPoints");
                if (sps != null)
                {
                    ApplyElevatorSetpoints(sps);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(elevator);
                }
            }

            // 2. Arm JointController & BuildArm
            BuildArm[] arms = root.GetComponentsInChildren<BuildArm>(true);
            foreach (var arm in arms)
            {
                if (arm.transform.parent != null && arm.transform.parent.name.Contains("Climber")) continue;

                SerializedObject so = new SerializedObject(arm);
                SerializedProperty sps = so.FindProperty("setPoints");
                if (sps != null)
                {
                    ApplyArmSetpoints(sps);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(arm);
                }
            }

            // 3. JointController directly on GameObjects
            JointController[] controllers = root.GetComponentsInChildren<JointController>(true);
            foreach (var jc in controllers)
            {
                if (jc.setPoints != null)
                {
                    bool isElevator = !jc.isAngularJoint;
                    for (int i = 0; i < jc.setPoints.Length; i++)
                    {
                        var sp = jc.setPoints[i];
                        if (sp == null) continue;

                        string name = sp.setpointName?.Trim().ToUpper() ?? "";
                        if (name == "OVEN" || name == "L4")
                        {
                            sp.setpointName = "Oven";
                            sp.SetPointValue(isElevator ? 0f : -325f);
                            sp.controllerButton = ControllerInputs.A;
                            sp.keyboardButton = KeyboardInputs.U;
                        }
                        else if (name == "L1" || name == "LEVEL 1")
                        {
                            sp.setpointName = "L1";
                            sp.SetPointValue(isElevator ? 14f : -315f);
                            sp.controllerButton = ControllerInputs.B;
                            sp.keyboardButton = KeyboardInputs.I;
                        }
                        else if (name == "L2" || name == "LEVEL 2")
                        {
                            sp.setpointName = "L2";
                            sp.SetPointValue(isElevator ? 28f : -315f);
                            sp.controllerButton = ControllerInputs.X;
                            sp.keyboardButton = KeyboardInputs.O;
                        }
                        else if (name == "L3" || name == "LEVEL 3")
                        {
                            sp.setpointName = "L3";
                            sp.SetPointValue(isElevator ? 42f : -315f);
                            sp.controllerButton = ControllerInputs.Y;
                            sp.keyboardButton = KeyboardInputs.P;
                        }
                    }
                    EditorUtility.SetDirty(jc);
                }
            }
        }
        
        Debug.Log("Successfully applied exact field heights and button mappings (A=Oven, B=L1, X=L2, Y=L3) to ExampleA robot!");
    }

    private static void ApplyElevatorSetpoints(SerializedProperty sps)
    {
        for (int i = 0; i < sps.arraySize; i++)
        {
            var elem = sps.GetArrayElementAtIndex(i);
            var nameProp = elem.FindPropertyRelative("setpointName");
            var pointProp = elem.FindPropertyRelative("point");
            var ctrlProp = elem.FindPropertyRelative("controllerButton");
            var keyProp = elem.FindPropertyRelative("keyboardButton");

            if (nameProp == null) continue;
            string sName = nameProp.stringValue.ToUpper();

            if (sName == "OVEN" || sName == "L4")
            {
                nameProp.stringValue = "Oven";
                if (pointProp != null) pointProp.floatValue = 0f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.A;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.U;
            }
            else if (sName == "L1" || sName == "LEVEL 1")
            {
                nameProp.stringValue = "L1";
                if (pointProp != null) pointProp.floatValue = 14f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.B;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.I;
            }
            else if (sName == "L2" || sName == "LEVEL 2")
            {
                nameProp.stringValue = "L2";
                if (pointProp != null) pointProp.floatValue = 28f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.X;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.O;
            }
            else if (sName == "L3" || sName == "LEVEL 3")
            {
                nameProp.stringValue = "L3";
                if (pointProp != null) pointProp.floatValue = 42f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.Y;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.P;
            }
        }
    }

    private static void ApplyArmSetpoints(SerializedProperty sps)
    {
        for (int i = 0; i < sps.arraySize; i++)
        {
            var elem = sps.GetArrayElementAtIndex(i);
            var nameProp = elem.FindPropertyRelative("setpointName");
            var pointProp = elem.FindPropertyRelative("point");
            var ctrlProp = elem.FindPropertyRelative("controllerButton");
            var keyProp = elem.FindPropertyRelative("keyboardButton");

            if (nameProp == null) continue;
            string sName = nameProp.stringValue.ToUpper();

            if (sName == "OVEN" || sName == "L4")
            {
                nameProp.stringValue = "Oven";
                if (pointProp != null) pointProp.floatValue = -325f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.A;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.U;
            }
            else if (sName == "L1" || sName == "LEVEL 1")
            {
                nameProp.stringValue = "L1";
                if (pointProp != null) pointProp.floatValue = -315f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.B;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.I;
            }
            else if (sName == "L2" || sName == "LEVEL 2")
            {
                nameProp.stringValue = "L2";
                if (pointProp != null) pointProp.floatValue = -315f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.X;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.O;
            }
            else if (sName == "L3" || sName == "LEVEL 3")
            {
                nameProp.stringValue = "L3";
                if (pointProp != null) pointProp.floatValue = -315f;
                if (ctrlProp != null) ctrlProp.enumValueIndex = (int)ControllerInputs.Y;
                if (keyProp != null) keyProp.enumValueIndex = (int)KeyboardInputs.P;
            }
        }
    }

    [MenuItem("Tools/Setup Harvest Havoc Depots")]
    public static void SetupDepots()
    {
        string fieldPath = "Assets/Fields/HarvestHavoc.prefab";
        using (var editingScope = new PrefabUtility.EditPrefabContentsScope(fieldPath))
        {
            GameObject root = editingScope.prefabContentsRoot;
            var allDepots = root.GetComponentsInChildren<Transform>(true);
            int count = 0;
            foreach (var t in allDepots)
            {
                string name = t.gameObject.name;
                if (name.IndexOf("Depot", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("1", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                     name.IndexOf("2", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.Equals("RedDepot", System.StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("BlueDepot", System.StringComparison.OrdinalIgnoreCase)))
                {
                    if (t.gameObject.GetComponent<Field.SeasonSpecific.HarvestHavocDepot>() == null)
                    {
                        t.gameObject.AddComponent<Field.SeasonSpecific.HarvestHavocDepot>();
                        EditorUtility.SetDirty(t.gameObject);
                        count++;
                    }
                }
            }
            Debug.Log($"Setup {count} HarvestHavocDepot components on HarvestHavoc.prefab!");
        }
    }
}
