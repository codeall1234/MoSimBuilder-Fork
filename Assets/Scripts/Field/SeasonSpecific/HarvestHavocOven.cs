using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    /// <summary>
    /// Implements Blair Bunnybots 2026: Harvest Havoc OVEN and RAMP mechanics.
    /// 
    /// Two distinct ramps exist on the Oven/Ramp structure:
    /// 1. "Little Ramp": Inside the lower oven chute opening where carrots are placed/deposited.
    ///    Carrots roll down this small ramp (sloping down towards the back alliance wall at ~10 degrees,
    ///    surface height Y ~ 0.18m - 0.26m). Up to 3 carrots sit side-by-side on this ramp.
    /// 
    /// 2. "Big Ramp": Spanning the top of the entire oven structure (from Y ~ 1.25m down to field level).
    ///    When 3 carrots are placed in the oven, they are swapped out. 3 carrots are recirculated to
    ///    the opposing alliance depot, and 1 Carrot Cake is taken and rolled down this Big Ramp into the
    ///    Neutral Zone / Field.
    /// </summary>
    public class HarvestHavocOven : FieldScorer
    {
        [SerializeField] private int carrotPoints = 2;
        [SerializeField] private int carrotCakePoints = 1;
        [SerializeField] private GameObject carrotCakePrefab;
        [SerializeField] private Transform depotSpawnPoint;

        private readonly List<GamePiece> _scoredOvenPieces = new List<GamePiece>();
        private bool _isBaking;

        private static readonly List<HarvestHavocOven> _allOvens = new List<HarvestHavocOven>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            ResetAllOvens();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoDiscoverOvens()
        {
            EnsureAllOvens();
        }

        private static bool _ovensInitialized = false;

        public static void EnsureAllOvens()
        {
            _allOvens.RemoveAll(o => o == null);
            if (_allOvens.Count >= 2) return;
            if (_ovensInitialized && _allOvens.Count > 0) return;

            var redGo = GameObject.Find("mesh164_mesh") ?? GameObject.Find("RedOven");
            if (redGo != null)
            {
                var comp = redGo.GetComponent<HarvestHavocOven>();
                if (comp == null) comp = redGo.AddComponent<HarvestHavocOven>();
                if (!_allOvens.Contains(comp)) _allOvens.Add(comp);
            }

            var blueGo = GameObject.Find("mesh90_mesh") ?? GameObject.Find("BlueOven");
            if (blueGo != null)
            {
                var comp = blueGo.GetComponent<HarvestHavocOven>();
                if (comp == null) comp = blueGo.AddComponent<HarvestHavocOven>();
                if (!_allOvens.Contains(comp)) _allOvens.Add(comp);
            }

            if (_allOvens.Count > 0)
            {
                _ovensInitialized = true;
            }
        }

        public static void ResetAllOvens()
        {
            _ovensInitialized = false;
            foreach (var oven in _allOvens)
            {
                if (oven != null)
                {
                    oven._scoredOvenPieces.Clear();
                    oven._isBaking = false;
                }
            }
        }

        void OnEnable()
        {
            if (!_allOvens.Contains(this)) _allOvens.Add(this);
        }

        void OnDisable()
        {
            _allOvens.Remove(this);
        }

        public bool IsBlueAlliance
        {
            get
            {
                // World X position is the definitive truth for the field:
                // Red Alliance Farm is at negative X (X < 0)
                // Blue Alliance Farm is at positive X (X > 0)
                float worldX = transform.position.x;
                var ren = GetComponent<Renderer>();
                if (ren != null && Mathf.Abs(ren.bounds.center.x) > 0.1f)
                {
                    worldX = ren.bounds.center.x;
                }
                else
                {
                    var mf = GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        worldX = transform.TransformPoint(mf.sharedMesh.bounds.center).x;
                    }
                }
                if (Mathf.Abs(worldX) > 0.1f)
                {
                    return worldX > 0f;
                }
                return isBlue;
            }
        }

        private GameObject _triggerChild;

        void Start()
        {
            scorePieces = new PieceNames[] { PieceNames.Carrot, PieceNames.CarrotCake };
            scoreToAdd = carrotPoints;
            autoScoreToAdd = carrotPoints;

            isBlue = IsBlueAlliance;

            EnsureDepot();
            EnsureTriggerCollider();
        }

        private void EnsureDepot()
        {
            if (depotSpawnPoint == null)
            {
                bool blue = IsBlueAlliance;
                string name1 = blue ? "BlueDepot1" : "RedDepot1";
                string name2 = blue ? "BlueDepot2" : "RedDepot2";
                string name3 = blue ? "BlueDepot" : "RedDepot";
                var depotObj = GameObject.Find(name1) 
                            ?? GameObject.Find(name2) 
                            ?? GameObject.Find(name3) 
                            ?? GameObject.Find("Depot");
                if (depotObj != null)
                {
                    depotSpawnPoint = depotObj.transform;
                }
            }

            if (carrotCakePrefab == null)
            {
                carrotCakePrefab = Resources.Load<GameObject>("Pieces/CarrotCake");
            }
        }

        private void EnsureTriggerCollider()
        {
            // Disable conflicting colliders on the mesh itself to prevent scale warnings
            var existingBox = GetComponent<BoxCollider>();
            if (existingBox != null) existingBox.enabled = false;
            var mc = GetComponent<MeshCollider>();
            if (mc != null && mc.isTrigger) mc.enabled = false;

            bool blue = IsBlueAlliance;
            string triggerName = blue ? "BlueOvenChuteTrigger" : "RedOvenChuteTrigger";
            _triggerChild = GameObject.Find(triggerName);
            if (_triggerChild == null)
            {
                _triggerChild = new GameObject(triggerName);
            }

            // Position trigger at the chute entrance under the big ramp:
            // Red entrance: X = -7.30m, Y = 0.35m, Z = -1.739m
            // Blue entrance: X = 7.30m, Y = 0.35m, Z = 1.739m
            Vector3 worldCenter = blue 
                ? new Vector3(7.30f, 0.35f, 1.739f)
                : new Vector3(-7.30f, 0.35f, -1.739f);

            _triggerChild.transform.position = worldCenter;
            _triggerChild.transform.rotation = Quaternion.identity;
            _triggerChild.transform.localScale = Vector3.one;

            var box = _triggerChild.GetComponent<BoxCollider>();
            if (box == null) box = _triggerChild.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(0.70f, 0.50f, 0.70f);

            var proxy = _triggerChild.GetComponent<OvenTriggerProxy>();
            if (proxy == null) proxy = _triggerChild.AddComponent<OvenTriggerProxy>();
            proxy.oven = this;

            var boxScorer = _triggerChild.GetComponent<BoxScorer>();
            if (boxScorer == null) boxScorer = _triggerChild.AddComponent<BoxScorer>();
            boxScorer.isBlue = blue;
            boxScorer.cakeDepot = depotSpawnPoint;
            boxScorer.carrotCakePrefab = carrotCakePrefab;
            boxScorer.carrotPoints = carrotPoints;
            boxScorer.carrotCakePoints = carrotCakePoints;
        }

        public void IgnoreOvenCollisions(GamePiece piece)
        {
            if (piece == null) return;
            Transform ovenRoot = transform;
            while (ovenRoot.parent != null && 
                   !ovenRoot.parent.name.Contains("FieldHolder") && 
                   !ovenRoot.parent.name.Contains("GameManager") && 
                   !ovenRoot.parent.name.Contains("HarvestHavoc"))
            {
                ovenRoot = ovenRoot.parent;
            }

            var ovenColliders = ovenRoot.GetComponentsInChildren<Collider>(true);
            var pieceColliders = piece.GetComponentsInChildren<Collider>(true);
            foreach (var pCol in pieceColliders)
            {
                if (pCol == null || pCol.isTrigger) continue;
                foreach (var oCol in ovenColliders)
                {
                    if (oCol == null || oCol.isTrigger) continue;
                    Physics.IgnoreCollision(pCol, oCol, true);
                }
            }
        }

        /// <summary>
        /// Attempts to score a piece into the Oven.
        /// Carrots are placed on the LITTLE RAMP inside the oven chute opening.
        /// Pieces rolling down the Big Ramp on top (Y > 0.50m) are NOT scored!
        /// </summary>
        public bool TryScorePiece(GamePiece piece, out Vector3 targetPos, out Quaternion targetRot)
        {
            // If the piece is above Y = 0.50m, it is rolling down the big ramp on top of the oven.
            // It MUST NOT be scored into the lower oven chute!
            if (piece.transform.position.y > 0.50f)
            {
                targetPos = Vector3.zero;
                targetRot = Quaternion.identity;
                return false;
            }

            bool blue = IsBlueAlliance;

            // Carrot cylinder axis lies along Z (across chute width)
            // Roll tilted 10.0 degrees to match little ramp downward slope
            targetRot = Quaternion.Euler(0f, 0f, blue ? -10.0f : 10.0f);

            // Ignore collisions with solid chute walls so carrot sits quietly on the ramp
            IgnoreOvenCollisions(piece);

            // Calculated rest positions on the 10-degree sloped Little Ramp inside the chute:
            // Slot 0 (deepest against back): |X| = 8.05m, Y = 0.177m
            // Slot 1 (middle):               |X| = 7.82m, Y = 0.217m
            // Slot 2 (front near entrance):  |X| = 7.59m, Y = 0.258m
            float[] redXOffsets = { -8.05f, -7.82f, -7.59f };
            float[] blueXOffsets = { 8.05f, 7.82f, 7.59f };
            float[] yHeights = { 0.177f, 0.217f, 0.258f };
            float chuteZ = blue ? 1.739f : -1.739f;

            if (piece.pieceType == PieceNames.CarrotCake)
            {
                float cakeX = blue ? 7.82f : -7.82f;
                targetPos = new Vector3(cakeX, 0.217f, chuteZ);
                StartCoroutine(RecirculateCake(piece));
                return true;
            }

            // Clean destroyed pieces
            _scoredOvenPieces.RemoveAll(p => p == null);

            // If already scored in this oven, return existing position
            if (_scoredOvenPieces.Contains(piece))
            {
                int existingIdx = Mathf.Clamp(_scoredOvenPieces.IndexOf(piece), 0, 2);
                float existX = blue ? blueXOffsets[existingIdx] : redXOffsets[existingIdx];
                float existY = yHeights[existingIdx];
                targetPos = new Vector3(existX, existY, chuteZ);
                return true;
            }

            _scoredOvenPieces.Add(piece);
            int count = _scoredOvenPieces.Count;

            int slotIdx = Mathf.Clamp(count - 1, 0, 2);
            float targetX = blue ? blueXOffsets[slotIdx] : redXOffsets[slotIdx];
            float targetY = yHeights[slotIdx];
            targetPos = new Vector3(targetX, targetY, chuteZ);

            if (count >= 3 && !_isBaking)
            {
                StartCoroutine(BakeCarrots());
            }

            return true;
        }

        private IEnumerator RecirculateCake(GamePiece cake)
        {
            yield return new WaitForSeconds(0.8f);
            if (cake != null)
            {
                Destroy(cake.gameObject);
            }
            // Reintroduce down the alliance's BIG RAMP into the Neutral Zone
            RollCakeDownRamp();
        }

        /// <summary>
        /// Recirculation logic per official manual page 21:
        /// When 3 carrots are placed into the oven, they are swapped out:
        /// 1. The 3 carrots are destroyed from the oven chute.
        /// 2. 3 carrots are added to the OPPOSING ALLIANCE'S DEPOT inventory.
        /// 3. 1 Carrot Cake is taken from the opposing depot stock.
        /// 4. The Carrot Cake is rolled down THIS ALLIANCE'S BIG RAMP into the Neutral Zone!
        /// </summary>
        private IEnumerator BakeCarrots()
        {
            _isBaking = true;
            yield return new WaitForSeconds(0.6f);

            _scoredOvenPieces.RemoveAll(p => p == null);

            // Destroy the 3 carrots in the oven
            int toDestroy = Mathf.Min(3, _scoredOvenPieces.Count);
            for (int i = 0; i < toDestroy; i++)
            {
                if (_scoredOvenPieces[i] != null)
                {
                    Destroy(_scoredOvenPieces[i].gameObject);
                }
            }
            _scoredOvenPieces.RemoveRange(0, toDestroy);

            // Carrots recirculate to opposing depot inventory
            bool opposingIsBlue = !IsBlueAlliance;
            HarvestHavocDepot.AddRecirculatedCarrots(opposingIsBlue, 3);

            // Trade for 1 Carrot Cake from opposing depot reserve (starts with 9 cakes)
            bool cakeAvailable = HarvestHavocDepot.TryTradeCarrotCake(opposingIsBlue);

            if (cakeAvailable)
            {
                // Roll the received Carrot Cake down THIS ALLIANCE'S BIG RAMP on top of the oven
                RollCakeDownRamp();
            }
            else
            {
                Debug.Log($"[HarvestHavocOven] {(IsBlueAlliance ? "Blue" : "Red")} Alliance 3 carrots handed over - opposing depot has no cakes left!");
            }

            _isBaking = false;
        }

        /// <summary>
        /// Rolls a Carrot Cake down this alliance's BIG RAMP (on top of the oven) into the Neutral Zone,
        /// satisfying official manual rules G23 & G24.
        /// 
        /// Spawns at the very beginning of the ramp near the alliance wall and rolls physically
        /// ON TOP OF the ramp into the field.
        /// </summary>
        public void RollCakeDownRamp()
        {
            if (carrotCakePrefab == null)
            {
                carrotCakePrefab = Resources.Load<GameObject>("Pieces/CarrotCake");
            }
            if (carrotCakePrefab == null)
            {
                Debug.LogError("[HarvestHavocOven] Failed to load CarrotCake prefab from Resources/Pieces/CarrotCake!");
                return;
            }

            bool blue = IsBlueAlliance;

            // Spawns at the top of the Big Ramp near the alliance wall:
            // Red: X = -8.20m, Y = 1.32m, Z = -1.739m
            // Blue: X = 8.20m, Y = 1.32m, Z = 1.739m
            Vector3 rampTopPos = blue
                ? new Vector3(8.20f, 1.32f, 1.739f)
                : new Vector3(-8.20f, 1.32f, -1.739f);

            // Roll direction: downwards and towards the field center / neutral zone
            // Red (at -X) rolls towards +X; Blue (at +X) rolls towards -X
            Vector3 rollDir = blue
                ? new Vector3(-1f, -0.27f, 0f).normalized
                : new Vector3(1f, -0.27f, 0f).normalized;

            Quaternion rampRot = blue
                ? Quaternion.Euler(0f, 0f, 15.2f)
                : Quaternion.Euler(0f, 0f, -15.2f);

            GameObject cakeObj = Instantiate(carrotCakePrefab, rampTopPos, rampRot);
            var piece = cakeObj.GetComponent<GamePiece>();
            if (piece != null)
            {
                piece.state = GamePieceState.World;
            }

            // Mark as rolling so BoxScorer will NEVER score this cake
            cakeObj.AddComponent<RollingCarrotCake>();

            // Apply forward & downward rolling velocity down the ramp towards the neutral zone
            var rb = cakeObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.velocity = rollDir * 2.5f;
                rb.angularVelocity = new Vector3(0f, 0f, (blue ? 1f : -1f) * 6f);
            }

            Debug.Log($"[HarvestHavocOven] {(blue ? "Blue" : "Red")} Alliance rolled Carrot Cake down Big Ramp into Neutral Zone!");
        }

        public class OvenTriggerProxy : MonoBehaviour
        {
            public HarvestHavocOven oven;
        }

        public class RollingCarrotCake : MonoBehaviour
        {
            private float _spawnTime;

            void Awake()
            {
                _spawnTime = Time.time;
            }

            void Update()
            {
                // After 8 seconds, the cake has arrived in the Neutral Zone
                if (Time.time > _spawnTime + 8f)
                {
                    Destroy(this);
                }
            }
        }

        private void OnDestroy()
        {
            _scoredOvenPieces.Clear();
            _isBaking = false;
        }

        void FixedUpdate()
        {
            // Scoring handled upon entry. Baking handled via coroutines.
        }
    }
}
