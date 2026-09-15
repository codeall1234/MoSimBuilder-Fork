using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;

namespace BuilderLib
{
    public static class GamePieceManager
    {
        public static void disableColliders(GamePiece piece)
        {
            if (piece.colliderParent.activeSelf)
            {
                piece.colliderParent.SetActive(false);
            }
        }

        public static IEnumerator enableColliders(GamePiece piece, Transform robotRoot = null)
        {
            if (piece == null) yield break;

            if (piece.colliderParent != null && piece.colliderParent.activeSelf)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.05f);
        
            if (piece != null && piece.colliderParent != null)
            {
                piece.colliderParent.SetActive(true);
            }

            if (robotRoot != null)
            {
                yield return new WaitForSeconds(1.5f);
                // Only re-enable collisions if piece is still dynamically in the world and has cleared the robot
                if (piece != null && robotRoot != null && piece.state == GamePieceState.World)
                {
                    float dist = Vector3.Distance(piece.transform.position, robotRoot.position);
                    if (dist > 1.2f)
                    {
                        var robotColliders = robotRoot.GetComponentsInChildren<Collider>(true);
                        var pieceColliders = piece.GetComponentsInChildren<Collider>(true);
                        foreach (var pCol in pieceColliders)
                        {
                            if (pCol == null || pCol.isTrigger) continue;
                            foreach (var rCol in robotColliders)
                            {
                                if (rCol == null || rCol.isTrigger) continue;
                                Physics.IgnoreCollision(pCol, rCol, false);
                            }
                        }
                    }
                }
            }
        }
        public static bool AnimateTo(GamePiece piece, NodeAction action, Transform t = null)
        {
            var speed = action.Speed * 0.0254f;
        
            var transform = piece.rb.transform;
            var target = t ? t : action.MoveTo.transform;
            Rigidbody parentRb = null;
            if (target != t)
            {
                parentRb = Utils.FindParentRB(action.MoveTo.gameObject).GetComponent<Rigidbody>();
            }
            
            
            if (piece.state != GamePieceState.Moving)
            {
                piece.state = GamePieceState.Moving;
                piece.startPosition = transform.localPosition;
                disableColliders(piece);
            }
        
            var distance = transform.parent.InverseTransformPoint(target.position) - piece.startPosition;
            var parentPosition = transform.parent.position;
            Vector3 parentVelocity = Vector3.zero;
            if (parentRb) parentVelocity = transform.parent.InverseTransformDirection(parentRb.velocity);
            
            // Calculate the step, but clamp it to not overshoot
            var distanceMagnitude = distance.magnitude;
            var maxStep = speed * Time.deltaTime;
            var stepMagnitude = Mathf.Min(maxStep, distanceMagnitude);
            var step = (distance.normalized * stepMagnitude) + (parentVelocity * Time.smoothDeltaTime);
            
            var finalPosition = piece.startPosition + step;
        
            piece.startPosition = finalPosition;
            transform.position = parentPosition + transform.parent.TransformDirection(finalPosition);
            piece.rb.position = parentPosition + transform.parent.TransformDirection(finalPosition);
            piece.rb.velocity = Vector3.zero;
        
            // Calculate target rotation based on movement direction
            Quaternion targetRotation = target.rotation;
            
            // Smoothly rotate towards target rotation
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                action.AngularSpeed * Time.deltaTime  // Changed from Time.fixedDeltaTime to Time.deltaTime
            );
        
            if (action.AngularSpeed == 0)
            {
                transform.localRotation = Quaternion.identity;
            }
        
