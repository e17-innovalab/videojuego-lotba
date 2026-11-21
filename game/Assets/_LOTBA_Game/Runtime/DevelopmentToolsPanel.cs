#if UNITY_EDITOR || DEVELOPMENT_BUILD || LOTBA_DEVELOPMENT_TOOLS
using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Temporary internal UI. Excluded from ordinary release builds.</summary>
    public sealed class DevelopmentToolsPanel : MonoBehaviour
    {
        private SessionReportTransport transport;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private GameObject overlay, confirmation, pagination, detailSection, emptyState, sessionsSection, availableMetrics;
        [SerializeField] private UnityEngine.UI.Button launcher, closeButton, previousPage, nextPage, sendPending, resend, deleteLocal, saveEndpoint, testConnection, backToReports, copySelected, confirmButton, cancelButton, recordedMetricsButton, availableMetricsButton;
        [SerializeField] private UnityEngine.UI.ScrollRect bodyScroll;
        [SerializeField] private RectTransform saveIcon, rows;
        [SerializeField] private TMP_InputField endpoint;
        [SerializeField] private TMP_Text status, details, confirmText, pageLabel, reportCount, savedState, connectionResult, recordedMetrics, actionStatus;
        private string selected;
        private int page;
        private Action confirmed;
        public bool IsOpen => overlay != null && overlay.activeSelf;
        private readonly Color background = new Color32(12, 28, 40, 255);
        private readonly Color muted = new Color32(156, 180, 193, 255);
        private readonly Color accent = new Color32(103, 215, 202, 255);

        public void Initialize(SessionReportTransport sender)
        {
            transport = sender;
            if (transport == null) return;
            if (overlay == null)
            {
                Debug.LogError("Development tools hierarchy is missing from the Game UI prefab.", this);
                return;
            }
            BindExisting();
        }

        private void BindExisting()
        {
            if (availableMetrics != null)
                foreach (var label in availableMetrics.GetComponentsInChildren<TMP_Text>(true))
                    if (label.text.StartsWith("Datos v"))
                        label.text = RunTelemetry.SchemaLabel(RunTelemetry.CurrentSchemaVersion) +
                            "\nDecisiones · aceleraciones · compras\nChoques · puntos · gemas · rosas / CASI\nLetras · final de partida" +
                            "\nRecupero: intentos, resultados y gemas devueltas.\nSin datos personales.";
            if (launcher == null || closeButton == null || endpoint == null)
            {
                Debug.LogError("Development tools hierarchy is not fully assigned in the Game UI prefab.", this);
                return;
            }
            endpoint.keyboardType = TouchScreenKeyboardType.Default;
            endpoint.shouldHideMobileInput = false;
            launcher.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            endpoint.onValueChanged.AddListener(_ => UpdateEndpointState());
            saveEndpoint.onClick.AddListener(() =>
            {
                CancelConfirmation();
                if (transport.Configure(endpoint.text)) endpoint.text = transport.BaseUrl;
                UpdateEndpointState();
            });
            testConnection.onClick.AddListener(() => transport.TestConnection(endpoint.text));
            sendPending.onClick.AddListener(transport.SendPending);
            previousPage.onClick.AddListener(() => { page = Math.Max(0, page - 1); Refresh(); });
            nextPage.onClick.AddListener(() => { page++; Refresh(); });
            backToReports.onClick.AddListener(() => { selected = null; CancelConfirmation(); Refresh(); ResetScroll(); });
            copySelected.onClick.AddListener(CopySelected);
            resend.onClick.AddListener(() => ConfirmSelected(false));
            deleteLocal.onClick.AddListener(() => ConfirmSelected(true));
            confirmButton.onClick.AddListener(() => { var action = confirmed; CancelConfirmation(); action?.Invoke(); });
            cancelButton.onClick.AddListener(CancelConfirmation);
            recordedMetricsButton.onClick.AddListener(() => recordedMetrics.gameObject.SetActive(!recordedMetrics.gameObject.activeSelf));
            availableMetricsButton.onClick.AddListener(() => availableMetrics.SetActive(!availableMetrics.activeSelf));
            if (transport != null) transport.Changed -= Refresh;
            transport.Changed += Refresh;
            overlay.SetActive(false);
        }

        private void Open()
        {
            endpoint.text = transport.BaseUrl;
            selected = null;
            overlay.SetActive(true); overlay.transform.SetAsLastSibling();
            Refresh();
            ResetScroll();
        }
        public void Close() { CancelConfirmation(); if (overlay != null) overlay.SetActive(false); }
        private void CancelConfirmation() { confirmed = null; if (confirmation != null) confirmation.SetActive(false); }
        private void ConfirmSelected(bool delete)
        {
            if (selected == null) return;
            ConfirmReport(selected, delete);
        }
        private void ConfirmReport(string path, bool delete)
        {
            string destination = transport.BaseUrl;
            if (!delete && string.IsNullOrEmpty(destination)) { actionStatus.gameObject.SetActive(true); actionStatus.text = "Guardá una URL antes de enviar."; return; }
            confirmText.text = delete
                ? "¿Borrar esta partida del dispositivo? No se elimina del servidor."
                : "¿Enviar esta partida a " + destination + "?";
            confirmed = () =>
            {
                if (delete) { transport.Delete(path); if (selected == path && !File.Exists(path)) selected = null; }
                else if (destination == transport.BaseUrl) transport.SendSelected(path);
                Refresh();
            };
            confirmation.SetActive(true);
            confirmation.transform.SetAsLastSibling();
        }

        private void ResetScroll() { Canvas.ForceUpdateCanvases(); bodyScroll.StopMovement(); bodyScroll.verticalNormalizedPosition = 1f; }
        private void UpdateEndpointState()
        {
            if (savedState == null || saveEndpoint == null) return;
            bool valid = SessionReportTransport.TryNormalize(endpoint.text, out string normalized, out _);
            bool saved = valid && normalized == transport.BaseUrl;
            bool configured = saved && !string.IsNullOrEmpty(normalized);
            savedState.text = configured ? "Guardado" : saved ? "Sin configurar" : "Sin guardar";
            savedState.color = configured ? accent : new Color32(245, 192, 93, 255);
            saveEndpoint.interactable = !saved;
            foreach (Transform child in saveIcon) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (configured)
            {
                Stroke(saveIcon, -4, -2, 9, 2, -45, accent); Stroke(saveIcon, 3, 1, 15, 2, 45, accent);
            }
            else
            {
                var warning = savedState.color;
                Stroke(saveIcon, -5, 0, 18, 2, 60, warning); Stroke(saveIcon, 5, 0, 18, 2, -60, warning);
                Stroke(saveIcon, 0, -8, 18, 2, 0, warning); Stroke(saveIcon, 0, 0, 2, 6, 0, warning); Stroke(saveIcon, 0, -5, 2, 2, 0, warning);
            }
            bool showResult = transport.ConnectionTestUrl == endpoint.text && !string.IsNullOrEmpty(transport.ConnectionTestMessage);
            connectionResult.gameObject.SetActive(showResult);
            connectionResult.text = transport.ConnectionTestMessage;
            connectionResult.color = transport.ConnectionTestInProgress ? muted : transport.ConnectionTestSucceeded == true ? accent : new Color32(255, 139, 144, 255);
            testConnection.interactable = !transport.ConnectionTestInProgress;
        }

        private void CopySelected()
        {
            if (selected == null) return;
            actionStatus.gameObject.SetActive(true);
            try { GUIUtility.systemCopyBuffer = File.ReadAllText(selected); actionStatus.text = "JSON copiado."; }
            catch (Exception ex) { actionStatus.text = ex.Message; }
        }

        private void Refresh()
        {
            if (!IsOpen) return;
            UpdateEndpointState();
            string message = transport.LastMessage;
            bool relevant = !message.StartsWith("Destino guardado") && message != "Sin envío en curso.";
            status.gameObject.SetActive(relevant);
            status.text = "Envíos: " + FriendlyMessage(message);
            actionStatus.gameObject.SetActive(relevant);
            actionStatus.text = FriendlyMessage(message);
            foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            try
            {
                string[] files = transport.Store.Reports(); Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                page = Math.Min(page, Math.Max(0, (files.Length - 1) / 4));
                reportCount.text = files.Length == 1 ? "1 partida en este dispositivo" : $"{files.Length} partidas en este dispositivo";
                pageLabel.text = $"{page + 1} / {Math.Max(1, (files.Length + 3) / 4)}";
                pagination.SetActive(files.Length > 4); previousPage.interactable = page > 0; nextPage.interactable = (page + 1) * 4 < files.Length;
                emptyState.SetActive(files.Length == 0); sendPending.gameObject.SetActive(files.Length > 0);
                sendPending.interactable = !string.IsNullOrEmpty(transport.BaseUrl);
                for (int i = page * 4; i < Math.Min(files.Length, page * 4 + 4); i++)
                {
                    string path = files[i];
                    string state;
                    string id;
                    Color stateColor;
                    try
                    {
                        var record = transport.Store.Read(path);
                        bool delivered = record.deliveries.Exists(item => !string.IsNullOrEmpty(item.deliveredAtUtc));
                        bool rejected = record.deliveries.Exists(item => item.status == "rejected");
                        var current = record.deliveries.Find(item => item.destination == transport.BaseUrl);
                        state = current != null
                            ? current.status == "delivered" ? "ENTREGADA" : current.status == "rejected" ? "RECHAZADA" : "PENDIENTE"
                            : delivered ? "ENTREGADA EN OTRO DESTINO" : rejected ? "RECHAZADA EN OTRO DESTINO" : "PENDIENTE";
                        if (transport.ActiveReport == path) state = "ENVIANDO";
                        id = record.runId;
                        stateColor = state.StartsWith("ENTREGADA") ? accent : state.StartsWith("RECHAZADA") ? new Color32(255, 139, 144, 255) : new Color32(245, 192, 93, 255);
                    }
                    catch (Exception) { state = "ERROR DE ARCHIVO"; id = Path.GetFileNameWithoutExtension(path); stateColor = new Color32(255, 139, 144, 255); }
                    ReportItem(path, id, state, stateColor);
                }
                PopulateDetails();
            }
            catch (Exception ex) { details.text = ex.Message; }
        }

        private void PopulateDetails()
        {
            bool available = selected != null && File.Exists(selected);
            if (!available) selected = null;
            detailSection.SetActive(available);
            sessionsSection.SetActive(!available);
            if (!available) return;
            bool busy = transport.ActiveReport == selected;
            resend.interactable = !busy && !string.IsNullOrEmpty(transport.BaseUrl); deleteLocal.interactable = !busy;
            var report = JsonUtility.FromJson<SessionReport>(File.ReadAllText(selected));
            var record = transport.Store.Read(selected);
            var text = new StringBuilder().AppendLine($"Versión {report.metadata.gameVersion} · {RunTelemetry.SchemaLabel(report.metadata.schemaVersion)}")
                .AppendLine($"{report.summary.timeSeconds:0.0} s activos · {report.events.Count} eventos · {new FileInfo(selected).Length / 1024f:0.0} KB")
                .AppendLine("ID: " + record.runId).AppendLine()
                .AppendLine($"{report.summary.score} puntos · {report.summary.gems} gemas · {report.summary.collisions} choques")
                .AppendLine($"Final: {Ending(report.summary.ending)}").AppendLine();
            if (record.deliveries.Count == 0) text.AppendLine("Sin destino asignado.");
            foreach (var delivery in record.deliveries)
            {
                text.AppendLine(delivery.destination).AppendLine($"{DeliveryStatus(delivery.status)} · {delivery.attempts} intentos");
                if (!string.IsNullOrEmpty(delivery.deliveredAtUtc)) text.AppendLine("Entregada: " + delivery.deliveredAtUtc);
                if (!string.IsNullOrEmpty(delivery.lastError)) text.AppendLine(delivery.lastError);
                text.AppendLine();
            }
            details.text = text.ToString();
            var measured = new StringBuilder();
            foreach (var field in typeof(RunTelemetrySummary).GetFields())
                measured.AppendLine(field.Name + ": " + field.GetValue(report.summary));
            recordedMetrics.text = measured.ToString();
        }

        private static string DeliveryStatus(string value) => value == "delivered" ? "Entregada" : value == "rejected" ? "Rechazada" : "Pendiente";
        private static string Ending(string value) => value == "brake" ? "Frenado voluntario" : value == "overload" ? "Sobrecarga" : value == "route_complete" ? "Recorrido completado" : value;
        private static string FriendlyMessage(string value)
        {
            if (value.StartsWith("Destino guardado")) return "Dirección guardada.";
            if (value.StartsWith("Health accesible")) return "Servidor accesible.";
            if (value.StartsWith("Pendientes habilitados")) return "Envío de pendientes solicitado.";
            if (value.StartsWith("Envío solicitado")) return "Envío solicitado.";
            if (value.StartsWith("Entrega confirmada")) return "Último envío confirmado.";
            if (value.StartsWith("Enviando ")) return "Enviando partida…";
            return value;
        }

        private RectTransform Section(Transform parent, string title)
        {
            var rect = Rect(title, parent);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(19, 40, 53, 255);
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 12; layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            Text(rect, title, 24);
            return rect;
        }

        private static string Short(string id) => id != null && id.Length > 10 ? id.Substring(0, 10) : id;
        private void ReportItem(string path, string id, string state, Color stateColor)
        {
            var row = Row(rows); row.name = "ReportItem";
            var card = Button(row, "", () => { selected = path; CancelConfirmation(); Refresh(); ResetScroll(); });
            card.name = "OpenReport";
            card.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
            card.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 60;
            card.GetComponent<UnityEngine.UI.Image>().color = background;
            var border = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            border.effectColor = new Color32(73, 105, 121, 255); border.effectDistance = Vector2.one;
            var content = card.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            content.padding = new RectOffset(12, 10, 7, 7); content.spacing = 8;
            content.childControlWidth = true; content.childControlHeight = true; content.childForceExpandHeight = false;
            var info = Rect("Info", card.transform);
            info.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
            var labels = info.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            labels.spacing = 2; labels.childControlWidth = true; labels.childControlHeight = true; labels.childForceExpandHeight = false;
            Text(info, File.GetLastWriteTime(path).ToString("dd/MM HH:mm") + " · " + Short(id), 17).color = muted;
            Text(info, state, 17).color = stateColor;
            var arrow = Text(card.transform, ">", 26); arrow.color = muted; arrow.alignment = TextAlignmentOptions.Center;
            var arrowSize = arrow.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); arrowSize.preferredWidth = 20; arrowSize.flexibleWidth = 0;
            var trash = Button(row, "", () => ConfirmReport(path, true)); trash.name = "DeleteReport";
            trash.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
            var trashSize = trash.GetComponent<UnityEngine.UI.LayoutElement>(); trashSize.minHeight = 60; trashSize.preferredWidth = 42; trashSize.flexibleWidth = 0;
            trash.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0);
            trash.interactable = transport.ActiveReport != path;
            var tint = new Color32(216, 137, 147, 255);
            Stroke((RectTransform)trash.transform, 0, 10, 22, 2, 0, tint);
            Stroke((RectTransform)trash.transform, 0, 14, 9, 2, 0, tint);
            Stroke((RectTransform)trash.transform, -8, -2, 2, 20, 0, tint);
            Stroke((RectTransform)trash.transform, 8, -2, 2, 20, 0, tint);
            Stroke((RectTransform)trash.transform, 0, -12, 18, 2, 0, tint);
            Stroke((RectTransform)trash.transform, -3, -2, 2, 13, 0, tint);
            Stroke((RectTransform)trash.transform, 3, -2, 2, 13, 0, tint);
        }
        private void Stroke(RectTransform parent, float x, float y, float width, float height, float angle, Color color)
        {
            var rect = Rect("IconStroke", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            var line = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); line.color = color; line.raycastTarget = false;
        }
        private UnityEngine.UI.Button Link(Transform parent, string value, Action action)
        {
            var rect = Rect("Link", parent);
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); size.minHeight = 44; size.flexibleWidth = 1;
            var label = Text(rect, value, 19); Stretch((RectTransform)label.transform);
            label.richText = true; label.text = "<u>" + value + "</u>"; label.color = accent;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = true;
            var link = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); link.targetGraphic = label;
            link.onClick.AddListener(() => action()); return link;
        }
        private RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private TMP_Text Text(Transform parent, string value, int size)
        {
            var rect = Rect("Label", parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.text = value; text.color = Color.white;
            text.richText = false; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
        private UnityEngine.UI.Button Button(Transform parent, string value, Action action, bool primary = false)
        {
            var rect = Rect("Button", parent);
            var sizing = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            sizing.minHeight = 56; sizing.flexibleWidth = 1;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = primary ? accent : new Color32(37, 64, 79, 255);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Text(rect, value, 20); Stretch((RectTransform)label.transform);
            label.color = primary ? background : Color.white;
            label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => action()); return button;
        }
        private RectTransform Row(Transform parent)
        {
            var rect = Rect("Actions", parent); var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false; return rect;
        }
        private TMP_InputField Input(Transform parent)
        {
            var rect = Rect("BaseUrl", parent);
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = 58;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color32(24, 52, 65, 255);
            var input = rect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = image;
            var viewport = Rect("TextArea", rect); Stretch(viewport); viewport.offsetMin = new Vector2(12, 6); viewport.offsetMax = new Vector2(-12, -6);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = Text(viewport, "", 21); Stretch((RectTransform)text.transform); text.raycastTarget = true;
            var placeholder = Text(viewport, "https://servidor", 21); Stretch((RectTransform)placeholder.transform);
            placeholder.color = Color.gray;
            input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)text; input.placeholder = placeholder;
            input.characterLimit = 2048; input.lineType = TMP_InputField.LineType.SingleLine;
            input.keyboardType = TouchScreenKeyboardType.URL; input.richText = false;
            return input;
        }
        private void OnDestroy() { if (transport != null) transport.Changed -= Refresh; }
    }
}
#else
namespace Lotba.Game
{
    /// <summary>Empty release-build counterpart so the serialized prefab has no missing script.</summary>
    public sealed class DevelopmentToolsPanel : UnityEngine.MonoBehaviour
    {
        public bool IsOpen => false;
        public void Close() { }
    }
}
#endif
