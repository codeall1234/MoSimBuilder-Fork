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
    /// Official Game Manual Rules (Pages 8, 11-12, 16, 20-21, 28-29):
    /// - 24 Carrots per alliance total:
    ///   * 5 prestaged on the field
    ///   * 2 preloaded in robot
    ///   * 17 held outside the field behind the alliance's Depot
    /// - 10 Carrot Cakes per alliance total:
    ///   * 1 prestaged on dinner table
    ///   * 9 held outside the field at the opposing alliance's Depot for 3:1 recirculation trades
    /// - Each Alliance has 2 Depots in their Farm:
    ///   * Red Alliance Farm: RedDepot1, RedDepot2 (located at X < 0)
    ///   * Blue Alliance Farm: BlueDepot1, BlueDepot2 (located at X > 0)
    /// - Depots dispense carrots down their 23-inch ramp into the alliance's Farm
    ///   when an alliance robot approaches or the player presses 'F'.
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
            EnsureAllDepots();
        }

        private static bool _depotsInitialized = false;

        public static void EnsureAllDepots()
        {
            _allDepots.RemoveAll(d => d == null);
            if (_allDepots.Count >= 4) return;
            if (_depotsInitialized && _allDepots.Count > 0) return;

            string[] depotNames = { "RedDepot1", "RedDepot2", "BlueDepot1", "BlueDepot2" };
            foreach (var name in depotNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    var comp = go.GetComponent<HarvestHavocDepot>();
                    if (comp == null) comp = go.AddComponent<HarvestHavocDepot>();
                    if (!_allDepots.Contains(comp)) _allDepots.Add(comp);
                }
            }

            if (_allDepots.Count > 0)
            {
                _depotsInitialized = true;
            }
        }

        public static void ResetAllDepots()
        {
            RedCarrotsAvailable = 17;
            RedCakesForTrade = 9;
            BlueCarrotsAvailable = 17;
            BlueCakesForTrade = 9;
            _allDepots.Clear();
            _depotsInitialized = false;
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

        /// <summary>
        /// Checks if the robot currently holds or has intaked a carrot,
        /// or if a carrot is physically resting inside its frame.
        /// </summary>
        public static bool RobotHasCarrot(GameObject robot)
        {
            if (robot == null) return false;

            // 1. Any BuildNode on the robot holding a piece
            var nodes = robot.GetComponentsInChildren<BuildNode>(true);
            foreach (var node in nodes)
            {
                if (node != null && node.currentGamePiece != null) return true;
            }

            // 2. Any GamePiece child of robot
            var childPieces = robot.GetComponentsInChildren<GamePiece>(true);
            foreach (var p in childPieces)
            {
                if (p != null && p.state != GamePieceState.Stationary) return true;
            }

            // 3. Any GamePiece in scene owned by the robot or resting physically inside robot frame
            Vector3 rPos = robot.transform.position;
            var allPieces = FindObjectsOfType<GamePiece>();
            foreach (var p in allPieces)
            {
                if (p == null) continue;
                if (p.owner != null && p.owner.IsChildOf(robot.transform)) return true;

                if (p.state != GamePieceState.Stationary)
                {
                    float dist2D = Vector2.Distance(
                        new Vector2(p.transform.position.x, p.transform.position.z),
                        new Vector2(rPos.x, rPos.z)
                    );
                    float yDiff = p.transform.position.y - rPos.y;
                    if (dist2D <= 0.65f && yDiff >= -0.1f && yDiff <= 1.2f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

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
                // Blue Farm is at positive X; Red Farm is at negative X
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

            Vector3 mouthPos = GetRampMouthPosition();
            Vector3 rollDir = GetRampExitDirection();
            // Small tight trigger box right at the mouth drop zone
            Vector3 dropZone = mouthPos + rollDir * 0.15f + Vector3.down * 0.15f;
            _triggerCollider.center = transform.InverseTransformPoint(dropZone);
            Vector3 triggerWorldSize = new Vector3(0.60f, 0.50f, 0.60f);
            Vector3 localSize = transform.InverseTransformVector(triggerWorldSize);
            _triggerCollider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        /// <summary>
        /// Exact world position of the 23-inch ramp exit mouth into the Farm.
        /// </summary>
        public Vector3 GetRampMouthPosition()
        {
            Vector3 pos = transform.position;
            Vector3 rollDir = GetRampExitDirection();
            return new Vector3(
                pos.x + rollDir.x * 0.60f,
                0.584f,
                pos.z + rollDir.z * 0.60f
            );
        }

        /// <summary>
        /// Direction the depot ramp points into the Farm carpet.
        /// </summary>
        public Vector3 GetRampExitDirection()
        {
            Vector3 pos = transform.position;

            // Along the top side wall (Z around +4m): RedDepot1, BlueDepot1
            if (pos.z > 2.0f && Mathf.Abs(pos.x) < 6.5f)
            {
                return new Vector3(0f, 0f, -1f);
            }
            // Along the Red end wall (X around -8.1m): RedDepot2
            if (pos.x < -6.0f)
            {
                return new Vector3(1f, 0f, 0f);
            }
            // Along the Blue end wall (X around +8.1m): BlueDepot2
            if (pos.x > 6.0f)
            {
                return new Vector3(-1f, 0f, 0f);
            }

            // Fallback towards field center
            Vector3 toCenter = (Vector3.zero - pos);
            toCenter.y = 0;
            return toCenter.normalized;
        }

        public Vector3 GetRampSpawnPoint()
        {
            Vector3 pos = transform.position;
            Vector3 rollDir = GetRampExitDirection();

            // Top of the ramp is at Y ~ 0.72m, slightly behind ramp mouth
            return new Vector3(
                pos.x - rollDir.x * 0.15f,
                0.72f,
                pos.z - rollDir.z * 0.15f
            );
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsRobotOfOurAlliance(other))
            {
                _nearbyRobotColliders.Add(other);
            }
        }

        void OnTriggerExit(Collider other)
        {
            _nearbyRobotColliders.Remove(other);
        }

        private float _lastProximityCheckTime;
        private SwerveController _cachedRobot;

        void Update()
        {
            // Throttle proximity check to 10 Hz to completely eliminate CPU lag
            if (Time.time < _lastProximityCheckTime + 0.1f) return;
            _lastProximityCheckTime = Time.time;

            // Clean up destroyed colliders
            _nearbyRobotColliders.RemoveWhere(c => c == null);

            // Manual feed key: F key allows the player to manually request a carrot if right in front
            bool fPressed = UnityEngine.InputSystem.Keyboard.current != null && 
                            UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame;
            if (fPressed)
            {
                TryManualFeed();
            }

            // Continuous proximity check: only feeds if the robot is right in front of the mouth and has no carrot
            CheckChassisProximity();

            // Automatic proximity feeding from trigger
            if (_nearbyRobotColliders.Count > 0 && CanFeed())
            {
                foreach (var col in _nearbyRobotColliders)
                {
                    if (col == null) continue;
                    var robotRoot = GetRobotRoot(col.gameObject);
                    if (robotRoot != null && !RobotHasCarrot(robotRoot))
                    {
                        DispenseCarrot();
                        break;
                    }
                }
            }
        }

        private void CheckChassisProximity()
        {
            if (!CanFeed()) return;

            if (_cachedRobot == null)
            {
                _cachedRobot = FindObjectOfType<SwerveController>();
            }
            if (_cachedRobot == null) return;

            Vector3 mouthPos = GetRampMouthPosition();
            Vector3 rollDir = GetRampExitDirection();
            Vector3 robotPos = _cachedRobot.transform.position;

            // Must be right in front of the mouth (chassis center <= 0.85m)
            float distToMouth = Vector2.Distance(
                new Vector2(robotPos.x, robotPos.z),
                new Vector2(mouthPos.x, mouthPos.z)
            );
            if (distToMouth > 0.85f) return;

            // Directional alignment: robot must be in front of the mouth into the field
            Vector3 toRobot = robotPos - mouthPos;
            float forwardDot = Vector3.Dot(toRobot, rollDir);
            if (forwardDot < -0.1f || forwardDot > 0.95f) return;

            // Alliance check
            if (!IsRobotOfOurAlliance(_cachedRobot.gameObject)) return;

            // If robot already has a carrot, DO NOT deposit another one!
            if (RobotHasCarrot(_cachedRobot.gameObject)) return;

            DispenseCarrot();
        }

        private bool CanFeed()
        {
            if (Time.time < _lastFeedTime + feedCooldown) return false;
            return CarrotsRemaining > 0;
        }

        private void TryManualFeed()
        {
            if (!CanFeed()) return;

            if (_cachedRobot == null) _cachedRobot = FindObjectOfType<SwerveController>();
            if (_cachedRobot == null) return;

            Vector3 mouthPos = GetRampMouthPosition();
            float distToMouth = Vector2.Distance(
                new Vector2(_cachedRobot.transform.position.x, _cachedRobot.transform.position.z),
                new Vector2(mouthPos.x, mouthPos.z)
            );

            // Manual F key requires robot to be in front of the mouth (<= 1.1m)
            if (distToMouth <= 1.1f && IsRobotOfOurAlliance(_cachedRobot.gameObject))
            {
                if (RobotHasCarrot(_cachedRobot.gameObject))
                {
                    Debug.Log($"[HarvestHavocDepot] Robot already has a carrot! Not dispensing.");
                    return;
                }
                DispenseCarrot();
            }
        }

        /// <summary>
        /// Dispenses one carrot down the 23-inch depot ramp directly falling into the robot.
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

            Vector3 mouthPos = GetRampMouthPosition();
            Vector3 rollDir = GetRampExitDirection();

            // Spawn right at the ramp mouth lip so it immediately drops down into the robot below
            Vector3 spawnPos = mouthPos + Vector3.up * 0.08f;

            // Orient carrot horizontally across the ramp width (cylinder axis along width)
            Quaternion rot = Mathf.Abs(rollDir.z) > 0.5f 
                ? Quaternion.Euler(0f, 90f, 0f) 
                : Quaternion.Euler(0f, 0f, 0f);

            // Parent to field holder so it is properly managed and destroyed on Reset
            Transform parentTransform = null;
            var loadMatch = FindObjectOfType<LoadMatch>();
            if (loadMatch != null && loadMatch.getFieldHolder() != null)
            {
                parentTransform = loadMatch.getFieldHolder().transform;
            }

            GameObject carrotObj = Instantiate(carrotPrefab, spawnPos, rot, parentTransform);
            var piece = carrotObj.GetComponent<GamePiece>();
            if (piece != null)
            {
                piece.state = GamePieceState.World;
            }

            // Ignore collisions with depot structure so carrot drops smoothly
            IgnoreDepotCollisions(carrotObj);

            // Apply gentle forward nudge and downward gravity drop into the robot
            var rb = carrotObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                Vector3 launchVel = rollDir * 0.4f + Vector3.down * 1.0f;
                rb.velocity = launchVel;
                Vector3 rollAxis = Vector3.Cross(Vector3.up, rollDir);
                rb.angularVelocity = rollAxis * 2f;
            }

            Debug.Log($"[HarvestHavocDepot] {gameObject.name} dropped Carrot into Robot! Carrots remaining: {CarrotsRemaining}");
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

        private GameObject GetRobotRoot(GameObject colObj)
        {
            if (colObj == null) return null;
            var playerInput = colObj.GetComponentInParent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null) return playerInput.gameObject;

            var swerve = colObj.GetComponentInParent<SwerveController>();
            if (swerve != null) return swerve.gameObject;

            var frame = Utils.FindParentObjectComponent<BuildFrame>(colObj);
            if (frame != null) return frame.gameObject;

            var mech = colObj.GetComponentInParent<BuildMechanism>();
            if (mech != null) return mech.gameObject;

            return null;
        }

        public static bool IsRobotBlueAlliance(GameObject robot)
        {
            if (robot == null) return false;

            // 1. Explicit name check
            if (robot.name.IndexOf("Blue", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (robot.name.IndexOf("Red", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            // 2. FMS spawn check
            var fms = FindObjectOfType<FMS>();
            if (fms != null && fms.defaultSpawn != null)
            {
                return fms.defaultSpawn.position.x < 0f;
            }

            // 3. Check robot's current or starting X coordinate
            return robot.transform.position.x < 0f;
        }

        public bool IsRobotOfOurAlliance(GameObject robot)
        {
            if (robot == null) return false;
            var robotRoot = GetRobotRoot(robot);
            if (robotRoot == null) robotRoot = robot;

            // If robot has an explicit alliance, match it
            if (robotRoot.name.IndexOf("Blue", StringComparison.OrdinalIgnoreCase) >= 0) return IsBlueAlliance;
            if (robotRoot.name.IndexOf("Red", StringComparison.OrdinalIgnoreCase) >= 0) return !IsBlueAlliance;

            // In single player practice (1 active robot), allow player to receive carrots from any depot
            var swerves = FindObjectsOfType<SwerveController>();
            if (swerves == null || swerves.Length <= 1)
            {
                return true;
            }

            bool robotIsBlue = IsRobotBlueAlliance(robotRoot);
            return robotIsBlue == IsBlueAlliance;
        }

        private bool IsRobotOfOurAlliance(Collider col)
        {
            if (col == null || col.isTrigger) return false;
            var robotRoot = GetRobotRoot(col.gameObject);
            return IsRobotOfOurAlliance(robotRoot);
        }
    }
}
