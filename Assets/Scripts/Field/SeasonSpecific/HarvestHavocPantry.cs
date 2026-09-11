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
                targetRot = Quaternion.identity;

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
                targetRot = Quaternion.identity;
                return true;
            }
        }

        private static readonly Dictionary<int, PantryLevelState> _levelStates = new Dictionary<int, PantryLevelState>();

        public static void ResetAllSlots()
        {
            _levelStates.Clear();
        }

        public int LevelIndex
        {
            get
            {
                if (carrotPoints == 3 || transform.position.y < 1.0f) return 1;
                if (carrotPoints == 4 || transform.position.y < 1.35f) return 2;
                return 3;
            }
        }

        public bool BlueAlliance
        {
            get
            {
                return isBlue || transform.position.x > 0f;
            }
        }

        public int LevelKey => (BlueAlliance ? 10 : 0) + LevelIndex;

        void Start()
        {
            // Initialize required FieldScorer variables
            scorePieces = new PieceNames[] { PieceNames.Carrot, PieceNames.CarrotCake };
            scoreToAdd = 2;
            autoScoreToAdd = 2;

            if (transform.position.x > 0f) isBlue = true;

            // Ensure a trigger box exists for automatic scoring
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;

            // Enlarge trigger height/depth slightly so incoming pieces reliably trigger the shelf
            Vector3 size = box.size;
            Vector3 center = box.center;
            if (size.y < 0.25f)
            {
                center.y += (0.28f - size.y) * 0.5f;
                size.y = 0.28f;
            }
            if (size.z < 0.25f)
            {
                center.z += (0.25f - size.z) * 0.5f;
                size.z = 0.25f;
            }
            box.size = size;
            box.center = center;

            var boxScorer = gameObject.GetComponent<BoxScorer>();
            if (boxScorer == null) boxScorer = gameObject.AddComponent<BoxScorer>();
            boxScorer.isBlue = BlueAlliance;
            boxScorer.cakeDepot = depotSpawnPoint;
            boxScorer.carrotCakePrefab = carrotCakePrefab;
            boxScorer.carrotPoints = carrotPoints;
            boxScorer.carrotCakePoints = carrotCakePoints;

            // Register level state and slot positions
            RegisterLevelSlots();
        }

        private void RegisterLevelSlots()
        {
            int key = LevelKey;
            if (!_levelStates.TryGetValue(key, out var state))
            {
                state = new PantryLevelState
                {
                    allianceKey = BlueAlliance ? 1 : 0,
                    levelIndex = LevelIndex
                };
                _levelStates[key] = state;
            }

            // Determine bounds of the shelf surface
            var col = GetComponent<Collider>();
            var ren = GetComponent<Renderer>();
            Bounds bounds = ren != null ? ren.bounds : (col != null ? col.bounds : new Bounds(transform.position, Vector3.one));

            bool isShelfPlate = bounds.size.z > 0.05f;

            float shelfSurfaceY;
            float shelfCenterZ;
            if (isShelfPlate)
            {
                shelfSurfaceY = bounds.max.y + 0.02f;
                shelfCenterZ = bounds.center.z;
            }
            else
            {
                // Fallback / backboard: use measured shelf surface heights
                if (LevelIndex == 1) shelfSurfaceY = 0.850f;
                else if (LevelIndex == 2) shelfSurfaceY = 1.206f;
                else shelfSurfaceY = 1.561f;

                shelfCenterZ = -4.037f;
            }

            float minX = bounds.min.x;
            float maxX = bounds.max.x;
            if (Mathf.Abs(maxX - minX) < 1.0f)
            {
                // Fallback default shelf X range
                if (BlueAlliance) { minX = 5.540f; maxX = 7.928f; }
                else { minX = -7.928f; maxX = -5.540f; }
            }

            float span = maxX - minX;
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float x = minX + t * span;
                state.slotPositions[i] = new Vector3(x, shelfSurfaceY, shelfCenterZ);
            }
        }

        public bool TryScorePiece(GamePiece piece, out Vector3 targetPos, out Quaternion targetRot)
        {
            targetPos = Vector3.zero;
            targetRot = Quaternion.identity;

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
        }

        void FixedUpdate()
        {
            return;
        }
    }
}
