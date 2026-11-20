using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Builds and atomically persists local offline SessionReport v1 JSON files.</summary>
    public static class SessionReportWriter
    {
        public const string ReportVersion = "1.0";
        private const string FolderName = "SessionReports";

        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, FolderName);

        public static SessionReport Build(RunTelemetry telemetry)
        {
            if (telemetry == null || telemetry.summary == null) return null;

            return new SessionReport
            {
                metadata = new SessionReportMetadata
                {
                    reportVersion = ReportVersion,
                    schemaVersion = telemetry.schemaVersion,
                    runId = telemetry.runId,
                    gameVersion = telemetry.gameVersion,
                    platform = Application.platform.ToString(),
                    startedAtUtc = telemetry.startedAtUtc,
                    finishedAtUtc = telemetry.finishedAtUtc,
                    route = telemetry.route,
                    seed = telemetry.seed,
                    routeDistanceMetres = telemetry.routeDistanceMetres
                },
                summary = telemetry.summary,
                events = telemetry.events != null
                    ? new List<RunTelemetryEvent>(telemetry.events)
                    : new List<RunTelemetryEvent>()
            };
        }

        public static string Save(RunTelemetry telemetry, string directory = null)
        {
            try
            {
                SessionReport report = Build(telemetry);
                if (report == null) return null;

                string targetDirectory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;
                Directory.CreateDirectory(targetDirectory);

                string safeRunId = SanitizeRunId(report.metadata.runId);
                string filePath = Path.Combine(targetDirectory, $"session_{safeRunId}.json");
                if (File.Exists(filePath))
                {
                    Debug.LogWarning($"[SessionReportWriter] Report file already exists and will not be overwritten: {filePath}");
                    return filePath;
                }

                string json = JsonUtility.ToJson(report, true);
                SessionDeliveryStore.WriteAtomic(filePath, json);
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionReportWriter] Failed to save local session report: {ex}");
                return null;
            }
        }

        private static string SanitizeRunId(string runId)
        {
            string value = string.IsNullOrWhiteSpace(runId) ? Guid.NewGuid().ToString("N") : runId.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }
    }
}
