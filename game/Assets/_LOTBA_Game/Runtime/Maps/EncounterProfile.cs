using UnityEngine;

namespace Lotba.Game
{
    [CreateAssetMenu(menuName = "LOTBA/Maps/Encounter profile")]
    public sealed class EncounterProfile : ScriptableObject
    {
        [SerializeField, Min(4f)] private float gemInterval = 30f;
        [SerializeField, Range(1, 3)] private int gemThreads = 1;
        [SerializeField, Range(0f, 1f)] private float parallelThreadChance = 1f;
        [SerializeField, Range(0f, 1f)] private float laneChangeChance;
        [SerializeField, Range(0f, .5f)] private float gemIntervalVariation;
        [SerializeField, Min(100f)] private float vehiclePatternInterval = 240f;
        [SerializeField, Range(1, 12)] private int gemsPerThread = 4;
        [SerializeField, Min(.5f)] private float gemSpacing = 2.5f;
        [Header("Rosas y esquive")]
        [SerializeField, Min(0)] private int pinkGemEveryRows = 4;
        [SerializeField, Min(0f)] private float pinkGemMinimumSpacing = 110f;
        [SerializeField, Range(0f, 1f)] private float pinkGemDodgeChance = .4f;
        [SerializeField, Min(0f), Tooltip("Margen adicional después del tiempo de cambio de carril, en segundos.")]
        private float pinkGemDodgeReactionTime = .03f;
        [SerializeField, Min(4f)] private float obstacleInterval = 32f;
        [SerializeField, Min(2f)] private float obstacleClearance = 10f;
        [SerializeField, Range(0f, 1f)] private float extraObstacleChance;

        public int GemThreads => Mathf.Clamp(gemThreads, 1, 3);
        public float ParallelThreadChance => parallelThreadChance;
        public float LaneChangeChance => laneChangeChance;
        public float GemIntervalVariation => gemIntervalVariation;
        public float VehiclePatternInterval => Mathf.Max(100f, vehiclePatternInterval);
        public int GemsPerThread => Mathf.Clamp(gemsPerThread, 1, 12);
        public float GemSpacing => Mathf.Max(.5f, gemSpacing);
        public float GemInterval => Mathf.Max(4f, gemInterval);
        public int PinkGemEveryRows => pinkGemEveryRows;
        public float PinkGemMinimumSpacing => Mathf.Max(0f, pinkGemMinimumSpacing);
        public float PinkGemDodgeChance => pinkGemDodgeChance;
        public float PinkGemDodgeReactionTime => Mathf.Max(0f, pinkGemDodgeReactionTime);
        public float ObstacleInterval => Mathf.Max(4f, obstacleInterval);
        public float ObstacleClearance => Mathf.Max(2f, obstacleClearance);
        public float ExtraObstacleChance => extraObstacleChance;
    }
}
