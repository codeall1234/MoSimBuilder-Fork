using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    public class HarvestHavocPantry : FieldScorer
    {
        [SerializeField] private int carrotPoints = 5;
        [SerializeField] private int carrotCakePoints = 8;
        [SerializeField] private Transform depotSpawnPoint;
        [SerializeField] private GameObject carrotCakePrefab;

        public class PantryLevelState
        {
            public int allianceKey; // 0 = Red, 1 = Blue
            public int levelIndex;  // 1, 2, 3
            public Vector3[] slotPositions = new Vector3[5];
            public GamePiece[] occupiedPieces = new GamePiece[5];

            public int OccupiedCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        if (occupiedPieces[i] != null) count++;
                    }
                    return count;
                }
            }

            public bool TryScore(GamePiece piece, Vector3 piecePos, out Vector3 targetPos, out Quaternion targetRot)
            {
                targetPos = Vector3.zero;
                targetRot = Quaternion.Euler(0f, 90f, 0f);

                // If already scored in this shelf level, return the existing slot
                for (int i = 0; i < 5; i++)
                {
                    if (occupiedPieces[i] == piece)
                    {
                        targetPos = slotPositions[i];
                        targetRot = Quaternion.Euler(0f, 90f, 0f);
                        return true;
                    }
                }

                // If already at capacity (5 pieces), reject any more pieces
                if (OccupiedCount >= 5)
                {
                    return false;
                }

                // Find the unoccupied slot closest in X position
                int bestSlot = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    if (occupiedPieces[i] == null)
                    {
                        float d = Mathf.Abs(slotPositions[i].x - piecePos.x);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestSlot = i;
                        }
                    }
                }

                if (bestSlot == -1)
                {
                    return false;
                }

                occupiedPieces[bestSlot] = piece;
                targetPos = slotPositions[bestSlot];
                targetRot = Quaternion.Euler(0f, 90f, 0f);
                return true;
            }
        }

        private static readonly Dictionary<int, PantryLevelState> _levelStates = new Dictionary<int, PantryLevelState>();

        public int LevelIndex => DetermineLevelIndex();

        private int DetermineLevelIndex()
        {
            // 1. By mesh name or GameObject name
            string n = gameObject.name;
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                n += " " + mf.sharedMesh.name;
            }

            if (n.Contains("12") || n.Contains("119")) return 3;
            if (n.Contains("30") || n.Contains("95")) return 2;
            if (n.Contains("150") || n.Contains("174")) return 1;

            // 2. By serialized point values (Level 1 = 3pts, Level 2 = 4pts, Level 3 = 5pts)
            if (carrotPoints == 5 || carrotCakePoints == 12) return 3;
            if (carrotPoints == 4 || carrotCakePoints == 10) return 2;
            if (carrotPoints == 3 || carrotCakePoints == 8) return 1;

            // 3. By world geometry center Y (using mesh bounds transformed to world space)
            float worldY = GetBoundsCenterY();
            if (worldY > 1.35f) return 3;
            if (worldY > 1.0f) return 2;

            return 1;
        }

        public bool BlueAlliance => DetermineBlueAlliance();

        private bool DetermineBlueAlliance()
        {
            if (isBlue) return true;
            float worldX = GetBoundsCenterX();
            if (Mathf.Abs(worldX) > 0.1f) return worldX > 0f;
            return transform.position.x > 0f;
        }

        private float GetBoundsCenterY()
        {
            var ren = GetComponent<Renderer>();
            if (ren != null && ren.bounds.size.y > 0.01f) return ren.bounds.center.y;
            var col = GetComponent<Collider>();
            if (col != null && col.bounds.size.y > 0.01f) return col.bounds.center.y;
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                return transform.TransformPoint(mf.sharedMesh.bounds.center).y;
            }
            return transform.position.y;
        }

        private float GetBoundsCenterX()
        {
            var ren = GetComponent<Renderer>();
            if (ren != null && ren.bounds.size.x > 0.01f) return ren.bounds.center.x;
            var col = GetComponent<Collider>();
            if (col != null && col.bounds.size.x > 0.01f) return col.bounds.center.x;
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                return transform.TransformPoint(mf.sharedMesh.bounds.center).x;
            }
            return transform.position.x;
        }

        public int LevelKey => (BlueAlliance ? 10 : 0) + LevelIndex;

        private static readonly HashSet<int> _activeLevelTriggers = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            _levelStates.Clear();
            _activeLevelTriggers.Clear();
        }

        public static void ResetAllSlots()
        {
            _levelStates.Clear();
            _activeLevelTriggers.Clear();
        }

        void Start()
        {
            // Initialize required FieldScorer variables
            scorePieces = new PieceNames[] { PieceNames.Carrot, PieceNames.CarrotCake };
            scoreToAdd = 2;
            autoScoreToAdd = 2;

            if (BlueAlliance) isBlue = true;

            RegisterLevelSlots();

            int key = LevelKey;
            if (_activeLevelTriggers.Contains(key))
            {
                // A primary trigger and BoxScorer already exist for this shelf level on this alliance.
                // Disable colliders on this duplicate GameObject so they do not produce redundant trigger events.
                var col = GetComponent<Collider>();
                if (col != null && col.isTrigger) col.enabled = false;
                var existingScorer = GetComponent<BoxScorer>();
                if (existingScorer != null) existingScorer.enabled = false;
                return;
            }
            _activeLevelTriggers.Add(key);

            EnsureTriggerCollider();

            var boxScorer = gameObject.GetComponent<BoxScorer>();
            if (boxScorer == null) boxScorer = gameObject.AddComponent<BoxScorer>();
            boxScorer.isBlue = BlueAlliance;
            boxScorer.cakeDepot = depotSpawnPoint;
            boxScorer.carrotCakePrefab = carrotCakePrefab;
            boxScorer.carrotPoints = carrotPoints;
            boxScorer.carrotCakePoints = carrotCakePoints;
        }

        private void EnsureTriggerCollider()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;

            // Disable any conflicting MeshCollider trigger on the shelf mesh
            var mc = GetComponent<MeshCollider>();
            if (mc != null && mc.isTrigger)
            {
                mc.enabled = false;
            }

            int level = LevelIndex;
            bool isBlue = BlueAlliance;

            // X span across all 5 scoring slots
            float minX = isBlue ? 5.35f : -8.10f;
            float maxX = isBlue ? 8.10f : -5.35f;
            float centerX = (minX + maxX) * 0.5f;
            float sizeX = maxX - minX;

            // Y span for this shelf level (disjoint generous vertical zones for L1, L2, L3)
            float surfaceY;
            if (level == 1) surfaceY = 0.850f;
            else if (level == 2) surfaceY = 1.206f;
            else surfaceY = 1.561f;

            float centerY;
            float sizeY;
            if (level == 1)
            {
                centerY = surfaceY + 0.10f;
                sizeY = 0.45f;
            }
            else if (level == 2)
            {
                centerY = surfaceY + 0.14f;
                sizeY = 0.32f;
            }
            else
            {
                centerY = surfaceY + 0.25f;
                sizeY = 0.50f;
            }

            // Depth in Z: shelf back is ~ -4.20m, front lip is ~ -3.88m.
            // We set backZ = -4.20m, frontZ = -3.65m.
            // This provides 0.55m of depth, covering the shelf and the immediate drop zone,
            // without intruding into the robot drive lane (Z > -3.5m).
            float backZ = -4.20f;
            float frontZ = -3.65f;
            float centerZ = (backZ + frontZ) * 0.5f;
            float sizeZ = frontZ - backZ;

            Vector3 worldCenter = new Vector3(centerX, centerY, centerZ);
            Vector3 worldSize = new Vector3(sizeX, sizeY, sizeZ);

            // Convert world-space box to local transform space
            box.center = transform.InverseTransformPoint(worldCenter);
            Vector3 localSize = transform.InverseTransformVector(worldSize);
            box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        private void RegisterLevelSlots()
        {
            int key = LevelKey;
            int level = LevelIndex;
            bool isBlueAlliance = BlueAlliance;

            if (!_levelStates.TryGetValue(key, out var state))
            {
                state = new PantryLevelState
                {
                    allianceKey = isBlueAlliance ? 1 : 0,
                    levelIndex = level
                };
                _levelStates[key] = state;
            }

            // Expected shelf surface heights in world coordinates
            float shelfSurfaceY;
            if (level == 1) shelfSurfaceY = 0.850f;
            else if (level == 2) shelfSurfaceY = 1.206f;
            else shelfSurfaceY = 1.561f;

            float shelfCenterZ = -4.037f;

            // X range for 5 slots
            float minX, maxX;
            if (isBlueAlliance)
            {
                minX = 5.540f;
                maxX = 7.928f;
            }
            else
            {
                minX = -7.928f;
                maxX = -5.540f;
            }

            float span = maxX - minX;
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float x = minX + t * span;
                state.slotPositions[i] = new Vector3(x, shelfSurfaceY + 0.04f, shelfCenterZ);
            }
        }

        public bool TryScorePiece(GamePiece piece, out Vector3 targetPos, out Quaternion targetRot)
        {
            targetPos = Vector3.zero;
            targetRot = Quaternion.Euler(0f, 90f, 0f);

            int key = LevelKey;
            if (!_levelStates.TryGetValue(key, out var state))
            {
                RegisterLevelSlots();
                state = _levelStates[key];
            }

            return state.TryScore(piece, piece.transform.position, out targetPos, out targetRot);
        }

        private void OnDestroy()
        {
            _levelStates.Clear();
            _activeLevelTriggers.Clear();
        }

        void FixedUpdate()
        {
            return;
        }
    }
}
