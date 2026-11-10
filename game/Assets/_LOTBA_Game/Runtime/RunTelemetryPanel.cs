using System;
using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    internal sealed class RunTelemetryPanel
    {
        private readonly UnityEngine.UI.Button openButton, buttonTemplate;
        private readonly UnityEngine.UI.Image backgroundTemplate;
        private readonly TMP_FontAsset font;
        private RectTransform panel, content;
        private UnityEngine.UI.ScrollRect scroll;
        private TMP_Text copyLabel;
        private RunTelemetry report, displayedReport;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        public RunTelemetryPanel(GameObject results, TMP_Text resultStats, UnityEngine.UI.Button menu)
        {
            buttonTemplate = menu;
            backgroundTemplate = results.GetComponent<UnityEngine.UI.Image>();
            font = resultStats.font;
            var stats = resultStats.rectTransform;
            stats.anchorMin = new Vector2(stats.anchorMin.x, .345f);
            openButton = AddButton("Ver telemetría", results.transform, new Vector2(.07f, .24f),
                new Vector2(.93f, .325f), Open, out _);
            openButton.gameObject.SetActive(false);
        }

        public void SetReport(RunTelemetry value)
        {
            report = value;
            bool available = report?.summary != null;
            if (openButton.gameObject.activeSelf != available) openButton.gameObject.SetActive(available);
            if (!available) Close();
        }

        public void Close()
        {
            if (IsOpen) panel.gameObject.SetActive(false);
        }

        private void Open()
        {
            if (report?.summary == null) return;
            if (panel == null) BuildPanel();
            panel.gameObject.SetActive(true);
            copyLabel.text = "Copiar telemetría completa (JSON)";
            if (displayedReport != report)
            {
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var row = content.GetChild(i).gameObject;
                    row.SetActive(false);
                    UnityEngine.Object.Destroy(row);
                }
                PopulateReport();
                displayedReport = report;
            }
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void BuildPanel()
        {
            panel = CreateRect("Telemetría", backgroundTemplate.transform.parent, new Vector2(.055f, .07f), new Vector2(.945f, .93f));
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.sprite = backgroundTemplate.sprite;
            background.type = backgroundTemplate.type;
            background.color = backgroundTemplate.color;
            TMP_Text title = AddText(panel, "Telemetría de la partida", 30f, true);
            SetRect(title.rectTransform, new Vector2(.06f, .89f), new Vector2(.94f, .98f));
            title.alignment = TextAlignmentOptions.Center;

            RectTransform scrollRoot = CreateRect("Registro", panel, new Vector2(.06f, .145f), new Vector2(.94f, .88f));
            scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = CreateRect("Content", viewport, Vector2.up, Vector2.one);
            content.pivot = new Vector2(.5f, 1f);
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 8, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            AddButton("Copiar telemetría completa (JSON)", panel, new Vector2(.06f, .035f), new Vector2(.68f, .12f), Copy, out copyLabel);
            copyLabel.enableAutoSizing = true;
            copyLabel.fontSizeMin = 14f;
            copyLabel.fontSizeMax = 22f;
            AddButton("Cerrar", panel, new Vector2(.72f, .035f), new Vector2(.94f, .12f), Close, out _);
        }

        private void PopulateReport()
        {
            RunTelemetrySummary result = report.summary;
            AddText(content, "RESUMEN", 23f, true);
            AddText(content, $"{Ending(result.ending)}\n{result.score} puntos · {result.gems} gemas · {result.collisions} choques\n" +
                $"{result.distanceMetres:0.0} m · {result.timeSeconds:0.0} s activos · {result.peakSpeedKmh:0.0} km/h máx.\n" +
                $"Mejor racha: {result.bestStreak} · Mejor multiplicador: ×{result.bestMultiplier}", 19f);
            AddText(content, $"Ruta: {report.route}\nVersión: {report.gameVersion} · Esquema: {report.schemaVersion}\n" +
                $"Semilla: {report.seed} · Tiempo previsto: {report.durationSeconds:0} s\nPartida: {report.runId}", 17f);
        }

        private void Copy()
        {
            if (report?.summary == null) return;
            GUIUtility.systemCopyBuffer = report.ToJson();
            copyLabel.text = "JSON copiado";
        }

        private UnityEngine.UI.Button AddButton(string title, Transform parent, Vector2 min, Vector2 max, Action onClick, out TMP_Text label)
        {
            var button = UnityEngine.Object.Instantiate(buttonTemplate, parent, false);
            button.name = title;
            SetRect((RectTransform)button.transform, min, max);
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.onClick.AddListener(() => onClick());
            label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = title;
            return button;
        }

        private TMP_Text AddText(Transform parent, string text, float size, bool bold = false)
        {
            RectTransform rect = CreateRect("Texto", parent, Vector2.zero, Vector2.one);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.richText = false;
            label.raycastTarget = false;
            label.color = bold ? new Color32(255, 209, 76, 255) : Color.white;
            label.text = text;
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            SetRect(rect, min, max);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static string Ending(string reason) => reason == "brake" ? "Frenado voluntario" : "Fin por tiempo";

    }
}
