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
        private RectTransform speedOffer;
        private BoostSpeedLines boostSpeedLines;
        private RectTransform milestoneBar;
        private RectTransform milestoneFill;
        private TMPro.TextMeshProUGUI milestoneLabel;
        private int previousMultiplier = 1;
        public bool SettingsOpen { get; private set; }
        private RunTelemetryPanel telemetryPanel;
        public bool TelemetryOpen => telemetryPanel != null && telemetryPanel.IsOpen;
        public void CloseTelemetry() => telemetryPanel?.Close();
        public event Action StartRequested, MenuRequested, PauseRequested, BrakeRequested, SoundRequested, SettingsRequested, VibrationRequested;
        public event Action QualityRequested;
        public event Action<bool> SpeedChosen;
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
            var linesObject = new GameObject("Boost speed lines", typeof(RectTransform), typeof(CanvasRenderer), typeof(BoostSpeedLines));
            var linesRect = (RectTransform)linesObject.transform;
            linesRect.SetParent(raceHud.transform, false);
            linesRect.SetAsFirstSibling();
            linesRect.anchorMin = Vector2.zero;
            linesRect.anchorMax = Vector2.one;
            linesRect.offsetMin = linesRect.offsetMax = Vector2.zero;
            boostSpeedLines = linesObject.GetComponent<BoostSpeedLines>();
            boostSpeedLines.raycastTarget = false;
            linesObject.SetActive(false);
            CreateSpeedOffer();
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

        private void CreateSpeedOffer()
        {
            RectTransform Region(string name, Transform parent, Vector2 min, Vector2 max)
            {
                var region = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                region.SetParent(parent, false);
                region.anchorMin = min;
                region.anchorMax = max;
                region.offsetMin = region.offsetMax = Vector2.zero;
                return region;
            }

            TMPro.TextMeshProUGUI Label(string name, Transform parent, Vector2 min, Vector2 max, string text, float size)
            {
                var label = Region(name, parent, min, max).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                label.font = score.font;
                label.text = text;
                label.fontSize = size;
                label.enableAutoSizing = true;
                label.fontSizeMin = 11f;
                label.fontSizeMax = size;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.raycastTarget = false;
                return label;
            }

            UnityEngine.UI.Image Background(RectTransform region, Color color)
            {
                var image = region.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = color;
                image.raycastTarget = false;
                return image;
            }

            void Card(string name, Vector2 min, Vector2 max, Color color, string description, bool accelerate)
            {
                RectTransform card = Region(name, speedOffer, min, max);
                var image = Background(card, color);
                image.raycastTarget = true;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => SpeedChosen?.Invoke(accelerate));
                Label("Título", card, new Vector2(.05f, .58f), new Vector2(.95f, .94f), name, 23f).fontStyle = TMPro.FontStyles.Bold;
                Label("Detalle", card, new Vector2(.07f, .08f), new Vector2(.93f, .57f), description, 17f);
            }

            milestoneBar = Region("Hito de velocidad", raceHud.transform, new Vector2(.22f, .74f), new Vector2(.78f, .8f));
            Background(milestoneBar, new Color32(17, 29, 43, 235));
            milestoneFill = Region("Progreso", milestoneBar, new Vector2(.04f, .12f), new Vector2(.96f, .25f));
            Background(milestoneFill, new Color32(107, 221, 195, 255));
            milestoneLabel = Label("Estado del hito", milestoneBar, new Vector2(.03f, .27f), new Vector2(.97f, .97f), "", 17f);

            speedOffer = Region("Tarjetas de velocidad", raceHud.transform, new Vector2(.04f, .35f), new Vector2(.96f, .66f));
            Background(speedOffer, new Color32(16, 27, 39, 245));
            Label("Encabezado", speedOffer, new Vector2(.04f, .73f), new Vector2(.96f, .98f), "Elegí tu ritmo", 27f);
            Card("Mantener ritmo", new Vector2(.04f, .08f), new Vector2(.48f, .71f),
                new Color32(47, 71, 95, 255), "Velocidad y gemas habituales", false);
            Card("Acelerar", new Vector2(.52f, .08f), new Vector2(.96f, .71f),
                new Color32(121, 61, 36, 255), $"{RunSession.BoostSeconds:0} s · Gemas ×2\nMenos tiempo para esquivar", true);
            milestoneBar.gameObject.SetActive(false);
            speedOffer.gameObject.SetActive(false);
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
            if (boostSpeedLines != null) boostSpeedLines.Render(settings, session);
            if (impactLabel != null)
                impactLabel.gameObject.SetActive(running && Time.unscaledTime < impactUntil);
            RenderWordBanner(running);
            pause.interactable = !SettingsOpen && !session.OfferPending;
            if (speedOffer != null)
            {
                speedOffer.gameObject.SetActive(running && session.OfferPending);
                milestoneBar.gameObject.SetActive(running && !session.OfferPending && (!session.OfferResolved || session.BoostActive));
                float progress = session.BoostActive ? session.BoostProgress : session.OfferProgress;
                milestoneFill.anchorMax = new Vector2(Mathf.Lerp(.04f, .96f, progress), .25f);
                milestoneLabel.text = session.BoostActive ? "IMPULSO · GEMAS ×2" : "PRÓXIMA DECISIÓN · DISTANCIA";
            }
            score.text = session.Score.ToString(); gems.text = session.GemBasePoints.ToString();
            streak.text = $"RACHA {session.Streak}"; multiplier.text = $"×{session.Multiplier}";
            if (session.Multiplier > previousMultiplier) multiplierPulseUntil = Time.unscaledTime + .3f;
            previousMultiplier = session.Multiplier;
            float pulse = running ? Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .3f) : 0;
            multiplier.transform.parent.localScale = Vector3.one * (1 + Mathf.Sin(pulse * Mathf.PI) * .18f);
            streakFill.fillAmount = session.StreakProgress;
            int remainingSeconds = Mathf.CeilToInt(session.Remaining);
            remaining.text = $"{remainingSeconds / 60}:{remainingSeconds % 60:00}";
            gauge.SetSpeed(session.Speed, settings.InitialSpeed, settings.MaximumSpeed);
            soundLabel.text = muted ? "SONIDO: NO" : "SONIDO: SÍ";
            vibrationButton.interactable = devices.VibrationAvailable;
            vibrationLabel.text = !devices.VibrationAvailable ? "VIBRACIÓN: NO DISPONIBLE" : "VIBRACIÓN: " + devices.VibrationLevelLabel;
            if (session.State == RunState.Finished)
            {
                resultTitle.text = session.Ending == RunEnding.Brake ? "VOS MARCASTE\nEL FINAL" : "RECORRIDO\nCOMPLETADO";
                resultStats.text = $"<size=54><color=#FFD14C>{session.Score}</color></size>\nPUNTOS\n\n{session.Gems} gemas · {session.GemBasePoints} pts base\n{session.Distance:0} m · {session.Elapsed:0} s  ·  {SpeedGauge.DisplaySpeed(session.PeakSpeed):0} km/h máx.\n\nMejor racha: {session.BestStreak}  ·  ×{session.BestMultiplier}\nChoques: {session.Collisions}";
                if (session.VoluntaryBoosts > 0)
                    resultStats.text += $"\nAceleraciones elegidas: {session.VoluntaryBoosts} · Gemas extra: {session.BonusGems}";
            }
        }
    }
}
