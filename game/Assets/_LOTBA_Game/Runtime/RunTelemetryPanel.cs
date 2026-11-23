using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    internal sealed class RunTelemetryPanel
    {
        [SerializeField] private UnityEngine.UI.Button openButton, copyButton, closeButton;
        [SerializeField] private RectTransform panel;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private TMP_Text copyLabel, summaryBody, eventsBody, technicalBody;
        [NonSerialized] private RunTelemetry report, displayedReport;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        public void Bind()
        {
            // Technical reports are centralized in DevelopmentToolsPanel.
            openButton.gameObject.SetActive(false);
            Close();
        }

        public void SetReport(RunTelemetry value)
        {
            report = value;
            openButton.gameObject.SetActive(false);
            Close();
        }

        public void Close()
        {
            if (IsOpen) panel.gameObject.SetActive(false);
        }

        private void Open()
        {
            if (report?.summary == null) return;
            panel.gameObject.SetActive(true);
            copyLabel.text = "Copiar telemetría completa (JSON)";
            if (displayedReport != report)
            {
                PopulateReport();
                displayedReport = report;
            }
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void PopulateReport()
        {
            RunTelemetrySummary result = report.summary;
            var summary = new StringBuilder()
                .AppendLine(Ending(result.ending))
                .AppendLine($"{Highlight(result.score)} pts · {Highlight(result.gems)} gemas · " +
                    $"{Highlight(result.collisions)} choques · {Highlight(result.distanceMetres.ToString("0.0"))} m")
                .AppendLine()
                .AppendLine("Velocidad y tiempo")
                .Append($"{Highlight(result.timeSeconds.ToString("0.0"))} s activos · " +
                    $"{Highlight(result.peakSpeedKmh.ToString("0.0"))} km/h máx.");
            if (report.schemaVersion >= 2)
                summary.Append($"\n\nAceleraciones voluntarias: {Highlight(result.voluntaryBoosts)} · Gemas extra: {Highlight(result.bonusGems)}");
            summaryBody.text = summary.ToString();

            var events = new StringBuilder();
            if (report.schemaVersion >= 5)
                events.AppendLine($"Ofertas mostradas: {Highlight(result.decisionsShown)} · Ritmo mantenido: {Highlight(result.decisionsKept)}");
            if (report.schemaVersion >= 3)
                events.AppendLine($"Gemas recogidas: {Highlight(result.gems)} (perdidas: {Highlight(result.gemsLost)}) · Saldo: {Highlight(result.gemBalance)}");
                events.AppendLine($"Rosas: {Highlight(result.pinkGemsCollected)} · Pasadas: {Highlight(result.pinkGemsMissed)} · Casi: {Highlight(result.pinkGemNearMisses)} · Mejor racha: {Highlight(result.bestPinkGemStreak)}");
                events.AppendLine($"Esquives con rosa: {Highlight(result.pinkDodgeCleared)} superados · {Highlight(result.pinkDodgeCollisions)} choques");
            if (report.schemaVersion >= 9)
                events.AppendLine($"Mejoras de ritmo: {Highlight(result.speedUpgrades)} · Gemas gastadas: {Highlight(result.gemsSpent)} · " +
                    $"Aumento permanente: {Highlight($"+{result.purchasedSpeedKmh:0.#} km/h")}");
            if (report.schemaVersion >= 10)
                events.AppendLine($"Aceleraciones con el botón: {Highlight(result.manualBoosts)} · Choques con impulso: {Highlight(result.boostCollisions)}");
            else if (report.schemaVersion >= 3)
                events.AppendLine($"Choques con impulso: {Highlight(result.boostCollisions)}");
            if (report.schemaVersion >= 13)
            {
                events.AppendLine($"Recuperos logrados: {Highlight(result.recoverySucceeded)} / {result.recoveryAttempts} · " +
                    $"Reintegrado: +{result.pointsRecovered} pts · Perdido en atajo: -{result.recoveryZonePointsLost} pts (Neto: {result.recoveryNetPoints:+#;-#;0} pts)");
            }
            if (report.schemaVersion >= 20)
            {
                if (report.braking != null)
                    events.AppendLine($"Freno: {report.braking.mode} · Pulsaciones: {result.brakePresses} · Soltadas: {result.brakeReleases} · Mantenido: {result.brakeHeldSeconds:0.#} s");
                foreach (var entry in report.events)
                    if (entry.type == "boost_activated" && entry.turboConfiguration != null)
                        events.AppendLine($"Turbo · {entry.timeSeconds:0.#} s: {entry.turboConfiguration.Display()}");
            }
            foreach (var entry in report.events)
                if (entry.type == "track_reset" && entry.trackReset != null)
                    events.AppendLine($"Limpieza: {entry.trackReset.Display()}");
            if (report.recoveryOpportunities != null)
                foreach (var opportunity in report.recoveryOpportunities) events.AppendLine(opportunity.Display());
            if (report.schemaVersion >= 20)
                events.AppendLine($"Recuperado: {result.pointsRecovered} puntos / {result.gemsRecovered} gemas · Tiempo real: {result.realTimeSeconds:0.#} s");
            eventsBody.transform.parent.gameObject.SetActive(events.Length > 0);
            eventsBody.text = events.ToString().TrimEnd();

            string routeLength = report.schemaVersion < 7 ? $"Tiempo previsto: {report.durationSeconds:0} s" :
                report.routeDistanceMetres > 0f ? $"Distancia de ruta: {report.routeDistanceMetres:0} m" : "Ruta continua";
            technicalBody.text = $"Ruta: {report.route} · Semilla: {report.seed}\n" +
                $"Versión: {report.gameVersion} · {RunTelemetry.SchemaLabel(report.schemaVersion)} · {routeLength}\nID partida: {report.runId}";
        }

        private static string Highlight(object value) => $"<color=#FFD870><b>{value}</b></color>";

        private void Copy()
        {
            if (report?.summary == null) return;
            GUIUtility.systemCopyBuffer = report.ToJson();
            copyLabel.text = "JSON copiado";
        }

        private static string Ending(string reason) => reason == "brake" ? "Frenado voluntario" :
            reason == "overload" ? "Corte por sobrecarga" :
            reason == "route_complete" ? "Recorrido completado" : "Fin por tiempo";

    }
}
