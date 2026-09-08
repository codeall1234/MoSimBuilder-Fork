using UnityEngine;
using Util;
using System.Collections.Generic;

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

        private static readonly HashSet<int> _scoredThisFrame = new HashSet<int>();

        private void OnTriggerEnter(Collider other)
        {
            // Find the GamePiece component on this collider or any parent
            var piece = Utils.FindParentObjectComponent<GamePiece>(other.gameObject);
            if (piece == null || piece.state != GamePieceState.World) return;

            // Prevent double scoring within same frame
            int instanceId = piece.gameObject.GetInstanceID();
            if (_scoredThisFrame.Contains(instanceId)) return;
            _scoredThisFrame.Add(instanceId);

            // Award points based on piece type
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

            // Apply points to the correct alliance score
            if (isBlue)
                ScoreHolder.BlueScore += points;
            else
                ScoreHolder.RedScore += points;

            // If this is a carrot cake, spawn a fresh one at the depot
            if (isCake && carrotCakePrefab != null && cakeDepot != null)
            {
                Instantiate(carrotCakePrefab, cakeDepot.position, cakeDepot.rotation);
            }

            // Remove the original piece from the field
            Destroy(piece.gameObject);
        }

        private void LateUpdate()
        {
            _scoredThisFrame.Clear();
        }
    }
}
