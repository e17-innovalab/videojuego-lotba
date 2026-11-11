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
        [SerializeField] private TMPro.TMP_Text score, gems, multiplier, resultTitle, resultStats;
        [SerializeField] private Sprite flameOutline, flameFill;
        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private RectTransform wordBanner;
        [SerializeField] private TMPro.TMP_Text wordLabel;
        private Vector2 wordBannerVisiblePosition;
        private float wordBannerStarted, wordBannerUntil;
        private float multiplierPulseUntil;
        private float scorePulseUntil;
        private long previousScore;
        private RectTransform speedOffer;
        private BoostSpeedLines boostSpeedLines;
        private RectTransform milestoneBar;
        private RectTransform milestoneFill;
        private UnityEngine.UI.Image multiplierFill;
        private TMPro.TextMeshProUGUI milestoneLabel;
        private RectTransform boostTimeFill;
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

            milestoneBar = Region("Próxima decisión", raceHud.transform, new Vector2(.035f, .44f), new Vector2(.087f, .72f));
            var track = Region("Carril de progreso", milestoneBar, Vector2.zero, new Vector2(1f, .82f));
            Background(track, new Color32(17, 29, 43, 235));
            milestoneFill = Region("Progreso", track, new Vector2(.12f, .02f), new Vector2(.88f, .98f));
            Background(milestoneFill, new Color32(107, 221, 195, 255));
            var cardsIcon = Region("Icono de tres cartas", milestoneBar, new Vector2(-.28f, .87f), new Vector2(1.28f, 1f));
            for (int i = -1; i <= 1; i++)
            {
                var card = Region("Carta", cardsIcon, new Vector2(.27f + .18f * i, .1f), new Vector2(.73f + .18f * i, .9f));
                card.localRotation = Quaternion.Euler(0f, 0f, -i * 19f);
                Background(card, new Color32(145, 195, 217, 255));
                Background(Region("Centro", card, new Vector2(.16f, .12f), new Vector2(.84f, .88f)), new Color32(28, 54, 72, 255));
            }
            cardsIcon.GetChild(1).SetAsLastSibling();
            milestoneLabel = Label("Indicador de impulso", raceHud.transform, new Vector2(.28f, .69f), new Vector2(.72f, .74f), "IMPULSO · GEMAS ×2", 17f);
            var boostTrack = Region("Tiempo de impulso", milestoneLabel.transform, new Vector2(.12f, 0f), new Vector2(.88f, .09f));
            Background(boostTrack, new Color32(17, 29, 43, 235));
            boostTimeFill = Region("Tiempo restante", boostTrack, Vector2.zero, Vector2.one);
            Background(boostTimeFill, new Color32(107, 221, 195, 255));
            milestoneLabel.gameObject.SetActive(false);

            Transform badge = multiplier.transform.parent;
            var flameRegion = Region("Fuego", badge, new Vector2(.05f, .06f), new Vector2(.49f, .94f));
            multiplierFill = Background(Region("Relleno", flameRegion, Vector2.zero, Vector2.one), new Color32(255, 159, 45, 255));
            multiplierFill.sprite = flameFill;
            multiplierFill.preserveAspect = true;
            multiplierFill.type = UnityEngine.UI.Image.Type.Filled;
            multiplierFill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            multiplierFill.fillAmount = 0f;
            var outline = Background(Region("Contorno", flameRegion, Vector2.zero, Vector2.one), new Color32(255, 221, 164, 255));
            outline.sprite = flameOutline;
            outline.preserveAspect = true;

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
            scorePulseUntil = 0f;
            previousScore = 0;
            telemetryPanel?.SetReport(null);
            wordBannerUntil = 0;
            if (wordBanner != null) wordBanner.gameObject.SetActive(false);
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
        public void Render(RunSession session, GameSettings settings, bool muted, DeviceFeedbackAndInput devices, bool showBoostLines)
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
            if (boostSpeedLines != null) boostSpeedLines.Render(settings, session, showBoostLines);
            RenderWordBanner(running);
            pause.interactable = !SettingsOpen && !session.OfferPending;
            if (speedOffer != null)
            {
                speedOffer.gameObject.SetActive(running && session.OfferPending);
                milestoneBar.gameObject.SetActive(active);
                milestoneFill.anchorMax = new Vector2(.88f, Mathf.Lerp(.02f, .98f, session.OfferProgress));
                milestoneLabel.gameObject.SetActive(running && session.BoostActive);
                boostTimeFill.anchorMax = new Vector2(session.BoostProgress, 1f);
            }
            if (session.Score > previousScore) scorePulseUntil = Time.unscaledTime + .24f;
            previousScore = session.Score;
            float scorePulse = running ? Mathf.Clamp01((scorePulseUntil - Time.unscaledTime) / .24f) : 0f;
            score.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(scorePulse * Mathf.PI) * .25f);
            score.text = session.Score.ToString(); gems.text = $"{session.GemBalance} ×";
            multiplier.text = $"×{session.Multiplier}";
            if (multiplierFill != null) multiplierFill.fillAmount = session.MultiplierProgress;
            if (session.Multiplier > previousMultiplier) multiplierPulseUntil = Time.unscaledTime + .3f;
            previousMultiplier = session.Multiplier;
            float pulse = running ? Mathf.Clamp01((multiplierPulseUntil - Time.unscaledTime) / .3f) : 0;
            multiplier.transform.parent.localScale = Vector3.one * (1 + Mathf.Sin(pulse * Mathf.PI) * .18f);
            gauge.SetSpeed(session.Speed, settings.InitialSpeed, settings.MaximumSpeed);
            soundLabel.text = muted ? "SONIDO: NO" : "SONIDO: SÍ";
            vibrationButton.interactable = devices.VibrationAvailable;
            vibrationLabel.text = !devices.VibrationAvailable ? "VIBRACIÓN: NO DISPONIBLE" : "VIBRACIÓN: " + devices.VibrationLevelLabel;
            if (session.State == RunState.Finished)
            {
                resultTitle.text = session.Ending == RunEnding.Brake ? "VOS MARCASTE\nEL FINAL" : "RECORRIDO\nCOMPLETADO";
                resultStats.text = $"<size=54><color=#FFD14C>{session.Score}</color></size>\nPUNTOS\n\nGemas: {session.Gems} recogidas · {session.GemsLost} perdidas · {session.GemBalance} saldo\n{session.Distance:0} m · {session.Elapsed:0} s  ·  {SpeedGauge.DisplaySpeed(session.PeakSpeed):0} km/h máx.\n\nMultiplicador: ×{session.Multiplier}\nChoques: {session.Collisions} · {session.BoostCollisions} con impulso\nDecisiones: {session.DecisionsKept} mantener · {session.VoluntaryBoosts} acelerar";
            }
        }
    }
}
