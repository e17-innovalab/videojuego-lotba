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
        public List<RunRecoveryOpportunity> recoveryOpportunities = new List<RunRecoveryOpportunity>();
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
        public int transitionSection = -1;
        public RunRecoveryOpportunity recovery;
        public string lossOrigin;
        public int gemRewardMultiplier, pointRewardMultiplier;
    }

    [Serializable]
    public sealed class RunRecoveryOpportunity
    {
        public string offerId, attemptId, outcome, cause;
        public int section, decisionNumber;
        public bool accepted;
        public int[] eligibleLossSequences;
        public long eligiblePoints, offeredPoints, returnedPoints;
        public int eligibleGems, offeredGems, returnedGems;
        public float offerDistanceMetres, startDistanceMetres, endDistanceMetres, challengeMetres;
        public float startTimeSeconds, endTimeSeconds, fogLocalZ;
        public float pointLossFraction, gemLossFraction;
        public int minimumPointLoss;
        public RunTurboConfiguration turbo;
        public RunRecoveryOpportunity Copy() => (RunRecoveryOpportunity)MemberwiseClone();
        public string Display() => $"{offerId} · {attemptId} · tramo {section} · {outcome} ({cause}) · ofrecido {offeredPoints} puntos / {offeredGems} gemas · devuelto {returnedPoints} / {returnedGems} · reto {challengeMetres:0.#} m";
    }

    [Serializable]
    public sealed class RunTurboConfiguration
    {
        public string lifetimeMode;
        public float durationSeconds, speedBonusMetresPerSecond;
        public int gemMultiplier, pointMultiplier;

        public static RunTurboConfiguration From(TurboParameters value, bool externalLifetime = false) => new RunTurboConfiguration
        {
            lifetimeMode = externalLifetime ? "external_distance_challenge" : "timed",
            durationSeconds = externalLifetime ? 0f : value.DurationSeconds, speedBonusMetresPerSecond = value.SpeedBonus,
            gemMultiplier = value.GemMultiplier, pointMultiplier = value.PointMultiplier
        };

        public string Display() => (lifetimeMode == "external_distance_challenge" ? "Hasta resolver el reto" : $"{durationSeconds:0.#} s") + $" · +{speedBonusMetresPerSecond:0.#} m/s · gemas ×{gemMultiplier} · puntos ×{pointMultiplier}";
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
        public int recoverySucceeded, recoveryFailed, recoveryDeclined, recoveryInterrupted;
        public int recoveryOffers, recoveryUnavailable, recoveryOffersClosed;
        public int recoveryZoneCollisions;
        public long recoveryZonePointsLost;
        public int recoveryZoneGemsLost;
        public long recoveryNetPoints;
        public int recoveryNetGems;
        public int purchasedGemMultiplier, purchasedPointMultiplier;
        public long pointsRecovered;
        public long pointsLost;
        public float realTimeSeconds;
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
