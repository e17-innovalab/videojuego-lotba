using UnityEngine;

namespace Lotba.Game
{
    public enum GroundEncounterPattern { SingleBlocker, CorridorGate, Slalom }

    [CreateAssetMenu(menuName = "LOTBA/Maps/Encounter profile")]
    public sealed class EncounterProfile : ScriptableObject
    {
        [SerializeField, Min(4f), Tooltip("Distancia objetivo entre comienzos de hileras de gemas, en metros.")]
        private float gemInterval = 30f;
        [SerializeField, Range(1, 3)] private int gemThreads = 1;
        [SerializeField, Range(0f, 1f)] private float parallelThreadChance = 1f;
        [SerializeField, Range(0f, 1f)] private float laneChangeChance;
        [SerializeField, Range(0f, .5f)] private float gemIntervalVariation;
        [SerializeField, Min(100f), Tooltip("Distancia objetivo entre encuentros elevados, en metros.")]
        private float vehiclePatternInterval = 240f;
        [SerializeField, Min(.1f), Tooltip("Tiempo objetivo entre encuentros elevados; la distancia crece con la velocidad.")]
        private float vehiclePatternIntervalSeconds = 5.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Probabilidad de que un obstáculo de tránsito sea auto o colectivo.")]
        private float trafficObstacleChance = .35f;
        [SerializeField, Range(0f, 1f), Tooltip("Probabilidad de preferir un vehículo largo cuando el patrón admite tránsito.")]
        private float longVehicleChance = .15f;
        [SerializeField, Range(0f, 1f), Tooltip("Probabilidad de un encuentro con buses en los tres carriles y rampa de acceso.")]
        private float threeBusRampChance;
        [SerializeField, Range(0f, 1f), Tooltip("Probabilidad de que el camino elevado termine en una estela que requiere saltar desde el techo.")]
        private float roofJumpExitChance;
        [SerializeField, Range(0f, 1f), Tooltip("Probabilidad de encuentros elevados durante recuperación; aumenta con el avance de la partida.")]
        private float recoveryAerialEncounterChance;
        [SerializeField, Min(30f), Tooltip("Distancia base entre desafíos elevados de recuperación.")]
        private float recoveryAerialInterval = 110f;
        [SerializeField, Min(0f), Tooltip("Distancia inicial despejada antes del primer desafío elevado de recuperación.")]
        private float recoveryAerialStartDelay = 72f;
        [SerializeField, Range(0f, 1f), Tooltip("Peso inicial del salto entre buses dentro de recuperación.")]
        private float recoveryBusJumpChance = .6f;
        [SerializeField, Range(0f, 1f), Tooltip("Peso del salto entre buses en etapas avanzadas.")]
        private float recoveryAdvancedBusJumpChance = .85f;
        [SerializeField, Range(0f, 1f), Tooltip("Peso de la rampa de tres buses en etapas avanzadas de recuperación.")]
        private float recoveryAdvancedThreeBusRampChance = .5f;
        [SerializeField, Min(.1f), Tooltip("Tiempo de reacción de recuperación en etapas avanzadas.")]
        private float recoveryAdvancedReactionSeconds = .38f;
        [SerializeField, Range(.2f, 1f), Tooltip("Factor de reducción del intervalo entre desafíos elevados en etapas avanzadas.")]
        private float recoveryAdvancedAerialIntervalMultiplier = .65f;
        [SerializeField, Range(1, 12), Tooltip("Cantidad de gemas en cada hilera de piso.")]
        private int gemsPerThread = 4;
        [SerializeField, Min(.5f), Tooltip("Separación base entre gemas de una hilera; puede ampliarse según la velocidad.")]
        private float gemSpacing = 2.5f;
        [Header("Rosas y esquive")]
        [SerializeField, Min(0)] private int pinkGemEveryRows = 4;
        [SerializeField, Range(2, 5), Tooltip("Cantidad de gemas rosas en cada mini-hilera tras la tercera mejora de multiplicador.")]
        private int pinkMiniRowCount = 3;
        [SerializeField, Min(0f)] private float pinkGemMinimumSpacing = 110f;
        [SerializeField, Range(0f, 1f)] private float pinkGemDodgeChance = .4f;
        [SerializeField, Min(0f), Tooltip("Margen adicional después del tiempo de cambio de carril, en segundos.")]
        private float pinkGemDodgeReactionTime = .03f;
        [SerializeField, Min(4f), Tooltip("Distancia objetivo entre inicios de patrones de obstáculo.")]
        private float obstacleInterval = 32f;
        [SerializeField, Min(2f), Tooltip("Separación mínima después del final físico de un patrón.")]
        private float obstacleClearance = 10f;
        [SerializeField, Min(.1f), Tooltip("Tiempo mínimo de lectura antes de otro patrón; la distancia crece con la velocidad.")]
        private float obstacleReactionSeconds = .85f;
        [Header("Escalado gradual dentro de la zona")]
        [SerializeField, Min(0f), Tooltip("Metros desde el inicio de esta zona para alcanzar los valores avanzados. Cero desactiva el escalado.")]
        private float difficultyProgressionMetres;
        [SerializeField, Min(.1f)] private float advancedObstacleReactionSeconds = .7f;
        [SerializeField, Min(4f)] private float advancedObstacleInterval = 10f;
        [SerializeField, Min(.1f)] private float advancedVehiclePatternIntervalSeconds = 4.2f;
        [SerializeField, Range(0f, 1f)] private float advancedTrafficObstacleChance;
        [SerializeField, Range(0f, 1f)] private float advancedThreeBusRampChance;
        [SerializeField, Range(0f, 1f)] private float advancedRoofJumpExitChance;
        [SerializeField, Min(.1f), Tooltip("Ritmo objetivo entre hileras de gemas en segundos; la distancia crece con la velocidad.")]
        private float gemRowIntervalSeconds = 1.35f;
        [SerializeField, Min(.01f), Tooltip("Tiempo mínimo entre gemas de una hilera; el espacio crece con la velocidad de la partida.")]
        private float gemCollectionIntervalSeconds = .12f;
        [SerializeField, Min(20f), Tooltip("Distancia base entre letras coleccionables.")]
        private float letterInterval = 100f;
        [SerializeField, Range(0f, .5f), Tooltip("Variación proporcional de la distancia entre letras.")]
        private float letterIntervalVariation = .1f;
        [SerializeField, Range(0f, 1f)] private float extraObstacleChance;
        [Header("Mezcla de patrones de obstáculos")]
        [SerializeField, Min(0f)] private float singleBlockerWeight = .45f;
        [SerializeField, Min(0f)] private float corridorGateWeight = .35f;
        [SerializeField, Min(0f)] private float slalomWeight = .20f;
        [SerializeField, Range(0f, 1f)] private float hurdleChance = .15f;

