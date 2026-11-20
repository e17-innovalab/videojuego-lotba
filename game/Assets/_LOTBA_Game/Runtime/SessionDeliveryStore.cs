using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class SessionDelivery
    {
        public string destination, status = "pending", deliveredAtUtc, lastError;
        public int attempts;
        public long retryAfterUtcTicks;
    }

    [Serializable]
    public sealed class SessionDeliveryRecord
    {
        public string runId, originalDestination;
        public List<SessionDelivery> deliveries = new List<SessionDelivery>();
    }

    /// <summary>Local transport metadata. Never modifies the canonical SessionReport.</summary>
    public sealed class SessionDeliveryStore
    {
        private readonly string directory;
        public SessionDeliveryStore(string directory) { this.directory = directory; }
        private string RecordPath(string reportPath) => reportPath + ".delivery.json";

        public string[] Reports() => Directory.Exists(directory)
            ? Directory.GetFiles(directory, "session_*.json", SearchOption.TopDirectoryOnly)
                .WhereReportFiles()
            : Array.Empty<string>();

        public SessionDeliveryRecord Read(string path)
        {
            if (File.Exists(RecordPath(path)))
            {
                var saved = JsonUtility.FromJson<SessionDeliveryRecord>(File.ReadAllText(RecordPath(path)));
                if (saved == null || saved.deliveries == null) throw new IOException("Estado de envío inválido.");
                return saved;
            }
            var report = JsonUtility.FromJson<SessionReport>(File.ReadAllText(path));
            if (report?.metadata == null || string.IsNullOrWhiteSpace(report.metadata.runId))
                throw new IOException("Reporte sin runId.");
            return new SessionDeliveryRecord { runId = report.metadata.runId };
        }

        public void Write(string path, SessionDeliveryRecord record) =>
            WriteAtomic(RecordPath(path), JsonUtility.ToJson(record, true));

        public void Register(string path, string destination)
        {
            if (File.Exists(RecordPath(path))) return;
            var record = Read(path);
            record.originalDestination = destination;
            if (!string.IsNullOrEmpty(destination)) GetOrAdd(record, destination);
            Write(path, record);
        }

        public static SessionDelivery GetOrAdd(SessionDeliveryRecord record, string destination)
        {
            var delivery = record.deliveries.Find(item => item.destination == destination);
            if (delivery != null) return delivery;
            delivery = new SessionDelivery { destination = destination };
            record.deliveries.Add(delivery);
            return delivery;
        }

        public void Delete(string path)
        {
            File.Delete(path);
            File.Delete(RecordPath(path));
        }

        public static void WriteAtomic(string path, string json)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }

    internal static class SessionReportPaths
    {
        internal static string[] WhereReportFiles(this string[] paths) => Array.FindAll(paths,
            path => !path.EndsWith(".delivery.json", StringComparison.OrdinalIgnoreCase));
    }
}
