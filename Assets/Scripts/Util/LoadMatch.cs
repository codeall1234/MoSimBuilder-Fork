using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyBox;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Util;

[ExecuteAlways]
public class LoadMatch : MonoBehaviour
{
    [SerializeField] private GameObject[] fieldPrefab;
    [SerializeField] private bool useCustomSpawnPoint;
    [SerializeField] private Transform spawnPoint;

    [Header("Robot Selection")] [SerializeField]
    private InspectorDropdown robotSeasonSelected;
    [SerializeField] private InspectorDropdown robotSelected;

    [SerializeField] private Cameras view;

    [ConditionalField(true, nameof(isDriverStation))] [SerializeField]
    private StationNum stationNumber;
   [ConditionalField(true, nameof(isDriverStation))] [SerializeField]
    private TrackingType trackingType;
     private int selectedRobotIndex; 
     private string selectedName;
     private int selectedSeasonIndex;
     private string selectedSeasonName;
     private List<GameObject> availableRobots = new List<GameObject>();
     private List<string> availableSeasons = new List<string>();

     private bool isDriverStation() => view == Cameras.DriverStation;
    
    private GameObject _fieldHolder;
    private GameObject _activeRobot;
    private GameObject _activeCam;
    private GameObject _spawnedCamera;

    private FMS fms;

    private void OnEnable()
    {
        CheckSeasons();
        robotSeasonSelected.canBeSelected = availableSeasons;
        CheckRobots();
        robotSelected.canBeSelected = availableRobots.Select(x => x.name).ToList();
    }

    private void LateUpdate()
    {
        CheckSeasons();
        robotSeasonSelected.canBeSelected = availableSeasons;
        robotSeasonSelected.selectedIndex = selectedSeasonIndex;
        robotSeasonSelected.selectedName = selectedSeasonName;
        CheckRobots();
        robotSelected.canBeSelected = availableRobots.Select(x => x.name).ToList();
        robotSelected.selectedIndex = selectedRobotIndex;
        robotSelected.selectedName = selectedName;
    }

    private void Start()
    {
        selectedName = robotSelected.selectedName;
        selectedRobotIndex = robotSelected.selectedIndex;
        selectedSeasonIndex = robotSeasonSelected.selectedIndex;
        selectedSeasonName = robotSeasonSelected.selectedName;
        CheckRobots(); 
        ResetField();
    }
    
    private void Update()
    {
        selectedName = robotSelected.selectedName;
        selectedRobotIndex = robotSelected.selectedIndex;
        selectedSeasonIndex = robotSeasonSelected.selectedIndex;
        selectedSeasonName = robotSeasonSelected.selectedName;
        
        if (!EditorApplication.isPlayingOrWillChangePlaymode && RobotLoaded())
        {
            DeleteRobot();
        }
        if (EditorApplication.isPlaying) return;
        
        if (!CheckField())
        {
            DestroyField();
            LoadField();
        }
        
        CheckRobots(); 
    }
    
    private GameObject GetSelectedFieldPrefab()
    {
        if (fieldPrefab == null || fieldPrefab.Length == 0) return null;
        foreach (var prefab in fieldPrefab)
        {
            if (prefab != null && prefab.name == selectedSeasonName)
            {
                return prefab;
            }
        }
        return fieldPrefab[0];
    }

    private void LoadField()
    {
        _fieldHolder = new GameObject
        {
            name = "FieldHolder",
            transform = { position = Vector3.zero, rotation = Quaternion.identity, parent = transform },
            
        };
        GameObject prefabToLoad = GetSelectedFieldPrefab();
        if (prefabToLoad != null)
        {
            Instantiate(prefabToLoad, Vector3.zero, Quaternion.identity, _fieldHolder.transform);
        }
    }
    
    private bool CheckField()
    {
        if (transform.childCount == 0)
        {
            return false;
        }
        else
        {
            GameObject prefabToLoad = GetSelectedFieldPrefab();
            if (prefabToLoad != null)
            {
                return _fieldHolder.transform.Find(prefabToLoad.name+"(Clone)");
            }
            return false;
        }
    }
    
    private void DestroyField()
    {
        if (transform.Find("FieldHolder"))
        {
            _fieldHolder = transform.Find("FieldHolder").GameObject();
            DestroyImmediate(_fieldHolder);
        }
    }

    public TrackingType GetTrackingType()
    {
        return trackingType;
    }
    
    public void ResetField()
    {
        DestroyField();
        LoadField();
        SpawnRobot();
        addCamera();
        Utils.resetParentCache();
        if (fms)
        {
            fms.Restart();
        }
    }

    public void setFMS(FMS fms)
    {
        this.fms = fms;
    }

    public GameObject getFieldHolder()
    {
        return _fieldHolder;
    }
    
