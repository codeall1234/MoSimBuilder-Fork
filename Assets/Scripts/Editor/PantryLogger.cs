using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class PantryLogger
{
    static PantryLogger()
    {
        EditorApplication.delayCall += LogPantries;
    }

    private static void LogPantries()
    {
        var blue = GameObject.Find("BluePantry");
        if (blue != null)
        {
            Debug.Log($"[Antigravity] BluePantry position: {blue.transform.position}");
        }
        else
        {
            Debug.Log("[Antigravity] BluePantry NOT FOUND!");
        }

        var red = GameObject.Find("RedPantry");
        if (red != null)
        {
            Debug.Log($"[Antigravity] RedPantry position: {red.transform.position}");
        }
        else
        {
            Debug.Log("[Antigravity] RedPantry NOT FOUND!");
        }
    }
}
