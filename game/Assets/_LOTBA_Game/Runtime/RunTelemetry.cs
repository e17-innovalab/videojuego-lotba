using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class RunTelemetry
    {
        public int schemaVersion = 5;
        public string runId = Guid.NewGuid().ToString("N");
        public string gameVersion = Application.version;
        public string route;
        public int seed;
        public float durationSeconds;
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
        public int multiplierBefore, multiplier;
        public float multiplierProgress;
        public int decisionNumber;
    }

    [Serializable]
    public sealed class RunTelemetrySummary
    {
        public string ending;
        public float timeSeconds, distanceMetres, peakSpeedKmh;
        public long score;
        public int gems, collisions, bestStreak, bestMultiplier;
        public int voluntaryBoosts, bonusGems;
        public int decisionsShown, decisionsKept;
        public int gemsLost, gemBalance, boostCollisions;
    }
}