            if (distanceMagnitude <= 0.75f * 0.0254f)
            {
                changeParent(piece, action, t);
                return true; // Reached target
            }
            else
            {
                return false; // Moving towards target
            }
        }

        public static bool teleportTo(GamePiece piece, NodeAction action)
        {
            var target = action.MoveTo.transform;
            teleportTo(piece, target, true);
            changeParent(piece, action);
            return true;
        }
    
        public static bool teleportTo(GamePiece piece, Transform target, bool alreadyChanged = false)
        {
            disableColliders(piece);
            var transform = piece.rb.transform;
            transform.position = target.position;
            transform.rotation = target.rotation;
            if (alreadyChanged) return true;
            changeParent(piece, target);
            return true;
        }
    
        public static bool ReleaseToWorld(GamePiece piece, NodeAction action)
        {
            if (!piece) return false;
            if (!piece.owner) return false;
            if (!action.IsValidPiece(piece.pieceType)) return false;

            // Ignore collisions between piece colliders and all robot colliders during release
            Transform robotRoot = piece.owner != null ? piece.owner.root : null;
            if (robotRoot != null)
            {
                var robotColliders = robotRoot.GetComponentsInChildren<Collider>(true);
                var pieceColliders = piece.GetComponentsInChildren<Collider>(true);
                foreach (var pCol in pieceColliders)
                {
                    if (pCol == null || pCol.isTrigger) continue;
                    foreach (var rCol in robotColliders)
                    {
                        if (rCol == null || rCol.isTrigger) continue;
                        Physics.IgnoreCollision(pCol, rCol, true);
                    }
                }
            }

            var speed = action.overideSpeed * 0.0254f ?? action.Speed * 0.0254f;
            action.overideSpeed = null;
            var rb = piece.rb;
            var transform = rb.transform;

            rb.velocity = Vector3.zero;
            piece.transform.localPosition = Vector3.zero;
            piece.transform.localEulerAngles = action.OuttakeRotationOffset;

            Vector3 velocity;
            switch (action.Direction)
            {
                case Direction.forward:
                    velocity = piece.owner.transform.forward.normalized * speed;
                    break;
                case Direction.up:
                    velocity = piece.owner.transform.up.normalized * speed;
                    break;
                case Direction.sideways:
                    velocity = piece.owner.transform.right * speed;
                    break;
                default:
                    velocity = piece.owner.transform.forward.normalized * speed;
                    break;
            }
            rb.velocity = velocity;
            rb.angularVelocity = transform.TransformDirection(action.Spin);
        
            piece.state = GamePieceState.World;

            piece.transform.parent = piece.originalParent;
            
            piece.owner = null;
        
            return true;
        }


        public static bool changeParent(GamePiece piece, NodeAction action, Transform t = null)
        {
            var value = t ? t : action.MoveTo.transform;
            if (action.MoveTo)
            {
                action.MoveTo.currentGamePiece = piece;
                action.MoveTo.currentState = NodeState.Stowing;
            }
            changeParent(piece, value);
            return true;
        }
    
        public static bool changeParent(GamePiece piece, Transform target)
        {
            piece.owner = target.transform;
            piece.transform.parent = target.transform;
            piece.state = GamePieceState.Stationary;
            return true;
        }
    
        private static Quaternion FindShortestSymmetricRotation(Quaternion current, Quaternion target)
        {
            // For objects with symmetry on X, Y, and Z axes, we need to check multiple equivalent rotations
            List<Quaternion> symmetricRotations = new List<Quaternion>();

            // Original target
            symmetricRotations.Add(target);

            // Single axis symmetries (180° rotation around each axis)
            symmetricRotations.Add(target * Quaternion.Euler(180f, 0f, 0f));   // X-axis symmetry
            symmetricRotations.Add(target * Quaternion.Euler(0f, 180f, 0f));   // Y-axis symmetry
            symmetricRotations.Add(target * Quaternion.Euler(0f, 0f, 180f));   // Z-axis symmetry

            // Double axis symmetries
            symmetricRotations.Add(target * Quaternion.Euler(180f, 180f, 0f)); // X and Y
            symmetricRotations.Add(target * Quaternion.Euler(180f, 0f, 180f)); // X and Z
            symmetricRotations.Add(target * Quaternion.Euler(0f, 180f, 180f)); // Y and Z

            // Triple axis symmetry
            symmetricRotations.Add(target * Quaternion.Euler(180f, 180f, 180f)); // X, Y, and Z

            // Find the rotation with the smallest angular distance
            Quaternion bestRotation = target;
            float smallestAngle = float.MaxValue;

            foreach (Quaternion symRotation in symmetricRotations)
            {
                float angle = Quaternion.Angle(current, symRotation);
                if (angle < smallestAngle)
                {
                    smallestAngle = angle;
                    bestRotation = symRotation;
                }
            }

            return bestRotation;
        }
    }
}



