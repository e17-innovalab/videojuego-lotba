using System;
using System.IO;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class JourneySession
    {
        public string journeyId;
        public string schoolName;
        public string schoolShift;
        public string schoolDate;
        public string startedAtUtc;

        public SessionJourneyContext Snapshot() => new SessionJourneyContext
        {
            journeyId = journeyId,
            schoolName = schoolName,
            schoolShift = schoolShift,
            schoolDate = schoolDate
        };
    }

    /// <summary>Persists the currently open school visit independently of any individual run.</summary>
    public static class JourneySessionStore
    {
        private const string FileName = "active_journey.json";
        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static JourneySession Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                JourneySession session = JsonUtility.FromJson<JourneySession>(File.ReadAllText(FilePath));
                return session != null && !string.IsNullOrWhiteSpace(session.journeyId) &&
                       !string.IsNullOrWhiteSpace(session.schoolName) ? session : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Journey] No se pudo leer la jornada activa: " + ex.Message);
                return null;
            }
        }

        public static JourneySession Begin(string schoolName, string shift, DateTime localDate)
        {
            var session = new JourneySession
            {
                journeyId = Guid.NewGuid().ToString("N"),
                schoolName = schoolName.Trim(),
                schoolShift = shift,
                schoolDate = localDate.ToString("yyyy-MM-dd"),
                startedAtUtc = DateTime.UtcNow.ToString("O")
            };
            Save(session);
            return session;
        }

        public static void End()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception ex) { Debug.LogError("[Journey] No se pudo cerrar la jornada: " + ex.Message); }
        }

        private static void Save(JourneySession session)
        {
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(session, true));
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
    }
}
