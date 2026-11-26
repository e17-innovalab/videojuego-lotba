using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lotba.Game
{
    /// <summary>Logic-only bindings for authored administrative journey panels.</summary>
    public sealed class JourneyAdminPanel : MonoBehaviour
    {
        public bool HasActiveJourney => current != null;
        [SerializeField] private GameObject overlayRoot;
        [Header("Authored panels")]
        [SerializeField] private GameObject loginPanel, configurePanel, activePanel;
        [SerializeField] private GameObject missingJourneyNote;
        [Header("Access")]
        [SerializeField] private TMP_InputField usernameInput, passwordInput;
        [SerializeField] private Button loginButton, loginBackButton;
        [SerializeField] private TMP_Text loginButtonLabel, loginError;
        [Header("Journey setup")]
        [SerializeField] private TMP_InputField schoolInput;
        [SerializeField] private Button[] shiftButtons = new Button[3];
        [SerializeField] private TMP_Text dateLabel, setupError;
        [SerializeField] private Button beginButton, setupBackButton;
        [Header("Active journey")]
        [SerializeField] private TMP_Text activeSchool, activeShift, activeDate, playedCount, deliveredCount, pendingCount, activeError;
        [SerializeField] private Button continueButton, finishButton, activeBackButton;
        [Header("Credentials")]
        [SerializeField] private string adminUsername = "lotba", adminPassword = "admin";
        [Header("Integration")]
        [SerializeField] private SessionReportTransport reportTransport;
        private JourneySession current;
        private Action finishJourney;
        private void OnEnable()
        {
            if (reportTransport != null)
            {
                reportTransport.Changed -= OnDeliveryChanged;
                reportTransport.Changed += OnDeliveryChanged;
            }
        }

        private void OnDisable()
        {
            if (reportTransport != null) reportTransport.Changed -= OnDeliveryChanged;
        }

        private void OnDeliveryChanged()
        {
            if (activePanel != null && activePanel.activeSelf) RefreshActive();
        }
        private Action continueJourney;
        private int selectedShift;
        private bool confirmClosing;

        private void Awake()
        {
            if (missingJourneyNote != null) missingJourneyNote.SetActive(false);
            if (loginButton != null) loginButton.onClick.AddListener(Login);
            if (loginBackButton != null) loginBackButton.onClick.AddListener(Close);
            if (beginButton != null) beginButton.onClick.AddListener(BeginJourney);
            if (setupBackButton != null) setupBackButton.onClick.AddListener(Close);
            for (int i = 0; i < shiftButtons.Length; i++)
            {
                int index = i;
                if (shiftButtons[i] != null) shiftButtons[i].onClick.AddListener(() => SelectShift(index));
            }
            if (continueButton != null) continueButton.onClick.AddListener(() => continueJourney?.Invoke());
            if (finishButton != null) finishButton.onClick.AddListener(FinishJourney);
            if (activeBackButton != null) activeBackButton.onClick.AddListener(Close);
        }

        public void Initialize(SessionReportTransport transport, JourneySession session,
            Action continueAction, Action finishAction)
        {
            if (reportTransport != null) reportTransport.Changed -= OnDeliveryChanged;
            reportTransport = transport;
            if (reportTransport != null) reportTransport.Changed += OnDeliveryChanged;
            current = session;
            continueJourney = continueAction;
            finishJourney = finishAction;
            if (current != null) RefreshActive();
        }

        public void Open()
        {
            confirmClosing = false;
            if (dateLabel != null) dateLabel.text = DateTime.Now.ToString("dd/MM/yyyy");
            if (loginButtonLabel != null) loginButtonLabel.text = "INGRESAR";
            ClearLoginFeedback();
            if (usernameInput != null) usernameInput.text = adminUsername;
            if (passwordInput != null) passwordInput.text = "";
            if (setupError != null) setupError.text = "";
            if (activeError != null) activeError.text = "";
            Show(loginPanel);
        }

        public void CloseToMenu() => Show(null);

        private void Login()
        {
            if (usernameInput == null || passwordInput == null || usernameInput.text.Trim() != adminUsername || passwordInput.text != adminPassword)
            {
                if (loginError != null) loginError.text = "Usuario o contraseña incorrectos.";
                return;
            }
            if (passwordInput != null) passwordInput.text = "";
            if (confirmClosing)
            {
                confirmClosing = false;
                CompleteJourneyClose();
                return;
            }
            if (current != null)
            {
                RefreshActive();
                Show(activePanel);
                return;
            }
            if (schoolInput != null) schoolInput.text = "";
            SelectShift(0);
            if (dateLabel != null) dateLabel.text = DateTime.Now.ToString("dd/MM/yyyy");
            Show(configurePanel);
        }

        private void BeginJourney()
        {
            string school = schoolInput != null ? schoolInput.text.Trim() : "";
            if (string.IsNullOrWhiteSpace(school))
            {
                if (setupError != null) setupError.text = "Ingresá el nombre del colegio.";
                return;
            }
            string[] shifts = { "mañana", "tarde", "noche" };
            string shift = shifts[Mathf.Clamp(selectedShift, 0, shifts.Length - 1)];
            try { current = JourneySessionStore.Begin(school, shift, DateTime.Now.Date); }
            catch (Exception ex)
            {
                if (setupError != null) setupError.text = "No se pudo guardar la jornada en esta tablet. " + ex.Message;
                return;
            }
            if (activeError != null) activeError.text = "";
            RefreshActive();
            continueJourney?.Invoke();
        }

        private void RefreshActive()
        {
            if (current == null) return;
            if (activeSchool != null) activeSchool.text = current.schoolName;
            if (activeShift != null) activeShift.text = current.schoolShift;
            if (activeDate != null && DateTime.TryParse(current.schoolDate, out var date)) activeDate.text = date.ToString("dd/MM/yyyy");
            int total = 0, delivered = 0, pending = 0;
            if (reportTransport != null && reportTransport.Store != null)
                reportTransport.Store.CountJourney(current.journeyId, out total, out delivered, out pending);
            if (playedCount != null) playedCount.text = $"Partidas realizadas  {total}";
            if (deliveredCount != null) deliveredCount.text = $"Sincronizadas  {delivered}";
            if (pendingCount != null) pendingCount.text = $"Pendientes de envío  {pending}";
        }

        private void FinishJourney()
        {
            confirmClosing = true;
            if (loginButtonLabel != null) loginButtonLabel.text = "CONFIRMAR CIERRE";
            if (passwordInput != null) passwordInput.text = "";
            Show(loginPanel);
        }

        private void CompleteJourneyClose()
        {
            int pending = 0;
            if (reportTransport != null && reportTransport.Store != null)
                reportTransport.Store.CountJourney(current?.journeyId, out _, out _, out pending);
            if (pending > 0 && activeError != null)
                activeError.text = $"Quedan {pending} reportes pendientes. La jornada se cerrará y se conservarán para envío automático.";
            JourneySessionStore.End();
            current = null;
            finishJourney?.Invoke();
            confirmClosing = false;
            if (loginButtonLabel != null) loginButtonLabel.text = "INGRESAR";
            ClearLoginFeedback();
            Show(null);
        }

        private void Close()
        {
            confirmClosing = false;
            ClearLoginFeedback();
            Show(null);
        }

        private void ClearLoginFeedback()
        {
            if (passwordInput != null) passwordInput.text = "";
            if (loginError != null) loginError.text = "";
        }

        private void SelectShift(int index)
        {
            selectedShift = Mathf.Clamp(index, 0, 2);
            for (int i = 0; i < shiftButtons.Length; i++)
                if (shiftButtons[i] != null) shiftButtons[i].interactable = i != selectedShift;
        }

        private void Show(GameObject target)
        {
            if (overlayRoot != null && target != null && !overlayRoot.activeSelf) overlayRoot.SetActive(true);
            if (loginPanel != null) loginPanel.SetActive(target == loginPanel);
            if (configurePanel != null) configurePanel.SetActive(target == configurePanel);
            if (activePanel != null) activePanel.SetActive(target == activePanel);
            if (missingJourneyNote != null) missingJourneyNote.SetActive(target == loginPanel && current == null);
            if (overlayRoot != null && target == null) overlayRoot.SetActive(false);
        }
    }
}
