using System;
using System.Collections;
using System.Collections.Generic;
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
        public const float RetryCooldownSeconds = 45f;
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
        private bool draining;
        private readonly Queue<DeliveryRequest> requests = new Queue<DeliveryRequest>();
        private readonly HashSet<string> queued = new HashSet<string>();
        private readonly Dictionary<string, float> destinationRetryAfter = new Dictionary<string, float>();

        private sealed class DeliveryRequest
        {
            public string path, destination;
            public string Key => path + "\n" + destination;
        }

        [Serializable] private sealed class Receipt { public string status, runId; }

        private void Awake()
        {
            Store = new SessionDeliveryStore(SessionReportWriter.DefaultDirectory);
            BaseUrl = DefaultBaseUrl;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || LOTBA_DEVELOPMENT_TOOLS
            string configured = PlayerPrefs.GetString(EndpointPreference, DefaultBaseUrl);
            if (TryNormalize(configured, out string normalized, out _)) BaseUrl = normalized;
#endif
        }

        private void Start() => QueueStartupPending();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) RetryPendingIfReady();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) RetryPendingIfReady();
        }

        private void QueueStartupPending()
        {
            try
            {
                foreach (string path in Store.Reports())
                {
                    try
                    {
                        var record = Store.Read(path);
                        TryBindOrphanReport(path, record);
                        if (!HasNetworkConnectivity()) continue;
                        foreach (var delivery in record.deliveries)
                            if (delivery.status == "pending") Enqueue(path, delivery.destination);
                    }
                    catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
                }
            }
            catch (Exception ex) { Notify("No se pudo leer la cola: " + ex.Message); }
        }

        public void RetryPendingIfReady()
        {
            if (!HasNetworkConnectivity()) return;
            bool anyQueued = false;
            try
            {
                foreach (string path in Store.Reports())
                {
                    if (ActiveReport == path) continue;
                    try
                    {
                        var record = Store.Read(path);
                        TryBindOrphanReport(path, record);
                        foreach (var delivery in record.deliveries)
                        {
                            if (delivery.status == "pending" && !IsDestinationInCooldown(delivery.destination))
                            {
                                Enqueue(path, delivery.destination);
                                anyQueued = true;
                            }
                        }
                    }
                    catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Telemetry] " + ex.Message); }
            if (anyQueued) Notify("Reintentando envíos pendientes.");
        }

        private void TryBindOrphanReport(string path, SessionDeliveryRecord record)
        {
            if (string.IsNullOrEmpty(BaseUrl) ||
                !string.IsNullOrEmpty(record.originalDestination) ||
                record.deliveries.Count != 0)
            {
                return;
            }
            record.originalDestination = BaseUrl;
            SessionDeliveryStore.GetOrAdd(record, BaseUrl);
            Store.Write(path, record);
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
            try
            {
                Store.Register(path, BaseUrl);
                var record = Store.Read(path);
                var delivery = record.deliveries.Find(item => item.status == "pending" && item.destination == record.originalDestination);
                Notify("Partida guardada localmente.");
                if (delivery != null)
                {
                    if (!HasNetworkConnectivity() || IsDestinationInCooldown(delivery.destination))
                        Notify("Partida pendiente. Se reintentará automáticamente al recuperar conexión.");
                    else
                    {
                        Enqueue(path, delivery.destination);
                        RetryPendingIfReady();
                    }
                }
            }
            catch (Exception ex) { Notify("Reporte guardado, estado pendiente de reparar: " + ex.Message); }
        }

        // Explicit action: bind legacy/unassigned reports or retry only this destination.
        public void SendPending()
        {
            if (string.IsNullOrEmpty(BaseUrl)) { Notify("Configurá primero el destino."); return; }
            try
            {
                destinationRetryAfter.Remove(BaseUrl);
                foreach (string path in Store.Reports())
                {
                    if (ActiveReport == path) continue;
                    var record = Store.Read(path);
                    if (string.IsNullOrEmpty(record.originalDestination) && record.deliveries.Count == 0)
                        record.originalDestination = BaseUrl;
                    if (record.originalDestination != BaseUrl &&
                        !record.deliveries.Exists(item => item.destination == BaseUrl)) continue;
                    var delivery = SessionDeliveryStore.GetOrAdd(record, BaseUrl);
                    if (delivery.status == "delivered") continue;
                    delivery.status = "pending";
                    Store.Write(path, record);
                    Enqueue(path, BaseUrl);
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
                if (string.IsNullOrEmpty(record.originalDestination)) record.originalDestination = BaseUrl;
                Store.Write(path, record);
                destinationRetryAfter.Remove(BaseUrl);
                Enqueue(path, BaseUrl);
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

        private void Enqueue(string path, string destination)
        {
            if (string.IsNullOrEmpty(destination) || IsDestinationInCooldown(destination)) return;
            var request = new DeliveryRequest { path = path, destination = destination };
            if (!queued.Add(request.Key)) return;
            requests.Enqueue(request);
            if (draining) return;
            draining = true;
            StartCoroutine(Drain());
        }

        // Finite work queue: startup, a new report, lifecycle resume or an explicit manual action.
        // Transient failures apply a per-destination cooldown to avoid request spam.
        private IEnumerator Drain()
        {
            while (requests.Count > 0)
            {
                var work = requests.Dequeue();
                SessionDeliveryRecord record = null;
                SessionDelivery delivery = null;
                if (!IsDestinationInCooldown(work.destination) && File.Exists(work.path))
                {
                    try
                    {
                        record = Store.Read(work.path);
                        delivery = record.deliveries.Find(item => item.destination == work.destination && item.status == "pending");
                    }
                    catch (Exception ex) { Notify("No se pudo leer la partida: " + ex.Message); }
                }
                if (delivery != null) yield return Upload(work.path, record, delivery);
                queued.Remove(work.Key);
            }
            draining = false;
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
                // Keep redirects disabled for POST security (avoids body drop or cross-host leakage).
                // Production DefaultBaseUrl must point to the canonical non-redirecting HTTPS origin.
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
                    destinationRetryAfter.Remove(delivery.destination);
                }
                else
                {
                    long code = request.responseCode;
                    bool retry = IsTransientFailure(code);
                    delivery.status = !string.IsNullOrEmpty(delivery.deliveredAtUtc) ? "delivered" : retry ? "pending" : "rejected";
                    delivery.lastError = "HTTP " + code + ": " +
                        (failure ?? request.error ?? "Respuesta sin confirmación válida para este runId.");
                    if (retry) destinationRetryAfter[delivery.destination] = Time.realtimeSinceStartup + RetryCooldownSeconds;
                }
                TryWrite(path, record);
            }
            ActiveReport = null;
            Notify(delivery.status == "delivered" && string.IsNullOrEmpty(delivery.lastError)
                ? "Entrega confirmada: " + record.runId : delivery.lastError +
                  (IsDestinationInCooldown(delivery.destination)
                      ? $" · Destino en pausa ({RetryCooldownSeconds:0} s) o hasta recuperar conexión." : ""));
        }

        private bool IsDestinationInCooldown(string destination) =>
            !string.IsNullOrEmpty(destination) &&
            destinationRetryAfter.TryGetValue(destination, out float retryAt) &&
            Time.realtimeSinceStartup < retryAt;

        private static bool HasNetworkConnectivity() =>
            Application.internetReachability != NetworkReachability.NotReachable;

        private static bool IsTransientFailure(long responseCode) =>
            responseCode == 0 || responseCode == 408 || responseCode == 429 || responseCode >= 500;

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
