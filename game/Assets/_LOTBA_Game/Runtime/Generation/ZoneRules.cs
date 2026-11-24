using UnityEngine;

namespace Lotba.Game
{
    public enum ZoneType
    {
        Normal,
        Recovery,
        Transition,
        Overstimulation
    }

    [CreateAssetMenu(fileName = "ZoneRules", menuName = "LOTBA/Generation/Zone Rules")]
    public class ZoneRules : ScriptableObject
    {
        [Header("Contenido permitido")]
        [SerializeField] private bool allowCommonGems = true;
        [SerializeField] private bool allowPinkGems = true;
        [SerializeField] private bool allowLetters = true;
        [SerializeField] private bool allowElevatedEncounters = true;
        [SerializeField] private bool allowProfileObstacles = true;
        [SerializeField] private EncounterProfile encounterProfile;
        [SerializeField, Tooltip("Set opcional de buses/containers reservado para encuentros aéreos durante recuperación.")]
        private ObstacleSet recoveryAerialSet;

        [Header("Multiplicadores de intensidad")]
        [SerializeField, Min(0.1f)] private float obstacleIntensityMultiplier = 1f;
        [SerializeField, Min(0f)] private float gemIntensityMultiplier = 1f;
        [SerializeField, Min(4f)] private float minimumObstacleSpacing = 12f;

        public bool AllowCommonGems => allowCommonGems;
        public bool AllowPinkGems => allowPinkGems;
        public bool AllowLetters => allowLetters;
        public bool AllowElevatedEncounters => allowElevatedEncounters;
        public bool AllowProfileObstacles => allowProfileObstacles;
        public EncounterProfile EncounterProfile => encounterProfile;
        public ObstacleSet RecoveryAerialSet => recoveryAerialSet;
        public float ObstacleIntensity => obstacleIntensityMultiplier;
        public float GemIntensity => gemIntensityMultiplier;
        public float MinimumObstacleSpacing => minimumObstacleSpacing;

        public static ZoneRules CreateDefault(ZoneType type)
        {
            var rules = CreateInstance<ZoneRules>();
            switch (type)
            {
                case ZoneType.Recovery:
                    rules.allowCommonGems = false;
                    rules.allowPinkGems = false;
                    rules.allowLetters = false;
                    rules.allowElevatedEncounters = true;
                    rules.allowProfileObstacles = true;
                    rules.obstacleIntensityMultiplier = 1.7f;
                    rules.gemIntensityMultiplier = 0f;
                    rules.minimumObstacleSpacing = 12f;
                    break;
                case ZoneType.Overstimulation:
                    rules.allowCommonGems = true;
                    rules.allowPinkGems = true;
                    rules.allowLetters = false;
                    rules.allowElevatedEncounters = true;
                    rules.allowProfileObstacles = true;
                    rules.obstacleIntensityMultiplier = 2.0f;
                    rules.gemIntensityMultiplier = 1.5f;
                    rules.minimumObstacleSpacing = 8f;
                    break;
                case ZoneType.Transition:
                    rules.allowCommonGems = false;
                    rules.allowPinkGems = false;
                    rules.allowLetters = false;
                    rules.allowElevatedEncounters = false;
                    rules.allowProfileObstacles = false;
                    rules.obstacleIntensityMultiplier = 0f;
                    rules.gemIntensityMultiplier = 0f;
                    break;
                default:
                    rules.allowCommonGems = true;
                    rules.allowPinkGems = true;
                    rules.allowLetters = true;
                    rules.allowElevatedEncounters = true;
                    rules.allowProfileObstacles = true;
                    rules.obstacleIntensityMultiplier = 1f;
                    rules.gemIntensityMultiplier = 1f;
                    rules.minimumObstacleSpacing = 12f;
                    break;
            }
            return rules;
        }
    }
}
