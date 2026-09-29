using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    /// <summary>
    /// Implements Blair Bunnybots 2026: Harvest Havoc ENDGAME scoring:
    /// Official Game Manual Rules (Pages 10, 15-18):
    /// - PARKING: A robot whose bumpers are fully or partially within the alliance's
    ///   TABLE ZONE at the end of the match has PARKED (2 points).
    /// - HARVEST HAUL: Up to 3 additional carrots (1 point each) or carrot cakes (3 points each)
    ///   held in a robot while PARKED grant additional endgame points.
    /// - Red Table Zone: Red side of the Dinner Table (X < 0, adjacent to Dinner Table).
    /// - Blue Table Zone: Blue side of the Dinner Table (X > 0, adjacent to Dinner Table).
    /// </summary>
    public class HarvestHavocEndgame : MonoBehaviour
    {
        [SerializeField] public bool isBlue;
        
        private bool hasScored = false;
        private static readonly List<HarvestHavocEndgame> _allEndgames = new List<HarvestHavocEndgame>();
        private static bool _endgamesInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            ResetEndgame();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoDiscoverEndgames()
        {
            EnsureAllEndgameScorers();
        }

        public static void EnsureAllEndgameScorers()
        {
            _allEndgames.RemoveAll(e => e == null);
            if (_endgamesInitialized && _allEndgames.Count >= 2) return;

            EnsureAllianceScorer(false); // Red Table Zone
            EnsureAllianceScorer(true);  // Blue Table Zone

            _endgamesInitialized = true;
        }

        private static void EnsureAllianceScorer(bool isBlueAlliance)
        {
            string objName = isBlueAlliance ? "BlueTableZoneScorer" : "RedTableZoneScorer";
            var go = GameObject.Find(objName);
            if (go == null)
            {
                go = new GameObject(objName);
            }

            // Table zone is adjacent to the Dinner Table (center at X=0, Z=0, size 1.2m x 1.2m):
            // Red Zone: X in [-2.4m, -0.2m], Z in [-1.5m, 1.5m], Y in [0.0, 0.8m]
            // Blue Zone: X in [0.2m, 2.4m], Z in [-1.5m, 1.5m], Y in [0.0, 0.8m]
            Vector3 center = isBlueAlliance 
                ? new Vector3(1.30f, 0.40f, 0.0f) 
                : new Vector3(-1.30f, 0.40f, 0.0f);
            Vector3 size = new Vector3(2.20f, 0.80f, 3.0f);

            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;

            var scorer = go.GetComponent<HarvestHavocEndgame>();
            if (scorer == null) scorer = go.AddComponent<HarvestHavocEndgame>();
            scorer.isBlue = isBlueAlliance;

            if (!_allEndgames.Contains(scorer))
            {
                _allEndgames.Add(scorer);
            }
        }

        public static void ResetEndgame()
        {
            _endgamesInitialized = false;
            foreach (var e in _allEndgames)
            {
                if (e != null)
                {
                    e.hasScored = false;
                    e.StopAllCoroutines();
                }
            }
        }

        void OnEnable()
        {
            if (!_allEndgames.Contains(this)) _allEndgames.Add(this);
        }

        void OnDisable()
        {
            _allEndgames.Remove(this);
        }

        public Bounds GetTableZoneBounds()
        {
            Vector3 center = isBlue 
                ? new Vector3(1.30f, 0.40f, 0.0f) 
                : new Vector3(-1.30f, 0.40f, 0.0f);
            Vector3 size = new Vector3(2.20f, 0.80f, 3.0f);
            return new Bounds(center, size);
        }

        void FixedUpdate()
        {
            if (FMS.MatchState == MatchState.finished && !hasScored)
            {
                hasScored = true;
                StartCoroutine(DelayedScoreEndgame());
            }
            else if (FMS.MatchState != MatchState.finished)
            {
                hasScored = false;
            }
        }

        private IEnumerator DelayedScoreEndgame()
        {
            // Per official manual page 16:
            // "A ROBOT whose BUMPERS are fully or partially within the ALLIANCE's TABLE ZONE
            // at 3 seconds after the end of the MATCH has PARKED."
            yield return new WaitForSeconds(3.0f);
            ScoreEndgame();
        }

        public void ScoreEndgame()
        {
            int points = 0;
            Bounds zoneBounds = GetTableZoneBounds();
            HashSet<GameObject> parkedRobots = new HashSet<GameObject>();

            var robots = FindObjectsByType<SwerveController>(FindObjectsSortMode.None);
            foreach (var swerve in robots)
            {
                if (swerve == null) continue;
                var robotGo = swerve.gameObject;

                // Alliance check:
                // In single player practice, allow scoring for player's robot
                if (robots.Length > 1)
                {
                    bool robotIsBlue = HarvestHavocDepot.IsRobotBlueAlliance(robotGo);
                    if (robotIsBlue != isBlue) continue;
                }

                // Check if any collider of the robot or its center position overlaps the Table Zone
                bool isParked = false;
                var colliders = robotGo.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    if (col != null && !col.isTrigger && zoneBounds.Intersects(col.bounds))
                    {
                        isParked = true;
                        break;
                    }
                }
                if (!isParked && zoneBounds.Contains(robotGo.transform.position))
                {
                    isParked = true;
                }

                if (isParked && !parkedRobots.Contains(robotGo))
                {
                    parkedRobots.Add(robotGo);
                    int parkPts = 2; // Park points (official manual page 16, 17)
                    int haulPts = GetHarvestHaulPoints(robotGo); // Harvest haul points
                    points += parkPts + haulPts;
                    Debug.Log($"[HarvestHavocEndgame] {(isBlue ? "Blue" : "Red")} robot '{robotGo.name}' PARKED! Park: {parkPts}pts, Harvest Haul: {haulPts}pts");
                }
            }

            if (isBlue)
            {
                ScoreHolder.BlueScore += points;
            }
            else
            {
                ScoreHolder.RedScore += points;
            }
        }

        private int GetHarvestHaulPoints(GameObject robot)
        {
            int points = 0;
            int heldCount = 0;

            var buildNodes = robot.GetComponentsInChildren<BuildNode>();
            foreach (var node in buildNodes)
            {
                if (node != null && node.currentGamePiece != null)
                {
                    if (heldCount >= 3) break; // Max 3 items per rule (manual page 16, 17)

                    if (node.currentGamePiece.pieceType == PieceNames.Carrot)
                    {
                        points += 1;
                        heldCount++;
                    }
                    else if (node.currentGamePiece.pieceType == PieceNames.CarrotCake)
                    {
                        points += 3;
                        heldCount++;
                    }
                }
            }

            return points;
        }
    }
}

