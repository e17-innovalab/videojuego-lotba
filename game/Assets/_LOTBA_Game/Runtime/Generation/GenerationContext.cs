namespace Lotba.Game
{
    /// <summary>
    /// Contexto inmutable que describe el estado de la partida relevante para la generación.
    /// Desacopla a los generadores de la dependencia directa con RunSession.
    /// </summary>
    public readonly struct GenerationContext
    {
        public readonly float Distance;
        public readonly float Speed;
        public readonly int CurrentLane;
        public readonly int SafeLane;
        public readonly float SpawnHorizon;
        public readonly ZoneType ActiveZone;
        public readonly ZoneRules Rules;
        public readonly RouteDefinition Route;
        public readonly EncounterProfile Profile;
        public readonly bool PinkGemsUnlocked;
        public readonly bool RecoveryActive;
        public readonly float RecoveryZoneEnd;
        public readonly int Seed;
        public readonly float ObstacleIntensity;
        public readonly float GemIntensity;

        public GenerationContext(
            float distance,
            float speed,
            int currentLane,
            int safeLane,
            float spawnHorizon,
            ZoneType activeZone,
            ZoneRules rules,
            RouteDefinition route,
            EncounterProfile profile,
            bool pinkGemsUnlocked,
            bool recoveryActive,
            float recoveryZoneEnd,
            int seed,
            float obstacleIntensity = 1f,
            float gemIntensity = 1f)
        {
            Distance = distance;
            Speed = speed;
            CurrentLane = currentLane;
            SafeLane = safeLane;
            SpawnHorizon = spawnHorizon;
            ActiveZone = activeZone;
            Rules = rules;
            Route = route;
            Profile = profile;
            PinkGemsUnlocked = pinkGemsUnlocked;
            RecoveryActive = recoveryActive;
            RecoveryZoneEnd = recoveryZoneEnd;
            Seed = seed;
            ObstacleIntensity = obstacleIntensity;
            GemIntensity = gemIntensity;
        }

        public GenerationContext WithHorizon(float newHorizon) => new GenerationContext(
            Distance, Speed, CurrentLane, SafeLane, newHorizon, ActiveZone, Rules, Route,
            Profile, PinkGemsUnlocked, RecoveryActive, RecoveryZoneEnd, Seed, ObstacleIntensity, GemIntensity
        );
    }
}
