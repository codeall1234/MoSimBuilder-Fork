using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;

public class LookAtRobot : MonoBehaviour
{
    [SerializeField] private Transform cam;
    private LoadMatch loadMatch;

    private Transform target;
    

    private bool lookTo;
    // Start is called before the first frame update
    void Start()
    {
        lookTo = false;
        loadMatch = FindFirstObjectByType<LoadMatch>();
        if (loadMatch != null)
        {
            target = loadMatch.GetRobotLoaded().transform;
            lookTo = loadMatch.GetTrackingType() == TrackingType.TrackRobot;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (lookTo && target != null)
        {
          cam.LookAt(target);
        }
    }
}

