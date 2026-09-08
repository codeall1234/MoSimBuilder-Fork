using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Util;
using BuilderLib;

namespace Field.SeasonSpecific
{
    public class HarvestHavocOven : FieldScorer
    {
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
            boxScorer.carrotPoints = 5; // pantry/oven carrot points
            boxScorer.carrotCakePoints = 8;
        }

        void FixedUpdate()
        {
            occupyObjects = occupyPieces();
            
            // Score 2 points per piece in the oven (1 * autoScoreToAdd which is 2)
            ScorePoints(occupyObjects.Count);

            // Find all Carrots and CarrotCakes in the oven
            List<GamePiece> carrots = new List<GamePiece>();
            foreach (var piece in occupyObjects)
            {
                if (piece.pieceType == PieceNames.Carrot)
                {
                    carrots.Add(piece);
                }
                else if (piece.pieceType == PieceNames.CarrotCake)
                {
                    // Carrot cakes scored in the oven are immediately recirculated
                    Destroy(piece.gameObject);
                    SpawnCarrotCake();
                }
            }

            // Simulate Human Player recirculation (trade 3 Carrots for 1 Carrot Cake)
            if (carrots.Count >= 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    Destroy(carrots[i].gameObject);
                }
                SpawnCarrotCake();
            }
        }

        private void SpawnCarrotCake()
        {
            if (carrotCakePrefab != null && depotSpawnPoint != null)
            {
                Instantiate(carrotCakePrefab, depotSpawnPoint.position, depotSpawnPoint.rotation);
            }
        }
    }
}
