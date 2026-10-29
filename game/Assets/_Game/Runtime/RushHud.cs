using System;
using UnityEngine;

namespace Lotba.CityRush
{
    /// <summary>Scene UI bindings. Configuration exposes only implemented, available capabilities.</summary>
    public sealed class RushHud : MonoBehaviour
    {
        [SerializeField] private GameObject readyPanel, pausePanel, resultPanel, raceHud, controls, settingsPanel;
        [SerializeField] private UnityEngine.UI.Button start, retry, menu, pause, resume, brake, pauseMenu;
        [SerializeField] private UnityEngine.UI.Button[] configurationButtons;
        [SerializeField] private UnityEngine.UI.Button closeSettings, soundButton, vibrationButton, tiltButton, obeliscoButton, laBocaButton, flowButton;
        [SerializeField] private GameObject locationOptions;
        [SerializeField] private TMPro.TMP_Text soundLabel, vibrationLabel, tiltLabel, locationLabel, settingsNotice;
        [SerializeField] private TMPro.TMP_Text score, coins, streak, multiplier, speed, remaining, popup, resultTitle, resultStats;
        [SerializeField] private UnityEngine.UI.Image streakFill;
        [SerializeField] private RushGauge gauge;
        [SerializeField] private RushSpeedLines speedLines;
        private float popupUntil, popupDuration, multiplierPulseUntil;
        private int previousMultiplier = 1;
        private Vector2 popupOrigin;
        private bool loading;
        public bool SettingsOpen { get; private set; }
        public event Action StartRequested, MenuRequested, PauseRequested, BrakeRequested, SoundRequested, SettingsRequested, VibrationRequested, TiltRequested;
        public event Action<RushLocation> LocationRequested;
        private void Awake()
        {
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
            obeliscoButton.onClick.AddListener(() => LocationRequested?.Invoke(RushLocation.Obelisco));
            laBocaButton.onClick.AddListener(() => LocationRequested?.Invoke(RushLocation.LaBoca));
            flowButton.onClick.AddListener(() => LocationRequested?.Invoke(RushLocation.LaBocaToObelisco));
        }
        public void ResetView() { popupUntil = multiplierPulseUntil = 0; previousMultiplier = 1; SettingsOpen = false; }
        public void OpenSettings() => SettingsOpen = true;
        public void SetLoading() { loading = true; start.interactable = retry.interactable = false; }
        public void ShowFeedback(long amount, bool impact)
        {
            popup.text = impact ? (amount == 0 ? "¡OJO!" : $"−{Math.Abs(amount)}") : amount > 10 ? $"+10\n<size=22>×{amount / 10} = {amount} pts</size>" : "+10";
            popup.color = impact ? new Color(1, .3f, .16f) : new Color(1, .92f, .48f);
            popupDuration = impact ? .7f : .4f; popupUntil = Time.unscaledTime + popupDuration;
        }
        public void Render(RushSession session, RushSettings settings, bool muted, RushDevices devices)
        {
            bool running = session.State == RushState.Running;
            bool active = running || session.State == RushState.Paused;
            if (session.State == RushState.Finished) SettingsOpen = false;
            readyPanel.SetActive(session.State == RushState.Ready && !SettingsOpen);
            pausePanel.SetActive(session.State == RushState.Paused && !SettingsOpen);
            settingsPanel.SetActive(SettingsOpen);
            resultPanel.SetActive(session.State == RushState.Finished);
            raceHud.SetActive(active); controls.SetActive(active);
            bool menuConfiguration = session.State == RushState.Ready;
            locationOptions.SetActive(menuConfiguration);
            var settingsRect = (RectTransform)settingsPanel.transform;
            settingsRect.anchorMin = new Vector2(.055f, menuConfiguration ? .08f : .24f);
            settingsRect.anchorMax = new Vector2(.945f, .94f);
            settingsRect.offsetMin = settingsRect.offsetMax = Vector2.zero;
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
            RushLocation selected = RushSceneFlow.Selected;
            locationLabel.text = "ESCENARIO: " + RushSceneFlow.NameFor(selected);
            obeliscoButton.GetComponentInChildren<TMPro.TMP_Text>().text = selected == RushLocation.Obelisco ? "OBELISCO · ELEGIDO" : "OBELISCO";
            laBocaButton.GetComponentInChildren<TMPro.TMP_Text>().text = selected == RushLocation.LaBoca ? "LA BOCA · ELEGIDO" : "LA BOCA";
            flowButton.GetComponentInChildren<TMPro.TMP_Text>().text = selected == RushLocation.LaBocaToObelisco ? "LA BOCA → OBELISCO · ELEGIDO" : "LA BOCA → OBELISCO";
            settingsNotice.text = menuConfiguration ? "El recorrido se elige antes de empezar. La inclinación es opcional y se calibra al comenzar." : devices.TiltEnabled ? "Al continuar, sostené el teléfono centrado un instante para calibrar la inclinación." : "Los cambios de sonido y vibración se aplican a esta partida.";
            var noticeRect = settingsNotice.rectTransform;
            noticeRect.anchorMin = new Vector2(.08f, menuConfiguration ? .11f : .24f);
            noticeRect.anchorMax = new Vector2(.92f, menuConfiguration ? .22f : .55f);
            noticeRect.offsetMin = noticeRect.offsetMax = Vector2.zero;
            if (loading) start.GetComponentInChildren<TMPro.TMP_Text>().text = "PREPARANDO...";
            if (session.State == RushState.Finished)
            {
                resultTitle.text = session.Ending == RushEnding.Brake ? "VOS MARCASTE\nEL FINAL" : "RECORRIDO\nCOMPLETADO";
                resultStats.text = $"<size=54><color=#FFD14C>{session.Score}</color></size>\nPUNTOS\n\n{session.CoinValue} monedas · {session.Coins} recogidas\n{session.Distance:0} m · {session.Elapsed:0} s  ·  {session.PeakSpeed * 3.6f:0} km/h máx.\n\nMejor racha: {session.BestStreak}  ·  ×{session.BestMultiplier}\nChoques: {session.Collisions}";
            }
        }
    }
}
