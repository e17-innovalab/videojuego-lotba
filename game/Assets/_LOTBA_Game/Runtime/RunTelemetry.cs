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
        public int recoveryAttempts;
        public int decisionsShown, decisionsKept;
        public int gemsLost, gemsRecovered, gemBalance, boostCollisions;
        public int gemsSpent, speedUpgrades;
        public int pinkGemsCollected, pinkGemsMissed, pinkGemNearMisses, bestPinkGemStreak;
        public int pinkDodgeCleared, pinkDodgeCollisions;
        public float purchasedSpeedKmh;
    }
}