        public int GemThreads => Mathf.Clamp(gemThreads, 1, 3);
        public float ParallelThreadChance => parallelThreadChance;
        public float LaneChangeChance => laneChangeChance;
        public float GemIntervalVariation => gemIntervalVariation;
        public float VehiclePatternInterval => Mathf.Max(100f, vehiclePatternInterval);
        public float VehiclePatternIntervalSeconds => Mathf.Max(.1f, vehiclePatternIntervalSeconds);
        public float TrafficObstacleChance => trafficObstacleChance;
        public float LongVehicleChance => longVehicleChance;
        public float ThreeBusRampChance => threeBusRampChance;
        public float RoofJumpExitChance => roofJumpExitChance;
        public float RecoveryAerialEncounterChance => recoveryAerialEncounterChance;
        public float RecoveryAerialInterval => Mathf.Max(30f, recoveryAerialInterval);
        public float RecoveryAerialStartDelay => Mathf.Max(0f, recoveryAerialStartDelay);
        public float RecoveryBusJumpChance => recoveryBusJumpChance;
        public float RecoveryAdvancedBusJumpChance => recoveryAdvancedBusJumpChance;
        public float RecoveryAdvancedThreeBusRampChance => recoveryAdvancedThreeBusRampChance;
        public float RecoveryAdvancedReactionSeconds => Mathf.Max(.1f, recoveryAdvancedReactionSeconds);
        public float RecoveryAdvancedAerialIntervalMultiplier => Mathf.Clamp(recoveryAdvancedAerialIntervalMultiplier, .2f, 1f);
        public int GemsPerThread => Mathf.Clamp(gemsPerThread, 1, 12);
        public float GemSpacing => Mathf.Max(.5f, gemSpacing);
        public float GemInterval => Mathf.Max(4f, gemInterval);
        public int PinkGemEveryRows => pinkGemEveryRows;
        public int PinkMiniRowCount => Mathf.Clamp(pinkMiniRowCount, 2, 5);
        public float PinkGemMinimumSpacing => Mathf.Max(0f, pinkGemMinimumSpacing);
        public float PinkGemDodgeChance => pinkGemDodgeChance;
        public float PinkGemDodgeReactionTime => Mathf.Max(0f, pinkGemDodgeReactionTime);
        public float ObstacleInterval => Mathf.Max(4f, obstacleInterval);
        public float ObstacleClearance => Mathf.Max(2f, obstacleClearance);
        public float ObstacleReactionSeconds => Mathf.Max(.1f, obstacleReactionSeconds);
        public float DifficultyProgressionMetres => Mathf.Max(0f, difficultyProgressionMetres);
        private float DifficultyT(float progress) => DifficultyProgressionMetres > 0f ? Mathf.Clamp01(progress) : 0f;
        public float ObstacleReactionSecondsAt(float progress) => Mathf.Lerp(ObstacleReactionSeconds,
            Mathf.Max(.1f, advancedObstacleReactionSeconds), DifficultyT(progress));
        public float ObstacleIntervalAt(float progress) => Mathf.Lerp(ObstacleInterval,
            Mathf.Max(4f, advancedObstacleInterval), DifficultyT(progress));
        public float VehiclePatternIntervalSecondsAt(float progress) => Mathf.Lerp(VehiclePatternIntervalSeconds,
            Mathf.Max(.1f, advancedVehiclePatternIntervalSeconds), DifficultyT(progress));
        public float TrafficObstacleChanceAt(float progress) => Mathf.Lerp(trafficObstacleChance,
            advancedTrafficObstacleChance, DifficultyT(progress));
        public float ThreeBusRampChanceAt(float progress) => Mathf.Lerp(threeBusRampChance,
            advancedThreeBusRampChance, DifficultyT(progress));
        public float RoofJumpExitChanceAt(float progress) => Mathf.Lerp(roofJumpExitChance,
            advancedRoofJumpExitChance, DifficultyT(progress));
        public float GemRowIntervalSeconds => Mathf.Max(.1f, gemRowIntervalSeconds);
        public float GemCollectionIntervalSeconds => Mathf.Max(.01f, gemCollectionIntervalSeconds);
        public float LetterInterval => Mathf.Max(20f, letterInterval);
        public float LetterIntervalVariation => Mathf.Clamp01(letterIntervalVariation);
        public float ExtraObstacleChance => extraObstacleChance;
        public float HurdleChance => hurdleChance;

        public GroundEncounterPattern PickGroundPattern(System.Random random)
        {
            float total = singleBlockerWeight + corridorGateWeight + slalomWeight;
            if (total <= 0f) return GroundEncounterPattern.SingleBlocker;
            double roll = random.NextDouble() * total;
            if (roll < singleBlockerWeight) return GroundEncounterPattern.SingleBlocker;
            roll -= singleBlockerWeight;
            if (roll < corridorGateWeight) return GroundEncounterPattern.CorridorGate;
            return GroundEncounterPattern.Slalom;
        }
    }
}
