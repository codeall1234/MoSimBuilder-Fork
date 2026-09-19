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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            _scoredPieces.Clear();
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.layer == 7 || other.GetComponentInParent<GamePiece>() != null)
            {
                OnTriggerEnter(other);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Find the GamePiece component on this collider or any parent
            var piece = Utils.FindParentObjectComponent<GamePiece>(other.gameObject);
            
            if (piece == null) return;

            bool isDebug = piece.gameObject.name.Contains("Carrot") || piece.gameObject.name.Contains("CarrotCake");

            if (piece.owner != null)
            {
                if (isDebug) Debug.Log($"[BoxScorer] Rejected {piece.name}: owner != null ({piece.owner.name})");
                return;
            }
            if (piece.state != GamePieceState.World)
            {
                if (isDebug) Debug.Log($"[BoxScorer] Rejected {piece.name}: state != World ({piece.state})");
                return;
            }

            // Never score rolling carrot cakes that were reintroduced down the ramp
            if (piece.GetComponent<HarvestHavocOven.RollingCarrotCake>() != null)
            {
                if (isDebug) Debug.Log($"[BoxScorer] Rejected {piece.name}: has RollingCarrotCake");
                return;
            }

            // Prevent double scoring
            int instanceId = piece.gameObject.GetInstanceID();
            if (_scoredPieces.Contains(instanceId))
            {
                if (isDebug) Debug.Log($"[BoxScorer] Rejected {piece.name}: already in _scoredPieces");
                return;
            }

            // Mark piece as no longer interactable by robots
            piece.owner = null;
            Transform fieldHolder = null;
            LoadMatch lm = FindFirstObjectByType<LoadMatch>();
            if (lm != null && lm.getFieldHolder() != null) fieldHolder = lm.getFieldHolder().transform;
            piece.transform.parent = piece.originalParent != null ? piece.originalParent : fieldHolder;
            piece.state = GamePieceState.World;

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

            Vector3 targetPos = piece.transform.position;
            Quaternion targetRot = piece.transform.rotation;

            if (pantry != null)
            {
                // Try to allocate an unoccupied slot on this pantry level
                if (!pantry.TryScorePiece(piece, out targetPos, out targetRot))
                {
                    if (isDebug) Debug.Log($"[BoxScorer] Rejected {piece.name}: Pantry level full");
                    // Level is already full (5 pieces). Do not add any more to this level!
                    return;
                }

                // Snap into designated spot on the shelf
                piece.transform.position = targetPos;
                piece.transform.rotation = targetRot;
                if (piece.rb != null)
                {
                    piece.rb.position = targetPos;
                    piece.rb.rotation = targetRot;
                }
            }

            // Check if this scorer is on an Oven
            var oven = GetComponent<HarvestHavocOven>();
            if (oven == null) oven = GetComponentInParent<HarvestHavocOven>();
            if (oven == null)
            {
                var proxy = GetComponent<HarvestHavocOven.OvenTriggerProxy>();
                if (proxy != null) oven = proxy.oven;
            }

            if (oven != null)
            {
                if (!oven.TryScorePiece(piece, out targetPos, out targetRot))
                {
                    if (isDebug) Debug.Log($"[BoxScorer] Rejected {piece.name}: Oven TryScorePiece failed (probably Y > 0.50)");
                    // Not a valid oven score (e.g. piece rolling down the big ramp on top of the oven)
                    return;
                }

                piece.transform.position = targetPos;
                piece.transform.rotation = targetRot;
                if (piece.rb != null)
                {
                    piece.rb.position = targetPos;
                    piece.rb.rotation = targetRot;
                }
            }

            if (isDebug) Debug.Log($"[BoxScorer] SCORED {piece.name}! Adding to _scoredPieces.");
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
            var childCols = piece.GetComponentsInChildren<Collider>(true);
            foreach (var col in childCols)
            {
                if (col != null && !col.isTrigger) col.enabled = true;
            }

            // Permanently ignore collisions between the scored piece and all robot colliders
            // to completely eliminate PhysX kinematic depenetration impulses against the robot
            var robots = FindObjectsOfType<SwerveController>();
            var pieceColliders = piece.GetComponentsInChildren<Collider>(true);
            foreach (var robot in robots)
            {
                if (robot == null) continue;
                var robotColliders = robot.GetComponentsInChildren<Collider>(true);
                foreach (var pCol in pieceColliders)
                {
                    if (pCol == null) continue;
                    foreach (var rCol in robotColliders)
                    {
                        if (rCol == null) continue;
                        Physics.IgnoreCollision(pCol, rCol, true);
                    }
                }
            }

            // Lock physics securely as Kinematic so pieces do not fall out of the pantry shelf or oven
            if (piece.rb != null)
            {
                piece.rb.velocity = Vector3.zero;
                piece.rb.angularVelocity = Vector3.zero;
                piece.rb.isKinematic = true;
                piece.rb.position = targetPos;
                piece.rb.rotation = targetRot;
            }
            piece.transform.position = targetPos;
            piece.transform.rotation = targetRot;

            // If this is a carrot cake and not handled by oven, spawn a fresh one at the depot
            if (oven == null && isCake && carrotCakePrefab != null && cakeDepot != null)
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
