using System;
using System.Collections.Generic;

namespace Lotba.Game
{
    [Serializable]
    public sealed class SessionReport
    {
        public SessionReportMetadata metadata;
        public RunTelemetrySummary summary;
        public List<RunTelemetryEvent> events = new List<RunTelemetryEvent>();
    }

    [Serializable]
    public sealed class SessionReportMetadata
    {
        public string reportVersion = "1.0";
        public int schemaVersion;
        public string runId;
        public string gameVersion;
        public string platform;
        public string startedAtUtc;
        public string finishedAtUtc;
        public string route;
        public int seed;
        public float routeDistanceMetres;
        public RunBrakingConfiguration braking;
    }
}
