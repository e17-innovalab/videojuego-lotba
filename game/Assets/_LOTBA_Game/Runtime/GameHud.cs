using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Scene UI bindings. Configuration exposes only implemented, available capabilities.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text gameTitle, developmentLabel, identityFooter, buildVersion;
        [SerializeField] private GameObject readyPanel, pausePanel, resultPanel, raceHud, controls, settingsPanel;
        [SerializeField] private UnityEngine.UI.Button start, retry, menu, pause, resume, brake, pauseMenu, exit;
        [SerializeField] private UnityEngine.UI.Button[] configurationButtons;
        [SerializeField] private UnityEngine.UI.Button closeSettings, soundButton, vibrationButton;
        [SerializeField] private TMPro.TMP_Text soundLabel, vibrationLabel;
        [SerializeField] private UnityEngine.UI.Button qualityButton;
        [SerializeField] private TMPro.TMP_Text qualityLabel;
        [SerializeField] private TMPro.TMP_Text score, gems, streak, multiplier, remaining, resultTitle, resultStats;
        [SerializeField] private UnityEngine.UI.Image streakFill;
        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private RectTransform wordBanner;
        [SerializeField] private TMPro.TMP_Text wordLabel;
        private Vector2 wordBannerVisiblePosition;
        private float wordBannerStarted, wordBannerUntil;
        private float multiplierPulseUntil;
        private TMPro.TextMeshProUGUI impactLabel;
        private float impactUntil;
        private int previousMultiplier = 1;
        public bool SettingsOpen { get; private set; }
        private RunTelemetryPanel telemetryPanel;
        public bool TelemetryOpen => telemetryPanel != null && telemetryPanel.IsOpen;
        public void CloseTelemetry() => telemetryPanel?.Close();
        public event Action StartRequested, MenuRequested, PauseRequested, BrakeRequested, SoundRequested, SettingsRequested, VibrationRequested;
        public event Action QualityRequested;
        private void Awake()
        {
            ApplyIdentity();
            wordBannerVisiblePosition = wordBanner.anchoredPosition;
            wordBanner.gameObject.SetActive(false);
            var impactObject = new GameObject("Impact feedback", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            var impactRect = (RectTransform)impactObject.transform;
            impactRect.SetParent(raceHud.transform, false);
            impactRect.anchorMin = impactRect.anchorMax = new Vector2(.5f, .72f);
            impactRect.sizeDelta = new Vector2(320f, 60f);
            impactLabel = impactObject.GetComponent<TMPro.TextMeshProUGUI>();
            impactLabel.font = score.font;
            impactLabel.fontSize = 24f;
            impactLabel.fontStyle = TMPro.FontStyles.Bold;
            impactLabel.alignment = TMPro.TextAlignmentOptions.Center;
            impactLabel.color = new Color32(255, 223, 145, 255);
            impactLabel.raycastTarget = false;
            impactObject.SetActive(false);
            telemetryPanel = new RunTelemetryPanel(resultPanel, resultStats, menu);
            exit.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
            exit.onClick.AddListener(ExitGame);
            start.onClick.AddListener(() => StartRequested?.Invoke()); retry.onClick.AddListener(() => StartRequested?.Invoke());
            pauseMenu.onClick.AddListener(() => MenuRequested?.Invoke());
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
            pause.onClick.AddListener(() => PauseRequested?.Invoke()); resume.onClick.AddListener(() => PauseRequested?.Invoke());
            brake.onClick.AddListener(() => BrakeRequested?.Invoke());
            foreach (var button in configurationButtons) button.onClick.AddListener(() => SettingsRequested?.Invoke());
            closeSettings.onClick.AddListener(() => SettingsOpen = false);
            soundButton.onClick.AddListener(() => SoundRequested?.Invoke());
            vibrationButton.onClick.AddListener(() => VibrationRequested?.Invoke());
            qualityButton.onClick.AddListener(() => QualityRequested?.Invoke());
        }
        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void ApplyIdentity()
        {
            if (gameTitle != null)
            {
                string title = GameIdentity.Title.ToUpperInvariant();
                int split = title.IndexOf(' ');
                gameTitle.text = split > 0
                    ? title.Substring(0, split) + "\n<color=#FFC43D>" + title.Substring(split + 1) + "</color>"
                    : title;
                gameTitle.enableAutoSizing = true;
                gameTitle.fontSizeMin = 32;
                gameTitle.fontSizeMax = 62;
            }
            if (developmentLabel != null)
            {
                developmentLabel.text = GameIdentity.DevelopmentLabel;
                developmentLabel.enableAutoSizing = true;
                developmentLabel.fontSizeMin = 12;
                developmentLabel.fontSizeMax = 17;
            }
            if (identityFooter != null) identityFooter.text = GameIdentity.FooterLabel;
            if (buildVersion != null) buildVersion.text = $"v{Application.version}";
        }
        public void ResetView()
        {
            multiplierPulseUntil = 0; previousMultiplier = 1; SettingsOpen = false;
            impactUntil = 0f;
            telemetryPanel?.SetReport(null);
            wordBannerUntil = 0;
            if (wordBanner != null) wordBanner.gameObject.SetActive(false);
        }

        public void ShowImpact(long loss, bool lostStreak)
        {
            impactLabel.text = loss > 0 ? $"−{loss} puntos" : "Choque";
            if (lostStreak) impactLabel.text += "\nRacha perdida";
            impactUntil = Time.unscaledTime + 1.15f;
        }

        public void ShowWordProgress(string word, bool[] collected, bool complete)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < word.Length; i++)
            {
                if (collected[i]) text.Append("<color=#FFD14C>").Append(word[i]).Append("</color>");
                else text.Append(word[i]);
            }
            wordLabel.text = text.ToString();
            if (!wordBanner.gameObject.activeSelf || Time.unscaledTime >= wordBannerUntil)
                wordBannerStarted = Time.unscaledTime;
            wordBannerUntil = Time.unscaledTime + (complete ? 3f : 1.6f);
        }

        private void RenderWordBanner(bool running)
        {
            if (wordBanner == null) return;
            float now = Time.unscaledTime;
            bool visible = running && now < wordBannerUntil;
            wordBanner.gameObject.SetActive(visible);
            if (!visible) return;
            float slide = Mathf.Min((now - wordBannerStarted) / .22f, (wordBannerUntil - now) / .22f);
            float hiddenY = wordBanner.rect.height * wordBanner.pivot.y + 9f;
            wordBanner.anchoredPosition = new Vector2(wordBannerVisiblePosition.x,
                Mathf.SmoothStep(hiddenY, wordBannerVisiblePosition.y, slide));
        }
        public void OpenSettings() => SettingsOpen = true;
        public void SetQuality(string label, bool canChange)
        {
            qualityLabel.text = $"CALIDAD: {label}";
            qualityButton.interactable = canChange;
        }
        public bool IsRaceButtonAt(Vector2 screenPoint) =>
            IsButtonAt(pause, screenPoint) || IsButtonAt(brake, screenPoint);

        private static bool IsButtonAt(UnityEngine.UI.Button button, Vector2 screenPoint) =>
            button.isActiveAndEnabled && button.IsInteractable() &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, screenPoint);
        public void Render(RunSession session, GameSettings settings, bool muted, DeviceFeedbackAndInput devices)
        {
            bool running = session.State == RunState.Running;
            bool active = running || session.State == RunState.Paused;
            if (session.State == RunState.Finished) SettingsOpen = false;
            readyPanel.SetActive(session.State == RunState.Ready && !SettingsOpen);
            pausePanel.SetActive(session.State == RunState.Paused && !SettingsOpen);
            settingsPanel.SetActive(SettingsOpen);
            telemetryPanel?.SetReport(session.State == RunState.Finished ? session.Telemetry : null);
            resultPanel.SetActive(session.State == RunState.Finished && !TelemetryOpen);
            raceHud.SetActive(active); controls.SetActive(active);
            if (impactLabel != null)
                impactLabel.gameObject.SetActive(running && Time.unscaledTime < impactUntil);
            RenderWordBanner(running);
            pause.interactable = !SettingsOpen;
            score.text = session.Score.ToString(); gems.text = session.GemBasePoints.ToString();
            streak.text = $"RACHA {session.Streak}"; multiplier.text = $"×{session.Multiplier}";
            if (session.Multiplier > previousMultiplier) multiplierPulseUntil = Time.unscaledTime + .3f;
            previousMultiplier = session.Multiplier;
            float pulse = running ? Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .3f) : 0;
            multiplier.transform.parent.localScale = Vector3.one * (1 + Mathf.Sin(pulse * Mathf.PI) * .18f);
            streakFill.fillAmount = session.StreakProgress;
            remaining.text = $"{session.Remaining:0} s";
            gauge.SetSpeed(session.Speed, settings.MaximumSpeed);
            soundLabel.text = muted ? "SONIDO: NO" : "SONIDO: SÍ";
            vibrationButton.interactable = devices.VibrationAvailable;
            vibrationLabel.text = !devices.VibrationAvailable ? "VIBRACIÓN: NO DISPONIBLE" : "VIBRACIÓN: " + devices.VibrationLevelLabel;
            if (session.State == RunState.Finished)
            {
                resultTitle.text = session.Ending == RunEnding.Brake ? "VOS MARCASTE\nEL FINAL" : "RECORRIDO\nCOMPLETADO";
                resultStats.text = $"<size=54><color=#FFD14C>{session.Score}</color></size>\nPUNTOS\n\n{session.Gems} gemas · {session.GemBasePoints} pts base\n{session.Distance:0} m · {session.Elapsed:0} s  ·  {session.PeakSpeed * 3.6f:0} km/h máx.\n\nMejor racha: {session.BestStreak}  ·  ×{session.BestMultiplier}\nChoques: {session.Collisions}";
            }
        }
    }
}
