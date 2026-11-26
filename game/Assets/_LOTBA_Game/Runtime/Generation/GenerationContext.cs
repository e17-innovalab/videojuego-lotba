using UnityEngine;

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
        public readonly int PinkGemLevel;
        public readonly bool RecoveryActive;
        public readonly float RecoveryZoneEnd;
        public readonly float RecoveryChallengeEnd;
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
            float gemIntensity = 1f,
            float recoveryChallengeEnd = -1f,
            int pinkGemLevel = 0)
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
            PinkGemLevel = Mathf.Max(pinkGemLevel, pinkGemsUnlocked ? 1 : 0);
            RecoveryActive = recoveryActive;
            RecoveryZoneEnd = recoveryZoneEnd;
            RecoveryChallengeEnd = recoveryChallengeEnd;
            Seed = seed;
            ObstacleIntensity = obstacleIntensity;
            GemIntensity = gemIntensity;
        }

        public GenerationContext WithHorizon(float newHorizon) => new GenerationContext(
            Distance, Speed, CurrentLane, SafeLane, newHorizon, ActiveZone, Rules, Route,
            Profile, PinkGemsUnlocked, RecoveryActive, RecoveryZoneEnd, Seed, ObstacleIntensity, GemIntensity,
            RecoveryChallengeEnd, PinkGemLevel
        );

        public EncounterProfile ProfileAt(float distance)
        {
            if ((RecoveryActive || ActiveZone == ZoneType.Recovery) && Rules != null && Rules.EncounterProfile != null)
                return Rules.EncounterProfile;
            if (Route == null || Route.Sections == null || Route.Sections.Length == 0)
                return Profile;
            int section = Route.SectionAt(distance, out _);
            return Route.Sections[section].EncounterProfile ?? Profile;
        }
    }
}
