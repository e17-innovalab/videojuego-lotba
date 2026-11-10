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
        [SerializeField] private TMPro.TMP_Text score, gems, streak, multiplier, speed, remaining, resultTitle, resultStats;
        [SerializeField] private UnityEngine.UI.Image streakFill;
        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private SpeedLines speedLines;
        [SerializeField] private Sprite wordBannerSprite;
        private RectTransform wordBanner;
        private TMPro.TextMeshProUGUI wordLabel;
        private float wordBannerStarted, wordBannerUntil;
        private float multiplierPulseUntil;
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
            telemetryPanel?.SetReport(null);
            wordBannerUntil = 0;
            if (wordBanner != null) wordBanner.gameObject.SetActive(false);
        }

        public void ShowWordProgress(string word, bool[] collected, bool complete)
        {
            if (wordBanner == null)
            {
                var panel = new GameObject("Word progress", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                wordBanner = (RectTransform)panel.transform;
                wordBanner.SetParent(raceHud.transform, false);
                wordBanner.anchorMin = new Vector2(.15f, 1f);
                wordBanner.anchorMax = new Vector2(.85f, 1f);
                wordBanner.sizeDelta = new Vector2(0f, 66f);
                var background = panel.GetComponent<UnityEngine.UI.Image>();
                background.sprite = wordBannerSprite;
                background.type = UnityEngine.UI.Image.Type.Sliced;
                background.color = new Color(.07f, .35f, .58f, .98f);
                background.raycastTarget = false;

                var label = new GameObject("Letters", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                wordLabel = label.GetComponent<TMPro.TextMeshProUGUI>();
                wordLabel.rectTransform.SetParent(wordBanner, false);
                wordLabel.rectTransform.anchorMin = Vector2.zero;
                wordLabel.rectTransform.anchorMax = Vector2.one;
                wordLabel.rectTransform.offsetMin = new Vector2(14f, 4f);
                wordLabel.rectTransform.offsetMax = new Vector2(-14f, -4f);
                wordLabel.font = gems.font;
                wordLabel.fontStyle = TMPro.FontStyles.Bold;
                wordLabel.alignment = TMPro.TextAlignmentOptions.Center;
                wordLabel.enableAutoSizing = true;
                wordLabel.fontSizeMin = 12f;
                wordLabel.fontSizeMax = 38f;
                wordLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                wordLabel.raycastTarget = false;
            }
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
            wordBanner.anchoredPosition = Vector2.up * Mathf.SmoothStep(42f, -126f, slide);
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
            RenderWordBanner(running);
            pause.interactable = !SettingsOpen;
            score.text = session.Score.ToString(); gems.text = session.GemBasePoints.ToString();
            streak.text = $"RACHA {session.Streak}"; multiplier.text = $"×{session.Multiplier}";
            if (session.Multiplier > previousMultiplier) multiplierPulseUntil = Time.unscaledTime + .3f;
            previousMultiplier = session.Multiplier;
            float pulse = running ? Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .3f) : 0;
            multiplier.transform.parent.localScale = Vector3.one * (1 + Mathf.Sin(pulse * Mathf.PI) * .18f);
            streakFill.fillAmount = session.StreakProgress;
            speed.text = $"{session.Speed * 3.6f:0}"; remaining.text = $"{session.Remaining:0} s";
            gauge.Value = Mathf.InverseLerp(8, settings.MaximumSpeed, session.Speed);
            if (running) speedLines.Intensity = Mathf.InverseLerp(settings.InitialSpeed * .8f, settings.MaximumSpeed, session.Speed);
            else speedLines.ClearEffect();
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
