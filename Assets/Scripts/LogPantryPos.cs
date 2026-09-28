using UnityEngine;

public class LogPantryPos : MonoBehaviour
{
    void Start()
    {
        var blue = GameObject.Find("BluePantry");
        var red = GameObject.Find("RedPantry");
        if (blue) Debug.Log("BluePantry Pos: " + blue.transform.position);
        if (red) Debug.Log("RedPantry Pos: " + red.transform.position);
    }
}
