using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Lotba.Game
{
    /// <summary>One durable report per run; delivery receipts are local and destination-specific.</summary>
    public sealed class SessionReportTransport : MonoBehaviour
    {
        // Set the final HTTPS base URL here before publishing. Empty disables automatic delivery.
        public const string DefaultBaseUrl = "";
        private const string EndpointPreference = "Lotba.Telemetry.BaseUrl";
        public const string SessionsRoute = "/api/v1/sessions";
        public const int MaximumPayloadBytes = 2 * 1024 * 1024;
        public SessionDeliveryStore Store { get; private set; }
        public string BaseUrl { get; private set; }
        public string ActiveReport { get; private set; }
        public string LastMessage { get; private set; } = "Sin envío en curso.";
        public string ConnectionTestUrl { get; private set; }
        public string ConnectionTestMessage { get; private set; }
        public bool? ConnectionTestSucceeded { get; private set; }
        public bool ConnectionTestInProgress => healthCheck;
        public event Action Changed;
        private bool healthCheck;

        [Serializable] private sealed class Receipt { public string status, runId; }

        private void Awake()
        {
            Store = new SessionDeliveryStore(SessionReportWriter.DefaultDirectory);
            BaseUrl = DefaultBaseUrl;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || LOTBA_DEVELOPMENT_TOOLS
            string configured = PlayerPrefs.GetString(EndpointPreference, DefaultBaseUrl);
            if (TryNormalize(configured, out string normalized, out _)) BaseUrl = normalized;
#endif
            StartCoroutine(Pump());
        }

        public static bool TryNormalize(string value, out string normalized, out string error)
        {
            normalized = (value ?? "").Trim().TrimEnd('/');
            error = null;
            if (normalized.Length == 0) return true;
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            { error = "Usá una URL base HTTP(S), sin claves, query ni fragmento."; return false; }
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && !LOTBA_DEVELOPMENT_TOOLS
            if (uri.Scheme != "https") { error = "La entrega final requiere HTTPS."; return false; }
#endif
            if (normalized.EndsWith(SessionsRoute, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - SessionsRoute.Length);
            return true;
        }

        public bool Configure(string value)
        {
            if (!TryNormalize(value, out string normalized, out string error))
            { Notify(error); return false; }
            BaseUrl = normalized;
            PlayerPrefs.SetString(EndpointPreference, BaseUrl);
            PlayerPrefs.Save();
            Notify("Destino guardado. No redirige partidas anteriores.");
            return true;
        }

        public void TrackSaved(string path)
        {
            try { Store.Register(path, BaseUrl); Notify("Partida guardada localmente."); }
            catch (Exception ex) { Notify("Reporte guardado, estado pendiente de reparar: " + ex.Message); }
        }

        // Explicit action: bind legacy/unassigned reports or retry only this destination.
        public void SendPending()
        {
            if (string.IsNullOrEmpty(BaseUrl)) { Notify("Configurá primero el destino."); return; }
            try
            {
                foreach (string path in Store.Reports())
                {
                    var record = Store.Read(path);
                    if (string.IsNullOrEmpty(record.originalDestination) && record.deliveries.Count == 0)
                        record.originalDestination = BaseUrl;
                    if (record.originalDestination != BaseUrl &&
                        !record.deliveries.Exists(item => item.destination == BaseUrl)) continue;
                    var delivery = SessionDeliveryStore.GetOrAdd(record, BaseUrl);
                    if (delivery.status == "delivered") continue;
                    delivery.status = "pending";
                    delivery.retryAfterUtcTicks = 0;
                    Store.Write(path, record);
                }
                Notify("Pendientes habilitados para el destino seleccionado.");
            }
            catch (Exception ex) { Notify(ex.Message); }
        }

        public void SendSelected(string path)
        {
            if (ActiveReport == path || string.IsNullOrEmpty(BaseUrl)) return;
            try
            {
                var record = Store.Read(path);
                var delivery = SessionDeliveryStore.GetOrAdd(record, BaseUrl);
                delivery.status = "pending";
                delivery.retryAfterUtcTicks = 0;
                if (string.IsNullOrEmpty(record.originalDestination)) record.originalDestination = BaseUrl;
                Store.Write(path, record);
                Notify("Envío solicitado al destino seleccionado; runId conservado.");
            }
            catch (Exception ex) { Notify(ex.Message); }
        }

        public void Delete(string path)
        {
            if (ActiveReport == path) { Notify("No se puede borrar durante el envío."); return; }
            try { Store.Delete(path); Notify("Copia local eliminada. No elimina datos del servidor."); }
            catch (Exception ex) { Notify(ex.Message); }
        }

        public void TestConnection() => TestConnection(BaseUrl);
        public void TestConnection(string value)
        {
            if (healthCheck) return;
            ConnectionTestUrl = value ?? "";
            ConnectionTestSucceeded = null;
            if (!TryNormalize(value, out string destination, out string error) || string.IsNullOrEmpty(destination))
            {
                ConnectionTestMessage = "Sin conexión: " + (error ?? "ingresá una dirección.");
                ConnectionTestSucceeded = false; Changed?.Invoke(); return;
            }
            StartCoroutine(Health(destination));
        }
        private IEnumerator Health(string destination)
        {
            healthCheck = true;
            ConnectionTestMessage = "Probando conexión…";
            Changed?.Invoke();
            using (var request = UnityWebRequest.Get(destination + "/api/health"))
            {
                request.timeout = 15;
                request.redirectLimit = 0;
                var operation = BeginRequest(request, out string failure);
                if (operation != null) yield return operation;
                ConnectionTestSucceeded = operation != null && request.result == UnityWebRequest.Result.Success;
                ConnectionTestMessage = ConnectionTestSucceeded == true
                    ? "Conexión exitosa."
                    : "No se pudo conectar. HTTP " + request.responseCode + " · " + (failure ?? request.error);
            }
            healthCheck = false;
            Changed?.Invoke();
        }

        private IEnumerator Pump()
        {
            while (true)
            {
                string candidate = null;
                SessionDeliveryRecord record = null;
                SessionDelivery delivery = null;
                try
                {
                    foreach (string path in Store.Reports())
                    {
                        try
                        {
                            var current = Store.Read(path);
                            var pending = current.deliveries.Find(item => item.status == "pending" &&
                                item.retryAfterUtcTicks <= DateTime.UtcNow.Ticks);
                            if (pending == null) continue;
                            candidate = path; record = current; delivery = pending; break;
                        }
                        catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
                    }
                }
                catch (Exception ex) { Notify("No se pudo leer la cola: " + ex.Message); }
                if (candidate != null) yield return Upload(candidate, record, delivery);
                yield return new WaitForSecondsRealtime(2f);
            }
        }

        private IEnumerator Upload(string path, SessionDeliveryRecord record, SessionDelivery delivery)
        {
            byte[] payload = null;
            try
            {
                payload = File.ReadAllBytes(path);
                if (payload.Length > MaximumPayloadBytes) throw new IOException("Reporte supera 2 MB; no se enviará automáticamente.");
                var report = JsonUtility.FromJson<SessionReport>(Encoding.UTF8.GetString(payload));
                if (report?.metadata?.runId != record.runId || report?.summary == null || report?.events == null)
                    throw new IOException("Reporte incompleto o runId inconsistente.");
                if (!TryNormalize(delivery.destination, out _, out string error) || string.IsNullOrEmpty(delivery.destination))
                    throw new IOException(error ?? "Destino vacío.");
                delivery.attempts++;
                // Persist before network; a killed APK safely retries the same payload.
                delivery.retryAfterUtcTicks = DateTime.UtcNow.AddSeconds(30).Ticks;
                Store.Write(path, record);
            }
            catch (Exception ex)
            {
                delivery.status = "rejected"; delivery.lastError = ex.Message;
                TryWrite(path, record); Notify(ex.Message); payload = null;
            }
            if (payload == null) yield break;
            ActiveReport = path;
            Notify("Enviando " + record.runId);
            using (var request = new UnityWebRequest(delivery.destination + SessionsRoute, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 20;
                request.redirectLimit = 0;
                var operation = BeginRequest(request, out string failure);
                if (operation != null) yield return operation;
                Receipt receipt = null;
                if (request.result == UnityWebRequest.Result.Success)
                    try { receipt = JsonUtility.FromJson<Receipt>(request.downloadHandler.text); }
                    catch (Exception) { /* A non-JSON success is not an acknowledgement. */ }
                bool confirmed = receipt != null && receipt.runId == record.runId &&
                    ((request.responseCode == 201 && receipt.status == "created") ||
                     (request.responseCode == 200 && receipt.status == "already_exists"));
                if (confirmed)
                {
                    delivery.status = "delivered";
                    delivery.deliveredAtUtc = DateTime.UtcNow.ToString("O");
                    delivery.lastError = "";
                }
                else
                {
                    long code = request.responseCode;
                    bool retry = code == 0 || code == 408 || code == 429 || code >= 500;
                    delivery.status = !string.IsNullOrEmpty(delivery.deliveredAtUtc) ? "delivered" : retry ? "pending" : "rejected";
                    delivery.lastError = "HTTP " + code + ": " +
                        (failure ?? request.error ?? "Respuesta sin confirmación válida para este runId.");
                    delivery.retryAfterUtcTicks = DateTime.UtcNow.AddSeconds(
                        Math.Min(300, 15 * Math.Pow(2, Math.Min(delivery.attempts, 5)))).Ticks;
                }
                TryWrite(path, record);
            }
            ActiveReport = null;
            Notify(delivery.status == "delivered" && string.IsNullOrEmpty(delivery.lastError)
                ? "Entrega confirmada: " + record.runId : delivery.lastError);
        }

        private void TryWrite(string path, SessionDeliveryRecord record)
        {
            try { Store.Write(path, record); }
            catch (Exception ex) { Debug.LogError("[Telemetry] No se pudo guardar recibo: " + ex.Message); }
        }
        private static UnityWebRequestAsyncOperation BeginRequest(UnityWebRequest request, out string error)
        {
            error = null;
            try { return request.SendWebRequest(); }
            catch (Exception ex) { error = ex.Message; return null; }
        }
        private void Notify(string message) { LastMessage = message; Changed?.Invoke(); }
    }
}
