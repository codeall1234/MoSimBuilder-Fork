using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    public class HarvestHavocEndgame : MonoBehaviour
    {
        [SerializeField] private bool isBlue;
        [SerializeField] private Collider[] tableZoneColliders;
        
        private LayerMask robotMask;
        private bool hasScored = false;

        void Start()
        {
            robotMask = LayerMask.GetMask("Robot");
        }

        void FixedUpdate()
        {
            if (FMS.MatchState == MatchState.finished && !hasScored)
            {
                ScoreEndgame();
                hasScored = true;
            }
            else if (FMS.MatchState != MatchState.finished)
            {
                hasScored = false;
            }
        }

        private void ScoreEndgame()
        {
            int points = 0;
            
            HashSet<GameObject> parkedRobots = new HashSet<GameObject>();
            
            foreach (var col in tableZoneColliders)
            {
                var overlap = Physics.OverlapBox(col.bounds.center, col.bounds.extents, col.transform.rotation, robotMask);
                foreach (var hit in overlap)
                {
                    var robot = Utils.FindParentObjectComponent<BuildFrame>(hit.gameObject);
                    if (robot != null && !parkedRobots.Contains(robot.gameObject))
                    {
                        parkedRobots.Add(robot.gameObject);
                        
                        points += 2; // Park points
                        points += GetHarvestHaulPoints(robot.gameObject); // Harvest Haul points
                    }
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
                if (node.currentGamePiece != null)
                {
                    if (heldCount >= 3) break; // Max 3 items
                    
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
