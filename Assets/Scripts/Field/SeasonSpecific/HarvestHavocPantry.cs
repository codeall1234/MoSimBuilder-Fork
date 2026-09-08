using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Util;
using BuilderLib;
using Util;

namespace Field.SeasonSpecific
{
    public class HarvestHavocPantry : FieldScorer
    {
        [SerializeField] private int carrotPoints = 5;
        [SerializeField] private int carrotCakePoints = 8;
        [SerializeField] private Transform depotSpawnPoint;
        [SerializeField] private GameObject carrotCakePrefab;
        
        void Start()
        {
            // Initialize required FieldScorer variables
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
            // Scoring and recirculation are now handled by BoxScorer trigger.
            // No additional logic needed here.
            return;
        }
    }
}
