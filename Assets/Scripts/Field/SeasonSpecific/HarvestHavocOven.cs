using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    public class HarvestHavocOven : FieldScorer
    {
        [SerializeField] private int carrotPoints = 2;
        [SerializeField] private int carrotCakePoints = 1;
        [SerializeField] private GameObject carrotCakePrefab;
        [SerializeField] private Transform depotSpawnPoint;

        private int scoredCount;
        private int storedCarrots;
        private readonly List<GamePiece> _scoredOvenPieces = new List<GamePiece>();
        private bool _isBaking;

        private static readonly List<HarvestHavocOven> _allOvens = new List<HarvestHavocOven>();

        public static void ResetAllOvens()
        {
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
                if (isBlue) return true;
                Transform cur = transform;
                while (cur != null)
                {
                    if (cur.name.IndexOf("Blue", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (cur.name.IndexOf("Red", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                    cur = cur.parent;
                }
                return GetOvenBounds().center.x > 0f;
            }
        }

        void Start()
        {
            // Initialize required FieldScorer variables
            scorePieces = new PieceNames[] { PieceNames.Carrot, PieceNames.CarrotCake };
            scoreToAdd = 2;
            autoScoreToAdd = 2;

            isBlue = IsBlueAlliance;

            EnsureDepot();
            EnsureTriggerCollider();

            var boxScorer = gameObject.GetComponent<BoxScorer>();
            if (boxScorer == null) boxScorer = gameObject.AddComponent<BoxScorer>();
            boxScorer.isBlue = isBlue;
            boxScorer.cakeDepot = depotSpawnPoint;
            boxScorer.carrotCakePrefab = carrotCakePrefab;
            boxScorer.carrotPoints = carrotPoints;
            boxScorer.carrotCakePoints = carrotCakePoints;
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
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;

            // Disable any conflicting MeshCollider trigger
            var mc = GetComponent<MeshCollider>();
            if (mc != null && mc.isTrigger)
            {
                mc.enabled = false;
            }

            Bounds ovenBounds = GetOvenBounds();
            Vector3 center = ovenBounds.center;
            float wallDir = IsBlueAlliance ? 1f : -1f;

            // Oven entrance is on the field side of the oven (-wallDir * 0.15m from center)
            // Chute opening trigger lowered to Y = 0.52m
            Vector3 worldCenter = new Vector3(
                center.x - wallDir * 0.15f,
                0.52f,
                center.z
            );
            Vector3 worldSize = new Vector3(0.85f, 0.75f, 0.75f);

            box.center = transform.InverseTransformPoint(worldCenter);
            Vector3 localSize = transform.InverseTransformVector(worldSize);
            box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        public Bounds GetOvenBounds()
        {
            Transform ovenRoot = transform;
            while (ovenRoot.parent != null && 
                   !ovenRoot.parent.name.Contains("FieldHolder") && 
                   !ovenRoot.parent.name.Contains("GameManager") && 
                   !ovenRoot.parent.name.Contains("HarvestHavoc"))
            {
                ovenRoot = ovenRoot.parent;
            }

            var renderers = ovenRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }
                return b;
            }
            return new Bounds(transform.position, new Vector3(0.6f, 0.4f, 0.6f));
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

        public bool TryScorePiece(GamePiece piece, out Vector3 targetPos, out Quaternion targetRot)
        {
            float wallDir = IsBlueAlliance ? 1f : -1f;
            float yaw = IsBlueAlliance ? 90f : -90f;
            // The oven chute is slanted upward towards the alliance wall at 15 degrees
            targetRot = Quaternion.Euler(-15f, yaw, 0f);

            // Ignore collisions with solid parts of the oven so piece enters freely
            IgnoreOvenCollisions(piece);

            Bounds ovenBounds = GetOvenBounds();
            Vector3 center = ovenBounds.center;

            // Position carrots on the 15-degree ramp inside the oven (lowered by ~1 foot to 0.425m)
            float depthX = center.x + wallDir * 0.20f;
            float floorY = 0.425f;

            if (piece.pieceType == PieceNames.CarrotCake)
            {
                // Carrot cake placed into the oven: recirculate immediately
                targetPos = new Vector3(depthX, floorY, center.z);
                StartCoroutine(RecirculateCake(piece));
                return true;
            }

            // Remove any destroyed or null pieces from previous batches
            _scoredOvenPieces.RemoveAll(p => p == null);

            // If already in oven, return existing assigned position
            if (_scoredOvenPieces.Contains(piece))
            {
                int existingIdx = _scoredOvenPieces.IndexOf(piece);
                float existingOffset = (existingIdx - 1) * 0.10f;
                targetPos = new Vector3(depthX, floorY, center.z + existingOffset);
                return true;
            }

            _scoredOvenPieces.Add(piece);
            int count = _scoredOvenPieces.Count;

            // Arrange carrots neatly side by side along the oven width axis (Z)
            float offset = (count - 2) * 0.10f;
            targetPos = new Vector3(depthX, floorY, center.z + offset);

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
            // Carrot Cake scored into oven is reintroduced down the alliance's ramp into the Neutral Zone
            RollCakeDownRamp();
        }

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

            // Recirculation per manual page 21:
            // Human Player exchanges 3 oven-scored carrots for 1 Carrot Cake stored at the OPPOSING ALLIANCE'S DEPOT
            bool opposingIsBlue = !IsBlueAlliance;
            HarvestHavocDepot.AddRecirculatedCarrots(opposingIsBlue, 3);

            // Try to trade for 1 Carrot Cake from the opposing depot's stock of 9 cakes
            bool cakeAvailable = HarvestHavocDepot.TryTradeCarrotCake(opposingIsBlue);

            if (cakeAvailable)
            {
                // Roll the received Carrot Cake down THIS ALLIANCE'S RAMP into the Neutral Zone!
                RollCakeDownRamp();
            }
            else
            {
                Debug.Log($"[HarvestHavocOven] {(IsBlueAlliance ? "Blue" : "Red")} Alliance 3 carrots handed over for free - opposing depot has no cakes left!");
            }

            _isBaking = false;
        }

        /// <summary>
        /// Rolls a Carrot Cake down this alliance's Ramp into the Neutral Zone,
        /// satisfying official manual rules G23 & G24.
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

            Bounds ovenBounds = GetOvenBounds();
            Vector3 center = ovenBounds.center;
            float wallDir = IsBlueAlliance ? 1f : -1f;

            // The ramp is on top of the oven structure.
            // Top entrance of the ramp is on the alliance wall side (+wallDir * 0.40m from center)
            // Height at top of ramp is Y ~ 1.15m.
            Vector3 rampTopPos = new Vector3(
                center.x + wallDir * 0.40f,
                1.15f,
                center.z
            );

            // Roll direction: downwards and towards the field center / neutral zone (-wallDir)
            Vector3 rollDir = new Vector3(-wallDir, -0.27f, 0f).normalized;

            // Orient the carrot cake horizontally across the ramp width (cylinder axis along Z)
            Quaternion rampRot = Quaternion.Euler(-15f, IsBlueAlliance ? -90f : 90f, 0f);

            GameObject cakeObj = Instantiate(carrotCakePrefab, rampTopPos, rampRot);
            var piece = cakeObj.GetComponent<GamePiece>();
            if (piece != null)
            {
                piece.state = GamePieceState.World;
            }

            // Ignore collisions with oven solid colliders so cake rolls smoothly down the ramp
            IgnoreOvenCollisions(piece);

            // Apply forward & downward rolling velocity down the ramp towards the neutral zone
            var rb = cakeObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = rollDir * 2.2f;
                rb.angularVelocity = new Vector3(0f, 0f, wallDir * 6f);
            }

            Debug.Log($"[HarvestHavocOven] {(IsBlueAlliance ? "Blue" : "Red")} Alliance rolled Carrot Cake down Ramp into Neutral Zone!");
        }

        private void OnDestroy()
        {
            _scoredOvenPieces.Clear();
            _isBaking = false;
        }

        void FixedUpdate()
        {
            // Scoring is handled when pieces enter. Baking is managed via coroutines.
        }
    }
}
