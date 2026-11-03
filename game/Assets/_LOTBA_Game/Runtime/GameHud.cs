using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Scene UI bindings. Configuration exposes only implemented, available capabilities.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text gameTitle, developmentLabel, identityFooter;
        [SerializeField] private GameObject readyPanel, pausePanel, resultPanel, raceHud, controls, settingsPanel;
        [SerializeField] private UnityEngine.UI.Button start, retry, menu, pause, resume, brake, pauseMenu;
        [SerializeField] private UnityEngine.UI.Button[] configurationButtons;
        [SerializeField] private UnityEngine.UI.Button closeSettings, soundButton, vibrationButton, tiltButton;
        [SerializeField] private TMPro.TMP_Text soundLabel, vibrationLabel, tiltLabel, settingsNotice;
        [SerializeField] private TMPro.TMP_Text score, coins, streak, multiplier, speed, remaining, popup, resultTitle, resultStats;
        [SerializeField] private UnityEngine.UI.Image streakFill;
        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private SpeedLines speedLines;
        private float popupUntil, popupDuration, multiplierPulseUntil;
        private int previousMultiplier = 1;
        private Vector2 popupOrigin;
        public bool SettingsOpen { get; private set; }
        public event Action StartRequested, MenuRequested, PauseRequested, BrakeRequested, SoundRequested, SettingsRequested, VibrationRequested, TiltRequested;
        private void Awake()
        {
            ApplyIdentity();
            popupOrigin = popup.rectTransform.anchoredPosition;
            start.onClick.AddListener(() => StartRequested?.Invoke()); retry.onClick.AddListener(() => StartRequested?.Invoke());
            pauseMenu.onClick.AddListener(() => MenuRequested?.Invoke());
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
            pause.onClick.AddListener(() => PauseRequested?.Invoke()); resume.onClick.AddListener(() => PauseRequested?.Invoke());
            brake.onClick.AddListener(() => BrakeRequested?.Invoke());
            foreach (var button in configurationButtons) button.onClick.AddListener(() => SettingsRequested?.Invoke());
            closeSettings.onClick.AddListener(() => SettingsOpen = false);
            soundButton.onClick.AddListener(() => SoundRequested?.Invoke());
            vibrationButton.onClick.AddListener(() => VibrationRequested?.Invoke());
            tiltButton.onClick.AddListener(() => TiltRequested?.Invoke());
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
        }
        public void ResetView() { popupUntil = multiplierPulseUntil = 0; previousMultiplier = 1; SettingsOpen = false; }
        public void OpenSettings() => SettingsOpen = true;
        public void ShowFeedback(long amount, bool impact)
        {
            popup.text = impact ? (amount == 0 ? "¡OJO!" : $"−{Math.Abs(amount)}") : amount > 10 ? $"+10\n<size=22>×{amount / 10} = {amount} pts</size>" : "+10";
            popup.color = impact ? new Color(1, .3f, .16f) : new Color(1, .92f, .48f);
            popupDuration = impact ? .7f : .4f; popupUntil = Time.unscaledTime + popupDuration;
        }
        public void Render(RunSession session, GameSettings settings, bool muted, DeviceFeedbackAndInput devices)
        {
            bool running = session.State == RunState.Running;
            bool active = running || session.State == RunState.Paused;
            if (session.State == RunState.Finished) SettingsOpen = false;
            readyPanel.SetActive(session.State == RunState.Ready && !SettingsOpen);
            pausePanel.SetActive(session.State == RunState.Paused && !SettingsOpen);
            settingsPanel.SetActive(SettingsOpen);
            resultPanel.SetActive(session.State == RunState.Finished);
            raceHud.SetActive(active); controls.SetActive(active);
            pause.interactable = !SettingsOpen;
            score.text = session.Score.ToString(); coins.text = session.CoinValue.ToString();
            streak.text = $"RACHA {session.Streak}"; multiplier.text = $"×{session.Multiplier}";
            if (session.Multiplier > previousMultiplier) multiplierPulseUntil = Time.unscaledTime + .3f;
            previousMultiplier = session.Multiplier;
            float pulse = running ? Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .3f) : 0;
            multiplier.transform.parent.localScale = Vector3.one * (1 + Mathf.Sin(pulse * Mathf.PI) * .18f);
            streakFill.fillAmount = session.StreakProgress;
            speed.text = $"{session.Speed * 3.6f:0}"; remaining.text = $"{session.Remaining:0} s";
            gauge.Value = Mathf.InverseLerp(8, settings.MaximumSpeed, session.Speed);
            popup.gameObject.SetActive(running && Time.unscaledTime < popupUntil);
            popup.alpha = Mathf.Clamp01((popupUntil - Time.unscaledTime) / .1f);
            float progress = popupDuration > 0 ? 1 - Mathf.Clamp01((popupUntil - Time.unscaledTime) / popupDuration) : 0;
            popup.rectTransform.anchoredPosition = popupOrigin + Vector2.up * (progress * 26);
            if (running) speedLines.Intensity = Mathf.InverseLerp(settings.InitialSpeed * .8f, settings.MaximumSpeed, session.Speed);
            else speedLines.ClearEffect();
            soundLabel.text = muted ? "SONIDO: NO" : "SONIDO: SÍ";
            vibrationButton.interactable = devices.VibrationAvailable;
            vibrationLabel.text = !devices.VibrationAvailable ? "VIBRACIÓN: NO DISPONIBLE" : devices.VibrationEnabled ? "VIBRACIÓN: SÍ" : "VIBRACIÓN: NO";
            tiltButton.interactable = devices.TiltAvailable;
            tiltLabel.text = !devices.TiltAvailable ? "INCLINACIÓN: NO DISPONIBLE" : devices.TiltEnabled ? "INCLINACIÓN: SÍ" : "INCLINACIÓN: NO";
            settingsNotice.text = devices.TiltEnabled ? "Al continuar, sostené el teléfono centrado un instante para calibrar la inclinación." : "Los cambios de sonido y vibración se aplican a esta partida.";
            if (session.State == RunState.Finished)
            {
                resultTitle.text = session.Ending == RunEnding.Brake ? "VOS MARCASTE\nEL FINAL" : "RECORRIDO\nCOMPLETADO";
                resultStats.text = $"<size=54><color=#FFD14C>{session.Score}</color></size>\nPUNTOS\n\n{session.CoinValue} monedas · {session.Coins} recogidas\n{session.Distance:0} m · {session.Elapsed:0} s  ·  {session.PeakSpeed * 3.6f:0} km/h máx.\n\nMejor racha: {session.BestStreak}  ·  ×{session.BestMultiplier}\nChoques: {session.Collisions}";
            }
        }
    }
}
