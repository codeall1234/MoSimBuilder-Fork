using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    public class HarvestHavocPantry : FieldScorer
    {
        [SerializeField] private int level = 1;
        [SerializeField] private int carrotPoints = 3;
        [SerializeField] private int carrotCakePoints = 8;
        [SerializeField] private Transform depotSpawnPoint;
        [SerializeField] private GameObject carrotCakePrefab;

        public int LevelIndex
        {
            get => level;
            set => level = value;
        }

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
                // Carrots lie horizontally across the shelf (cylinder axis along X, length ~0.15m, radius 0.038m)
                targetRot = Quaternion.Euler(0f, 90f, 0f);

                // If already scored in this shelf level, return the existing slot
                for (int i = 0; i < 5; i++)
                {
                    if (occupiedPieces[i] == piece)
                    {
                        targetPos = slotPositions[i];
                        return true;
                    }
                }

                // If already at capacity (5 pieces), reject any more pieces on this level
                if (OccupiedCount >= 5)
                {
                    return false;
                }

                // Find the unoccupied slot closest in X position to the piece
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
                return true;
            }
        }

        private static readonly Dictionary<int, PantryLevelState> _levelStates = new Dictionary<int, PantryLevelState>();
        private static readonly List<HarvestHavocPantry> _allPantries = new List<HarvestHavocPantry>();
        private static bool _pantriesInitialized = false;

        public bool BlueAlliance => isBlue;

        public int LevelKey => (BlueAlliance ? 10 : 0) + LevelIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            ResetAllSlots();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoDiscoverPantries()
        {
            EnsureAllPantries();
        }

        public static void ResetAllSlots()
        {
            _levelStates.Clear();
            _pantriesInitialized = false;
            foreach (var p in _allPantries)
            {
                if (p != null)
                {
                    p.RegisterLevelSlots();
                }
            }
        }

        public static void EnsureAllPantries()
        {
            _allPantries.RemoveAll(p => p == null);
            if (_pantriesInitialized && _allPantries.Count >= 6) return;

            // Ensure triggers for both Red Alliance (X < 0) and Blue Alliance (X > 0)
            EnsureAlliancePantries(false); // Red
            EnsureAlliancePantries(true);  // Blue

            _pantriesInitialized = true;
        }

        private static void EnsureAlliancePantries(bool isBlueAlliance)
        {
            string prefix = isBlueAlliance ? "BluePantry" : "RedPantry";
            float minX = isBlueAlliance ? 5.540f : -7.928f;
            float maxX = isBlueAlliance ? 7.928f : -5.540f;
            float centerX = (minX + maxX) * 0.5f;
            float sizeX = maxX - minX;

            // Z: shelf back is -4.20m, front lip is -3.88m, center is -4.037m
            float centerZ = -3.95f;
            float sizeZ = 0.60f;

            // 3 levels:
            // Level 1: surface Y = 0.830m, points: 3 / 8
            // Level 2: surface Y = 1.186m, points: 4 / 10
            // Level 3: surface Y = 1.541m, points: 5 / 12
            int[] levels = { 1, 2, 3 };
            int[] carrotPts = { 3, 4, 5 };
            int[] cakePts = { 8, 10, 12 };
            float[] triggerCenterYs = { 0.95f, 1.30f, 1.68f };
            float[] triggerSizes = { 0.30f, 0.30f, 0.40f };

            for (int i = 0; i < 3; i++)
            {
                int lvl = levels[i];
                string triggerName = $"{prefix}_L{lvl}_Trigger";
                var go = GameObject.Find(triggerName);
                if (go == null)
                {
                    go = new GameObject(triggerName);
                }

                go.transform.position = new Vector3(centerX, triggerCenterYs[i], centerZ);
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                var box = go.GetComponent<BoxCollider>();
                if (box == null) box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = new Vector3(sizeX, triggerSizes[i], sizeZ);

                var pantry = go.GetComponent<HarvestHavocPantry>();
                if (pantry == null) pantry = go.AddComponent<HarvestHavocPantry>();
                pantry.level = lvl;
                pantry.isBlue = isBlueAlliance;
                pantry.carrotPoints = carrotPts[i];
                pantry.carrotCakePoints = cakePts[i];
                pantry.scorePieces = new PieceNames[] { PieceNames.Carrot, PieceNames.CarrotCake };
                pantry.scoreToAdd = carrotPts[i];
                pantry.autoScoreToAdd = carrotPts[i];

                var boxScorer = go.GetComponent<BoxScorer>();
                if (boxScorer == null) boxScorer = go.AddComponent<BoxScorer>();
                boxScorer.isBlue = isBlueAlliance;
                boxScorer.carrotPoints = carrotPts[i];
                boxScorer.carrotCakePoints = cakePts[i];

                pantry.RegisterLevelSlots();

                if (!_allPantries.Contains(pantry))
                {
                    _allPantries.Add(pantry);
                }
            }
        }

        void OnEnable()
        {
            if (!_allPantries.Contains(this)) _allPantries.Add(this);
        }

        void OnDisable()
        {
            _allPantries.Remove(this);
        }

        void Start()
        {
            scorePieces = new PieceNames[] { PieceNames.Carrot, PieceNames.CarrotCake };
            scoreToAdd = carrotPoints;
            autoScoreToAdd = carrotPoints;

            RegisterLevelSlots();
        }

        public void RegisterLevelSlots()
        {
            int key = LevelKey;
            int lvl = LevelIndex;
            bool isBlueAlliance = isBlue;

            if (!_levelStates.TryGetValue(key, out var state))
            {
                state = new PantryLevelState
                {
                    allianceKey = isBlueAlliance ? 1 : 0,
                    levelIndex = lvl
                };
                _levelStates[key] = state;
            }

            // Shelf surface heights in world coordinates
            float shelfSurfaceY;
            if (lvl == 1) shelfSurfaceY = 0.830f;
            else if (lvl == 2) shelfSurfaceY = 1.186f;
            else shelfSurfaceY = 1.541f;

            float shelfCenterZ = -4.037f;
            float carrotRadius = 0.038f;

            // X range for 5 slots
            float minX = isBlueAlliance ? 5.540f : -7.928f;
            float maxX = isBlueAlliance ? 7.928f : -5.540f;
            float span = maxX - minX;

            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;
                float x = minX + t * span;
                state.slotPositions[i] = new Vector3(x, shelfSurfaceY + carrotRadius, shelfCenterZ);
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
    }
}
