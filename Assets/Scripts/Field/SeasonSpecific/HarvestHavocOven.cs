using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    public class HarvestHavocOven : FieldScorer
    {
        [SerializeField] private int carrotPoints = 2;
        [SerializeField] private int carrotCakePoints = 2;
        [SerializeField] private GameObject carrotCakePrefab;
        [SerializeField] private Transform depotSpawnPoint;

        private int scoredCount;
        private int storedCarrots;

        void Start()
        {
            // Initialize required FieldScorer variables
            scorePieces = new PieceNames[] { PieceNames.Carrot, PieceNames.CarrotCake };
            scoreToAdd = 2;
            autoScoreToAdd = 2;
            // Ensure a trigger box exists for visual debugging and automatic scoring
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            var boxScorer = gameObject.GetComponent<BoxScorer>();
            if (boxScorer == null) boxScorer = gameObject.AddComponent<BoxScorer>();
            boxScorer.isBlue = isBlue;
            boxScorer.cakeDepot = depotSpawnPoint;
            boxScorer.carrotCakePrefab = carrotCakePrefab;
            boxScorer.carrotPoints = carrotPoints;
            boxScorer.carrotCakePoints = carrotCakePoints;
        }

        void FixedUpdate()
        {
            // Scoring is handled by BoxScorer trigger. Pieces remain in the oven.
            return;
        }
    }
}
