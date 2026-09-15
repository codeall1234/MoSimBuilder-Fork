using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Util;

public class ClawWristRotator : MonoBehaviour
{
    [Tooltip("Target rotation angle around arm longitudinal axis when holding a piece or extended (degrees)")]
    public float extendedAngle = 90f;

    [Tooltip("Rotation speed in degrees per second")]
    public float rotateSpeed = 540f;

    [Tooltip("Rotation pivot in arm local coordinates")]
    public Vector3 localPivot = new Vector3(0.188f, 0f, 0f);

    private struct ChildPose
    {
        public Transform transform;
        public Vector3 initialLocalPos;
        public Quaternion initialLocalRot;
    }

    private readonly List<ChildPose> _placingChildren = new List<ChildPose>();
    private float _currentAngle = 0f;
    private bool _isElevatorExtended = false;

    private Buildelevator _elevator;
    private JointController _elevatorController;
    private JointController _armController;
    private PlayerInput _playerInput;
    private InputActionMap _inputMap;

    private void Start()
    {
        InitializePlacingChildren();
        InitializeControllers();
    }

    private void InitializePlacingChildren()
    {
        _placingChildren.Clear();
        float sumX = 0f;
        int countX = 0;
        bool foundNode = false;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            string lowerName = child.name.ToLower();
            // The arm tube model is not part of the claw/wrist assembly
            if (lowerName.Contains("model") || lowerName.Contains("tubing")) continue;

            _placingChildren.Add(new ChildPose
            {
                transform = child,
                initialLocalPos = child.localPosition,
                initialLocalRot = child.localRotation
            });

            if (lowerName.Contains("stow") || lowerName.Contains("outake"))
            {
                localPivot.x = child.localPosition.x;
                localPivot.y = child.localPosition.y;
                foundNode = true;
            }
            else if (!foundNode)
            {
                sumX += child.localPosition.x;
                countX++;
            }
        }

