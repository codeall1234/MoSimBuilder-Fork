using UnityEngine;
using Util;
using System.Collections.Generic;
using Field.SeasonSpecific;

namespace Util
{
    /// <summary>
    /// Handles automatic scoring when a GamePiece enters the trigger box and manages
    /// carrot‑cake recirculation to the designated depot (pantry or oven).
    /// </summary>
    public class BoxScorer : MonoBehaviour
    {
        [Header("Scoring Settings")]
        public bool isBlue; // Determines which alliance score to modify
        public int carrotPoints = 1;
        public int carrotCakePoints = 8;

        [Header("Carrot Cake Recirculation")]
        public Transform cakeDepot;               // Where to spawn a new cake
        public GameObject carrotCakePrefab;       // Prefab to instantiate

        private static readonly HashSet<int> _scoredPieces = new HashSet<int>();

        private void OnTriggerEnter(Collider other)
        {
            // Find the GamePiece component on this collider or any parent
            var piece = Utils.FindParentObjectComponent<GamePiece>(other.gameObject);
            if (piece == null || piece.state != GamePieceState.World) return;

            // Prevent double scoring
            int instanceId = piece.gameObject.GetInstanceID();
            if (_scoredPieces.Contains(instanceId)) return;

            // Determine points based on piece type
            int points = 0;
            bool isCake = false;
            if (piece.pieceType == PieceNames.Carrot)
            {
                points = carrotPoints;
            }
            else if (piece.pieceType == PieceNames.CarrotCake)
            {
                points = carrotCakePoints;
                isCake = true;
            }
            else
            {
                return; // Not a piece we care about
            }

            // Check if this scorer is on a Pantry shelf (5 pieces per level max)
            var pantry = GetComponent<HarvestHavocPantry>();
            if (pantry == null) pantry = GetComponentInParent<HarvestHavocPantry>();

            if (pantry != null)
            {
                // Try to allocate an unoccupied slot on this pantry level
                if (!pantry.TryScorePiece(piece, out Vector3 targetPos, out Quaternion targetRot))
                {
                    // Level is already full (5 pieces). Do not add any more to this level!
                    return;
                }

                // Snap into designated spot on the shelf
                piece.transform.position = targetPos;
                piece.transform.rotation = targetRot;
            }

            // Mark instance as scored so it cannot be scored again
            _scoredPieces.Add(instanceId);

            // Apply points to the correct alliance score
            if (isBlue)
                ScoreHolder.BlueScore += points;
            else
                ScoreHolder.RedScore += points;

            // Mark the piece as scored/stationary so it remains visible on the field
            // and cannot be re-intaked by robots
            piece.state = GamePieceState.Stationary;

            // Ensure colliders are enabled so it sits physically on the surface
            if (piece.colliderParent != null && !piece.colliderParent.activeSelf)
            {
                piece.colliderParent.SetActive(true);
            }

            // Lock physics securely as Kinematic so pieces do not fall out of the pantry shelf or oven
            if (piece.rb != null)
            {
                piece.rb.isKinematic = true;
                piece.rb.velocity = Vector3.zero;
                piece.rb.angularVelocity = Vector3.zero;
            }

            // If this is a carrot cake, spawn a fresh one at the depot
            if (isCake && carrotCakePrefab != null && cakeDepot != null)
            {
                Instantiate(carrotCakePrefab, cakeDepot.position, cakeDepot.rotation);
            }

            // Note: Piece is intentionally kept on the field (not Destroyed)
            // so it shows that the carrot or carrot cake is actually on the pantry or in the oven.
        }

        private void OnDestroy()
        {
            _scoredPieces.Clear();
        }

        public static void ResetScoredPieces()
        {
            _scoredPieces.Clear();
        }
    }
}
