using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class RunTelemetry
    {
        public const int CurrentSchemaVersion = 20;
        public int schemaVersion = CurrentSchemaVersion;
        public static string SchemaLabel(int version) => $"Datos v{version}";
        public string runId = Guid.NewGuid().ToString("N");
        public string gameVersion;
        public string startedAtUtc;
        public string finishedAtUtc;
        public string route;
        public int seed;
        public float durationSeconds;
        public float routeDistanceMetres;
        public RunBrakingConfiguration braking;
        public List<RunTelemetryEvent> events = new List<RunTelemetryEvent>();
        public RunTelemetrySummary summary;

        public string ToJson() => JsonUtility.ToJson(this, true);
    }

    [Serializable]
    public sealed class RunTelemetryEvent
    {
        public int sequence;
        public string type;
        public string zone;
        public string detail;
        public float timeSeconds;
        public float distanceMetres;
        public float speedKmh;
        public int lane;
        public long score;
        public long scoreDelta;
        public int gemsCollected, gemBalance, gemDelta;
        public bool duringBoost;
        public int decisionNumber;
        public int pinkGemStreak, scoreMultiplier, pinkGemStreakBefore;
        public bool pinkGemNearMiss;
        public int pinkDodgeEncounter;
        public float purchasedSpeedKmh;
        // Added to the unpublished v20 contract; historical local reports may lack it.
        public RunTurboConfiguration turboConfiguration;
    }

    [Serializable]
    public sealed class RunTurboConfiguration
    {
        public float durationSeconds, speedBonusMetresPerSecond;
        public int gemMultiplier, pointMultiplier;

        public static RunTurboConfiguration From(TurboParameters value) => new RunTurboConfiguration
        {
            durationSeconds = value.DurationSeconds, speedBonusMetresPerSecond = value.SpeedBonus,
            gemMultiplier = value.GemMultiplier, pointMultiplier = value.PointMultiplier
        };

        public string Display() => $"{durationSeconds:0.#} s · +{speedBonusMetresPerSecond:0.#} m/s · gemas ×{gemMultiplier} · puntos ×{pointMultiplier}";
    }

    [Serializable]
    public sealed class RunBrakingConfiguration
    {
        public string mode;
        public float decelerationMetresPerSecondSquared, releaseAccelerationMetresPerSecondSquared;
        public static RunBrakingConfiguration From(BrakingParameters value) => new RunBrakingConfiguration
        {
            mode = value.Mode == BrakingMode.HoldToStop ? "hold_to_stop" : "immediate",
            decelerationMetresPerSecondSquared = value.Deceleration,
            releaseAccelerationMetresPerSecondSquared = value.ReleaseAcceleration
        };
    }

    [Serializable]
    public sealed class RunTelemetrySummary
    {
        public string ending;
        public float timeSeconds, distanceMetres, peakSpeedKmh;
        public long score;
        public int gems, collisions;
        public int voluntaryBoosts, bonusGems;
        public int manualBoosts;
        public int recoveryBoosts;
        public int recoveryAttempts;
        public int brakePresses, brakeReleases;
        public float brakeHeldSeconds;
        public int decisionsShown, decisionsKept;
        public int gemsLost, gemsRecovered, gemBalance, boostCollisions;
        public int gemsSpent, speedUpgrades;
        public int pinkGemsCollected, pinkGemsMissed, pinkGemNearMisses, bestPinkGemStreak;
        public int pinkDodgeCleared, pinkDodgeCollisions;
        public float purchasedSpeedKmh;
    }
}
