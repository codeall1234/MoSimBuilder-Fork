using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    /// <summary>
    /// Implements the Blair Bunnybots 2026: Harvest Havoc DEPOT functionality.
    /// - Tracks outside-field carrot inventory (starts at 17, gains 3 for each opposing oven bake).
    /// - Tracks outside-field carrot cake inventory (starts at 9, traded 1 cake for 3 opposing oven carrots).
    /// - Dispenses carrots down the 23" ramp into the ALLIANCE's FARM when robots approach or on manual input.
    /// </summary>
    public class HarvestHavocDepot : MonoBehaviour
    {
        // ==========================================
        // STATIC ALLIANCE INVENTORY TRACKING
        // ==========================================
        public static int RedCarrotsAvailable = 17;
        public static int RedCakesForTrade = 9;
        public static int BlueCarrotsAvailable = 17;
        public static int BlueCakesForTrade = 9;

        private static readonly List<HarvestHavocDepot> _allDepots = new List<HarvestHavocDepot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            ResetAllDepots();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoDiscoverDepots()
        {
            // Discover all depot GameObjects in the scene and ensure HarvestHavocDepot is attached
            var allGOs = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var go in allGOs)
            {
                if (go == null) continue;
                string name = go.name;
                if (name.IndexOf("Depot", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("1", StringComparison.OrdinalIgnoreCase) >= 0 || 
                     name.IndexOf("2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.Equals("RedDepot", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("BlueDepot", StringComparison.OrdinalIgnoreCase)))
                {
                    if (go.GetComponent<HarvestHavocDepot>() == null)
                    {
                        go.AddComponent<HarvestHavocDepot>();
                    }
                }
            }
        }

        public static void ResetAllDepots()
        {
            RedCarrotsAvailable = 17;
            RedCakesForTrade = 9;
            BlueCarrotsAvailable = 17;
            BlueCakesForTrade = 9;
            _allDepots.Clear();
        }

        /// <summary>
        /// Called by the opposing alliance's Oven when 3 carrots are baked into a cake.
        /// Those 3 carrots are handed over to this alliance's depot inventory.
        /// </summary>
        public static void AddRecirculatedCarrots(bool forBlueAlliance, int count)
        {
            if (forBlueAlliance)
            {
                BlueCarrotsAvailable += count;
                Debug.Log($"[HarvestHavocDepot] +{count} Carrots recirculated to Blue Depot! Total available: {BlueCarrotsAvailable}");
            }
            else
            {
                RedCarrotsAvailable += count;
                Debug.Log($"[HarvestHavocDepot] +{count} Carrots recirculated to Red Depot! Total available: {RedCarrotsAvailable}");
            }
        }

        /// <summary>
        /// Called when an alliance trades 3 oven-scored carrots at the opposing depot for a Carrot Cake.
        /// Returns true if a cake was available and traded; false if cakes are exhausted (carrots given for free).
        /// </summary>
        public static bool TryTradeCarrotCake(bool fromBlueAllianceDepot)
        {
            if (fromBlueAllianceDepot)
            {
                if (BlueCakesForTrade > 0)
                {
                    BlueCakesForTrade--;
                    Debug.Log($"[HarvestHavocDepot] Blue Depot traded 1 Carrot Cake! Remaining cakes: {BlueCakesForTrade}");
                    return true;
                }
                Debug.Log("[HarvestHavocDepot] Blue Depot has no Carrot Cakes left! Carrots accepted for free.");
                return false;
            }
            else
            {
                if (RedCakesForTrade > 0)
                {
                    RedCakesForTrade--;
                    Debug.Log($"[HarvestHavocDepot] Red Depot traded 1 Carrot Cake! Remaining cakes: {RedCakesForTrade}");
                    return true;
                }
                Debug.Log("[HarvestHavocDepot] Red Depot has no Carrot Cakes left! Carrots accepted for free.");
                return false;
            }
        }

        public static int GetCarrotCount(bool isBlue) => isBlue ? BlueCarrotsAvailable : RedCarrotsAvailable;
        public static int GetCakeCount(bool isBlue) => isBlue ? BlueCakesForTrade : RedCakesForTrade;

        // ==========================================
        // INSTANCE PROPERTIES & BEHAVIOR
        // ==========================================
        [Header("Depot Settings")]
        [SerializeField] private bool isBlue;
        [SerializeField] private float feedCooldown = 1.8f;
        [SerializeField] private GameObject carrotPrefab;

        private float _lastFeedTime = -999f;
        private BoxCollider _triggerCollider;
        private readonly HashSet<Collider> _nearbyRobotColliders = new HashSet<Collider>();

        public bool IsBlueAlliance
        {
            get
            {
                if (isBlue) return true;
                Transform cur = transform;
                while (cur != null)
                {
                    if (cur.name.IndexOf("Blue", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (cur.name.IndexOf("Red", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    cur = cur.parent;
                }
                // Fallback: Depots in Blue Farm are at X > 0 in the prefab
                return transform.position.x > 0f;
            }
        }

        public int CarrotsRemaining
        {
            get => IsBlueAlliance ? BlueCarrotsAvailable : RedCarrotsAvailable;
            set
            {
                if (IsBlueAlliance) BlueCarrotsAvailable = Mathf.Max(0, value);
                else RedCarrotsAvailable = Mathf.Max(0, value);
            }
        }

        void OnEnable()
        {
            if (!_allDepots.Contains(this))
                _allDepots.Add(this);
        }

        void OnDisable()
        {
            _allDepots.Remove(this);
            _nearbyRobotColliders.Clear();
        }

        void Start()
        {
            isBlue = IsBlueAlliance;

            if (carrotPrefab == null)
            {
                carrotPrefab = Resources.Load<GameObject>("Pieces/Carrot");
            }

            EnsureTriggerCollider();
        }

        private void EnsureTriggerCollider()
        {
            // Create a detection volume in front of the depot ramp inside the Farm
            var colliders = GetComponents<BoxCollider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    _triggerCollider = col;
                    break;
                }
            }

            if (_triggerCollider == null)
            {
                _triggerCollider = gameObject.AddComponent<BoxCollider>();
                _triggerCollider.isTrigger = true;
            }

            // Size and center the trigger in front of the ramp into the Farm
            // Ramp height is 23" = 0.584m above the ground
            Vector3 worldPos = transform.position;
            Vector3 rollDir = GetRampExitDirection();

            // Place trigger box centered 0.8m in front of the ramp, at height Y = 0.50m
            Vector3 triggerWorldCenter = new Vector3(
                worldPos.x + rollDir.x * 0.80f,
                0.50f,
                worldPos.z + rollDir.z * 0.80f
            );

            _triggerCollider.center = transform.InverseTransformPoint(triggerWorldCenter);
            Vector3 triggerWorldSize = new Vector3(1.20f, 0.90f, 1.20f);
            Vector3 localSize = transform.InverseTransformVector(triggerWorldSize);
            _triggerCollider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        /// <summary>
        /// Determines the direction the ramp points into the field carpet.
        /// </summary>
        public Vector3 GetRampExitDirection()
        {
            Vector3 pos = transform.position;

            // Along the long wall (Z around +4m)
            if (pos.z > 2.0f && Mathf.Abs(pos.x) < 7.0f)
            {
                // Points into the field (-Z)
                return new Vector3(0f, 0f, -1f);
            }
            // Along the end wall (X around -8.1m or +8.1m)
            if (pos.x < -6.0f)
            {
                // Red side end wall: points towards +X into field
                return new Vector3(1f, 0f, 0f);
            }
            if (pos.x > 6.0f)
            {
                // Blue side end wall: points towards -X into field
                return new Vector3(-1f, 0f, 0f);
            }

            // Fallback: towards field center (0, 0, 0)
            Vector3 toCenter = (Vector3.zero - pos);
            toCenter.y = 0;
            return toCenter.normalized;
        }

        public Vector3 GetRampSpawnPoint()
        {
            Vector3 pos = transform.position;
            Vector3 rollDir = GetRampExitDirection();

            // Top of the ramp is at Y ~ 0.72m, slightly recessed behind the ramp mouth
            return new Vector3(
                pos.x - rollDir.x * 0.15f,
                0.72f,
                pos.z - rollDir.z * 0.15f
            );
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsRobotCollider(other))
            {
                _nearbyRobotColliders.Add(other);
            }
        }

        void OnTriggerExit(Collider other)
        {
            _nearbyRobotColliders.Remove(other);
        }

        void Update()
        {
            // Clean up any destroyed robot colliders
            _nearbyRobotColliders.RemoveWhere(c => c == null);

            // Manual feed key: F key allows the player to manually request a carrot if nearby
            bool fPressed = UnityEngine.InputSystem.Keyboard.current != null && 
                            UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame;
            if (fPressed)
            {
                TryManualFeed();
            }

            // Automatic proximity feeding: when robot is waiting in front of the ramp
            if (_nearbyRobotColliders.Count > 0 && CanFeed())
            {
                DispenseCarrot();
            }
        }

        private bool CanFeed()
        {
            if (Time.time < _lastFeedTime + feedCooldown) return false;
            return CarrotsRemaining > 0;
        }

        private void TryManualFeed()
        {
            if (!CanFeed()) return;

            // Check if player robot is within 3.5 meters of this depot
            var playerInput = FindObjectOfType<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                float dist = Vector3.Distance(playerInput.transform.position, transform.position);
                if (dist <= 3.5f)
                {
                    DispenseCarrot();
                }
            }
        }

        /// <summary>
        /// Dispenses one carrot down the 23-inch depot ramp into the alliance's farm.
        /// </summary>
        public bool DispenseCarrot()
        {
            if (CarrotsRemaining <= 0)
            {
                Debug.Log($"[HarvestHavocDepot] {gameObject.name}: No carrots remaining in Depot!");
                return false;
            }

            if (carrotPrefab == null)
            {
                carrotPrefab = Resources.Load<GameObject>("Pieces/Carrot");
            }
            if (carrotPrefab == null)
            {
                Debug.LogError("[HarvestHavocDepot] Failed to load Carrot prefab from Resources/Pieces/Carrot!");
                return false;
            }

            CarrotsRemaining--;
            _lastFeedTime = Time.time;

            Vector3 spawnPos = GetRampSpawnPoint();
            Vector3 rollDir = GetRampExitDirection();

            // Orient the carrot horizontally across the ramp width (perpendicular to roll direction)
            Quaternion rot;
            if (Mathf.Abs(rollDir.z) > 0.5f)
            {
                // Rolling along Z: cylinder axis along X
                rot = Quaternion.Euler(-15f, 0f, 0f);
            }
            else
            {
                // Rolling along X: cylinder axis along Z
                rot = Quaternion.Euler(0f, 90f, -15f);
            }

            GameObject carrotObj = Instantiate(carrotPrefab, spawnPos, rot);
            var piece = carrotObj.GetComponent<GamePiece>();
            if (piece != null)
            {
                piece.state = GamePieceState.World;
            }

            // Ignore collisions with depot structure so carrot rolls out smoothly without snagging
            IgnoreDepotCollisions(carrotObj);

            // Apply downward & forward roll velocity down the 23-inch ramp
            var rb = carrotObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Downward angle velocity (sloping down into the field)
                Vector3 launchVel = rollDir * 1.8f + Vector3.down * 0.4f;
                rb.velocity = launchVel;
                // Add gentle rolling angular velocity
                Vector3 rollAxis = Vector3.Cross(Vector3.up, rollDir);
                rb.angularVelocity = rollAxis * 5f;
            }

            Debug.Log($"[HarvestHavocDepot] {gameObject.name} rolled Carrot into Farm! Carrots remaining: {CarrotsRemaining}");
            return true;
        }

        private void IgnoreDepotCollisions(GameObject pieceObj)
        {
            var pieceColliders = pieceObj.GetComponentsInChildren<Collider>(true);
            var depotColliders = GetComponentsInChildren<Collider>(true);

            foreach (var pCol in pieceColliders)
            {
                if (pCol == null || pCol.isTrigger) continue;
                foreach (var dCol in depotColliders)
                {
                    if (dCol == null || dCol.isTrigger) continue;
                    Physics.IgnoreCollision(pCol, dCol, true);
                }
            }
        }

        private bool IsRobotCollider(Collider col)
        {
            if (col == null || col.isTrigger) return false;
            return col.GetComponentInParent<UnityEngine.InputSystem.PlayerInput>() != null ||
                   col.GetComponentInParent<JointController>() != null ||
                   col.GetComponentInParent<BuildMechanism>() != null ||
                   Utils.FindParentObjectComponent<BuildFrame>(col.gameObject) != null;
        }
    }
}