    private void SpawnRobot()
    {
        if (availableRobots.Count > 0 && selectedRobotIndex >= 0 && selectedRobotIndex < availableRobots.Count)
        {
            GameObject robotToSpawn = availableRobots[selectedRobotIndex];
            Transform spawnLocation = useCustomSpawnPoint ? spawnPoint : 
                                        fms != null ? fms.defaultSpawn : 
                                                        spawnPoint;
            
            if (spawnLocation == null && _fieldHolder != null)
            {
                var fmsObj = _fieldHolder.GetComponentInChildren<FMS>();
                if (fmsObj != null && fmsObj.defaultSpawn != null)
                {
                    spawnLocation = fmsObj.defaultSpawn;
                    fms = fmsObj;
                }
                else
                {
                    // Fallback to searching by name
                    Transform[] transforms = _fieldHolder.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in transforms)
                    {
                        if (t.name == "DefaultSpawn")
                        {
                            spawnLocation = t;
                            break;
                        }
                    }
                }
            }

            Vector3 pos = spawnLocation != null ? spawnLocation.position : Vector3.zero;
            Quaternion rot = spawnLocation != null ? spawnLocation.rotation : Quaternion.identity;

            _activeRobot = Instantiate(robotToSpawn, pos, rot, _fieldHolder.transform);

            var frame = _activeRobot.GetComponent<BuildFrame>();
            var controller = frame.GetSwerveController();
            if (controller)
            {
                switch (view)
                {
                    case (Cameras.FirstPerson) :
                        controller.reversed = false;
                        controller.fieldCentric = false;
                        break;
                    case (Cameras.FirstPersonReversed) :
                        controller.reversed = true;
                        controller.fieldCentric = false;
                        break;
                    case (Cameras.ThirdPerson) :
                        controller.reversed = false;
                        controller.fieldCentric = true;
                        break;
                    case (Cameras.ReversedThirdPerson) :
                        controller.reversed = true;
                        controller.fieldCentric = true;
                        break;
                    case Cameras.DriverStation :
                        controller.reversed = false;
                        controller.fieldCentric = true;
                        break;
                }
            }
        }
    }
    
    private bool RobotLoaded()
    {
        return _activeRobot != null;
    }

    public GameObject GetRobotLoaded()
    {
        return _activeRobot;
    }
    private void DeleteRobot()
    {
        DestroyImmediate(_spawnedCamera);
        DestroyImmediate(_activeRobot);
    }
    
    private void addCamera()
    {
        string objectToLoad = "Cameras/" + view.ToString();
        _activeCam = Resources.Load(objectToLoad) as GameObject;

        var parent = _activeRobot;
        var spawnRotation = spawnPoint.gameObject;
        if (fms)
        {
            parent = view == Cameras.DriverStation ? fms.blueStationCams[(int)stationNumber] : _activeRobot;
            spawnRotation = view == Cameras.DriverStation ? fms.redStationCams[(int)stationNumber] : spawnPoint.gameObject;
        }

        _spawnedCamera = Instantiate(_activeCam, Vector3.zero, spawnRotation.transform.rotation, parent.transform);;
        _spawnedCamera.transform.localPosition = Vector3.zero;
    }

    public void TogglePOV()
    {
        if (view == Cameras.ThirdPerson) view = Cameras.ReversedThirdPerson;
        else if (view == Cameras.ReversedThirdPerson) view = Cameras.ThirdPerson;
        else if (view == Cameras.FirstPerson) view = Cameras.FirstPersonReversed;
        else if (view == Cameras.FirstPersonReversed) view = Cameras.FirstPerson;
        else return;

        if (_spawnedCamera != null) Destroy(_spawnedCamera);
        addCamera();
    }
    
    public void CheckSeasons() 
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources", "Robots");
        
        availableSeasons.Clear();
        
        if (Directory.Exists(resourcesPath))
        {
            string[] rawFolderPaths = Directory.GetDirectories(resourcesPath);

            foreach (string path in rawFolderPaths)
            {
                string folderName = Path.GetFileName(path);
                availableSeasons.Add(folderName);
            }
        }
        
        if (selectedSeasonIndex >= availableSeasons.Count)
        {
            selectedSeasonIndex = availableSeasons.Count > 0 ? availableSeasons.Count - 1 : 0;
        }
    }
    
    public void CheckRobots()
    {
        string path = "Robots/" + selectedSeasonName;
        GameObject[] loadedRobots = Resources.LoadAll<GameObject>(path);
        
        availableRobots.Clear();
        foreach (var robot in loadedRobots)
        {
            availableRobots.Add(robot);
        }
        
        if (selectedRobotIndex >= availableRobots.Count)
        {
            selectedRobotIndex = availableRobots.Count > 0 ? availableRobots.Count - 1 : 0;
        }
    }
}
