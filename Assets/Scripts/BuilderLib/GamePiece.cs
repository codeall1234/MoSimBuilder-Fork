using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;

public class GamePiece: MonoBehaviour
{
    public PieceNames pieceType;
    public Transform owner;
    public Rigidbody rb;
    public GamePieceState state;
    public GameObject colliderParent;
    [HideInInspector] public Vector3 startPosition;
    [HideInInspector] public Transform originalParent;
    [HideInInspector] public float startingDistance;
    private bool hasId;

    private void Start()
    {
        hasId = false;
    }

    private void Update()
    {
        if (hasId) return;
        if (!rb) rb = GetComponent<Rigidbody>();
        if (originalParent == null)
        {
            var core = Utils.FindParentObjectComponent<LoadMatch>(gameObject);
            if (core == null) core = FindObjectOfType<LoadMatch>();
            if (core != null && core.getFieldHolder() != null && core.getFieldHolder().transform.childCount > 0)
            {
                originalParent = core.getFieldHolder().transform.GetChild(0);
            }
            else
            {
                originalParent = transform.parent;
            }
        }
        hasId = true;
    }
}