        if (!foundNode && countX > 0)
        {
            localPivot.x = sumX / countX;
            localPivot.y = 0f;
        }
    }

    private void InitializeControllers()
    {
        _armController = GetComponent<JointController>();
        _elevator = GetComponentInParent<Buildelevator>();
        if (_elevator == null)
        {
            _elevator = Utils.FindParentObjectComponent<Buildelevator>(gameObject);
        }
        if (_elevator != null)
        {
            _elevatorController = _elevator.GetController();
        }

        _playerInput = Utils.FindParentObjectComponent<PlayerInput>(gameObject);
        if (_playerInput != null && _playerInput.actions != null)
        {
            _inputMap = _playerInput.actions.FindActionMap("Robot");
        }
    }

    /// <summary>
    /// Checks whether a game piece has been transferred to/held in the arm's placing head.
    /// </summary>
    public bool HasPieceInArm()
    {
        for (int i = 0; i < _placingChildren.Count; i++)
        {
            var t = _placingChildren[i].transform;
            if (t == null) continue;
            var node = t.GetComponent<BuildNode>();
            if (node != null && node.currentGamePiece != null)
            {
                return true;
            }
        }

        var pieces = GetComponentsInChildren<GamePiece>(true);
        foreach (var p in pieces)
        {
            if (p != null && p.owner != null && p.owner.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (_placingChildren.Count == 0)
        {
            InitializePlacingChildren();
            if (_placingChildren.Count == 0) return;
        }

        CheckElevatorState();

        // Rotate when a piece has been transferred to the arm, OR when elevator is extended to score
        bool shouldRotate = HasPieceInArm() || _isElevatorExtended;

        float targetAngle = shouldRotate ? extendedAngle : 0f;
        if (Mathf.Abs(_currentAngle - targetAngle) > 0.01f)
        {
            _currentAngle = Mathf.MoveTowards(_currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
            ApplyRotation(_currentAngle);
        }
    }

    private void CheckElevatorState()
    {
        if (_inputMap == null && _playerInput != null && _playerInput.actions != null)
        {
            _inputMap = _playerInput.actions.FindActionMap("Robot");
        }

        // 1. Direct input button checks for elevator extension (L1, L2, L3, L4, Barge)
        if (_inputMap != null)
        {
            int[] extendKeys = { 24, 16, 20, 21, 27 };
            int[] extendButtons = { 0, 1, 2, 3, 4 };
            foreach (int k in extendKeys)
            {
                var action = _inputMap.FindAction(k.ToString());
                if (action != null && action.triggered)
                {
                    _isElevatorExtended = true;
                    break;
                }
            }
            foreach (int b in extendButtons)
            {
                var action = _inputMap.FindAction(b.ToString());
                if (action != null && action.triggered)
                {
                    _isElevatorExtended = true;
                    break;
                }
            }

            // Intake button: Keyboard 29, Controller 8
            var intakeK = _inputMap.FindAction("29");
            if (intakeK != null && (intakeK.triggered || intakeK.IsPressed()))
            {
                _isElevatorExtended = false;
            }
            var intakeC = _inputMap.FindAction("8");
            if (intakeC != null && (intakeC.triggered || intakeC.IsPressed()))
            {
                _isElevatorExtended = false;
            }
        }

        // 2. Active setpoint checks
        if (_elevator != null && _elevatorController == null)
        {
            _elevatorController = _elevator.GetController();
        }

        if (_elevatorController != null)
        {
            string sp = _elevatorController.GetActiveSetpoint();
            if (!string.IsNullOrEmpty(sp))
            {
                string upper = sp.ToUpper();
                if (upper.StartsWith("L") || upper.Contains("BARGE") || upper.Contains("SCORE") || upper.Contains("OVEN") || upper.Contains("REEF"))
                {
                    _isElevatorExtended = true;
                }
                else if (upper.Contains("INTAKE") || upper.Contains("HOME"))
                {
                    _isElevatorExtended = false;
                }
            }
        }

        if (_armController != null)
        {
            string armSp = _armController.GetActiveSetpoint();
            if (!string.IsNullOrEmpty(armSp))
            {
                string upper = armSp.ToUpper();
                if (upper.StartsWith("L") || upper.Contains("BARGE") || upper.Contains("SCORE") || upper.Contains("OVEN") || upper.Contains("REEF"))
                {
                    _isElevatorExtended = true;
                }
                else if (upper.Contains("INTAKE") || upper.Contains("HOME"))
                {
                    _isElevatorExtended = false;
                }
            }
        }

        // 3. Position check: if elevator has returned to stowed height and no scoring setpoint active
        if (_elevatorController != null)
        {
            string sp = _elevatorController.GetActiveSetpoint();
            bool hasScoringSp = !string.IsNullOrEmpty(sp) && 
                (sp.ToUpper().StartsWith("L") || sp.ToUpper().Contains("BARGE") || sp.ToUpper().Contains("SCORE") || sp.ToUpper().Contains("OVEN"));
            
            if (!hasScoringSp && _elevatorController.currentPosition < 0.08f)
            {
                _isElevatorExtended = false;
            }
        }
    }

    private void ApplyRotation(float angle)
    {
        Quaternion deltaRot = Quaternion.AngleAxis(angle, Vector3.forward);
        foreach (var child in _placingChildren)
        {
            if (child.transform == null) continue;
            Vector3 offset = new Vector3(child.initialLocalPos.x - localPivot.x, child.initialLocalPos.y - localPivot.y, 0f);
            Vector3 rotatedOffset = deltaRot * offset;
            child.transform.localPosition = new Vector3(
                localPivot.x + rotatedOffset.x,
                localPivot.y + rotatedOffset.y,
                child.initialLocalPos.z
            );
            child.transform.localRotation = deltaRot * child.initialLocalRot;
        }
    }
}
