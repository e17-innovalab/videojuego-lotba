using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Scene UI bindings. Configuration exposes only implemented, available capabilities.</summary>
    public sealed partial class GameHud : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text developmentLabel, identityFooter, buildVersion;
        [SerializeField] private GameObject readyPanel, pausePanel, resultPanel, raceHud, controls, settingsPanel;
        [SerializeField] private UnityEngine.UI.Button start, retry, menu, pause, resume, brake, pauseMenu, exit;
        [SerializeField] private UnityEngine.UI.Button[] configurationButtons;
        [SerializeField] private UnityEngine.UI.Button closeSettings, soundButton, vibrationButton;
        [SerializeField] private TMPro.TMP_Text soundLabel, vibrationLabel;
        [SerializeField] private UnityEngine.UI.Button qualityButton;
        [SerializeField] private TMPro.TMP_Text qualityLabel;
        [SerializeField] private UnityEngine.UI.Button previousQuality;
        [SerializeField] private UnityEngine.UI.Image soundSwitch;
        [SerializeField] private GameObject audioSliders;
        [SerializeField] private UnityEngine.UI.Slider musicVolumeSlider, effectsVolumeSlider;
        public event Action<float> MusicVolumeRequested, EffectsVolumeRequested;
        [SerializeField] private RectTransform soundSwitchKnob;
        private Vector2 soundOnMin, soundOnMax;
        [SerializeField] private TMPro.TMP_Text score, gems, resultTitle;
        [SerializeField] private TMPro.TMP_Text resultPoints;
        [SerializeField] private TMPro.TMP_Text[] resultRows = new TMPro.TMP_Text[6];
        [SerializeField] private UnityEngine.UI.Image endingBackground;
        [SerializeField] private CanvasGroup endingMessage, endingResults;
        [SerializeField] private TMPro.TMP_Text endingReflection;
        [SerializeField] private UnityEngine.UI.Button endingContinueButton;
        [SerializeField] private CanvasGroup endingContinueGroup;
        private int endingReflectionCharacters;
        private float endingStarted;
        private float endingContinuedAt = float.PositiveInfinity;
        [SerializeField] private Color resultsBackdrop = new Color32(12, 33, 50, 255);

        [SerializeField] private SpeedGauge gauge;
        [SerializeField] private RectTransform wordBanner;
        [SerializeField] private TMPro.TMP_Text wordLabel;
        private Vector2 wordBannerVisiblePosition;
        private float wordBannerStarted, wordBannerUntil;
        private const float ScorePulseDuration = .24f;
        private float scorePulseUntil;
        private int previousGemBalance;
        private long previousScore;
        [SerializeField] private TMPro.TMP_Text pinkGemMultiplier, pinkGemMiss;
        private float pinkGemMissUntil;
        private int displayedPinkGemMultiplier;
        private Transform pinkGemBadge;
        private Vector3 pinkGemBadgeRestScale;
        private float previousHudWidth;
        [SerializeField] private RectTransform speedOffer;
        [SerializeField] private GameObject modalShade, paceAlmostBadge, paceShortfallBadge;
        private Sprite availablePaceIcon, availableGemIcon;
        [SerializeField] private Sprite unavailablePaceIcon, unavailableGemIcon;
        [SerializeField] private UnityEngine.UI.Image paceCardIcon, paceCardGemIcon;
        private UnityEngine.UI.Graphic[] paceCardGraphics;
        private Color[] paceCardColors;
        [SerializeField] private UnityEngine.UI.Button increaseSpeedButton, keepSpeedButton;
        [SerializeField] private TMPro.TMP_Text increaseSpeedDetail, increaseSpeedPrice;
        [SerializeField] private RectTransform increaseSpeedPriceBadge;
        [SerializeField] private BoostSpeedLines boostSpeedLines;
        public void SetSpeedLinesOrigin(Vector2 screenPosition) => boostSpeedLines?.SetScreenOrigin(screenPosition);
        [SerializeField] private RectTransform milestoneBar, milestoneFill, milestoneRider;
        private Vector2 milestoneFillEnd, milestoneRiderMin, milestoneRiderMax;
        [SerializeField] private UnityEngine.UI.Button turboButton;
        [SerializeField] private UnityEngine.UI.Image turboFill;
        [SerializeField] private UnityEngine.UI.Image turboBolt;
        private Vector3 scoreRestScale;
        private HeldButtonInput brakeHoldInput;
        private BrakeProgressView brakeProgressView;
        public bool BrakeHeld => brakeHoldInput != null && brakeHoldInput.IsHeld && brake.isActiveAndEnabled && brake.IsInteractable();
        public void ClearBrakeHold() => brakeHoldInput?.Clear();
        public bool SettingsOpen { get; private set; }
        [SerializeField] private RunTelemetryPanel telemetryPanel;
        public bool TelemetryOpen => telemetryPanel != null && telemetryPanel.IsOpen;
        public void CloseTelemetry() => telemetryPanel?.Close();
        public event Action StartRequested, MenuRequested, PauseRequested, BrakeRequested, SoundRequested, SettingsRequested, VibrationRequested;
        public event Action<int> QualityRequested;
        public event Action TurboRequested;
        public event Action UiClicked;
        public event Action<bool> SpeedChosen;
        private void Awake()
        {
            brakeHoldInput = brake.GetComponent<HeldButtonInput>();
            brakeProgressView = brake.GetComponentInChildren<BrakeProgressView>(true);
            BindResponsiveWidths();
            ApplyIdentity();
            wordBannerVisiblePosition = wordBanner.anchoredPosition;
            wordBanner.gameObject.SetActive(false);
            soundOnMin = soundSwitchKnob.anchorMin;
            soundOnMax = soundSwitchKnob.anchorMax;
            milestoneFillEnd = milestoneFill.anchorMax;
            milestoneRiderMin = milestoneRider.anchorMin;
            milestoneRiderMax = milestoneRider.anchorMax;
            scoreRestScale = score.rectTransform.localScale;
            if (pinkGemMultiplier != null)
            {
                pinkGemBadge = pinkGemMultiplier.transform.parent;
                pinkGemBadgeRestScale = pinkGemBadge.localScale;
            }
            paceCardGraphics = increaseSpeedButton.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            paceCardColors = Array.ConvertAll(paceCardGraphics, graphic => graphic.color);
            availablePaceIcon = paceCardIcon.sprite;
            availableGemIcon = paceCardGemIcon.sprite;
            BindRanking();
            telemetryPanel.Bind();
            previousQuality.onClick.AddListener(() => QualityRequested?.Invoke(-1));
            keepSpeedButton.onClick.AddListener(() => SpeedChosen?.Invoke(false));
            increaseSpeedButton.onClick.AddListener(() => SpeedChosen?.Invoke(true));
            turboButton.onClick.AddListener(() => TurboRequested?.Invoke());
            exit.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
            exit.onClick.AddListener(ExitGame);
            start.onClick.AddListener(() => StartRequested?.Invoke()); retry.onClick.AddListener(() => StartRequested?.Invoke());
            pauseMenu.onClick.AddListener(() => MenuRequested?.Invoke());
            menu.onClick.AddListener(() => MenuRequested?.Invoke());
            pause.onClick.AddListener(() => PauseRequested?.Invoke()); resume.onClick.AddListener(() => PauseRequested?.Invoke());
            brake.onClick.AddListener(() => BrakeRequested?.Invoke());
            endingContinueButton.onClick.AddListener(ContinueEnding);
            foreach (var button in configurationButtons) button.onClick.AddListener(() => SettingsRequested?.Invoke());
            closeSettings.onClick.AddListener(() => SettingsOpen = false);
            soundButton.onClick.AddListener(() => SoundRequested?.Invoke());
            musicVolumeSlider.onValueChanged.AddListener(value => MusicVolumeRequested?.Invoke(value));
            effectsVolumeSlider.onValueChanged.AddListener(value => EffectsVolumeRequested?.Invoke(value));
            vibrationButton.onClick.AddListener(() => VibrationRequested?.Invoke());
            qualityButton.onClick.AddListener(() => QualityRequested?.Invoke(1));
            foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button != brake && button != turboButton)
                    button.onClick.AddListener(() => UiClicked?.Invoke());
        }

        private void BindResponsiveWidths()
        {
            ReadablePanelWidth.Wrap(speedOffer);
            ReadablePanelWidth.Wrap(wordBanner);
            ReadablePanelWidth.Wrap(pausePanel != null ? pausePanel.transform as RectTransform : null);
            ReadablePanelWidth.Wrap(resultPanel != null ? resultPanel.transform as RectTransform : null);
            ReadablePanelWidth.Wrap(endingMessage != null ? endingMessage.transform as RectTransform : null);
            ReadablePanelWidth.Wrap(settingsPanel != null ? settingsPanel.transform.Find("Marco") as RectTransform : null);
            ReadablePanelWidth.Wrap(rankingCard);
            if (developmentOverlay != null)
                ReadablePanelWidth.Wrap(developmentOverlay.transform.Find("Card") as RectTransform);
        }

        public void SetAudioLevels(float music, float effects)
        {
            musicVolumeSlider.SetValueWithoutNotify(music);
            effectsVolumeSlider.SetValueWithoutNotify(effects);
        }

        private void RenderEnding(RunEnding ending, bool opening, GameSettings settings, BrakingMode brakingMode)
        {
            if (opening)
            {
                endingStarted = Time.unscaledTime;
                endingContinuedAt = float.PositiveInfinity;
                endingBackground.gameObject.SetActive(true);
                if (ending != RunEnding.RouteComplete)
                {
                    endingReflection.maxVisibleCharacters = 0;
                    endingReflection.ForceMeshUpdate();
                    endingReflectionCharacters = endingReflection.textInfo.characterCount;
                }
            }
            if (ending == RunEnding.RouteComplete)
            {
                endingBackground.color = Color.clear;
                endingMessage.alpha = 0f;
                endingMessage.blocksRaycasts = false;
                endingContinueButton.interactable = false;
                endingResults.alpha = 1f;
                endingResults.interactable = endingResults.blocksRaycasts = true;
                resultPanel.SetActive(!TelemetryOpen && !RankingOpen);
                return;
            }
            float age = Time.unscaledTime - endingStarted;
            float fadeStart = ending == RunEnding.Brake ? RunnerWorld.BrakeSlideSeconds : 0f;
            if (ending == RunEnding.Brake && brakingMode == BrakingMode.HoldToStop && settings.BrakeEffectsPrefab != null)
                fadeStart = settings.BrakeEffectsPrefab.SettleSeconds;
            float fadeDuration = ending == RunEnding.Brake ? settings.EndingBrakeFadeSeconds : 0f;
            float blackoutAt = fadeStart + fadeDuration;
            float blackout = fadeDuration > 0f
                ? Mathf.SmoothStep(0f, 1f, (age - fadeStart) / fadeDuration)
                : age >= blackoutAt ? 1f : 0f;
            float artworkAge = age - blackoutAt;
            float messageAge = artworkAge - settings.EndingArtworkFadeSeconds;
            float charactersPerSecond = settings.EndingCharactersPerSecond;
            int typed = Mathf.FloorToInt(Mathf.Max(0f, messageAge) * charactersPerSecond);
            endingReflection.maxVisibleCharacters = Mathf.Min(typed, endingReflectionCharacters);
            float buttonAge = messageAge - endingReflectionCharacters / charactersPerSecond - settings.EndingReadingHoldSeconds;
            float resultsAge = Time.unscaledTime - endingContinuedAt;
            float reveal = resultsAge >= 0f ? 1f : 0f;
            float buttonReveal = FadeIn(buttonAge, settings.EndingContinueFadeSeconds);
            bool canContinue = float.IsPositiveInfinity(endingContinuedAt) && buttonReveal >= 1f;
            endingMessage.alpha = FadeIn(artworkAge, settings.EndingArtworkFadeSeconds) * (1f - reveal);
            endingMessage.blocksRaycasts = endingContinueButton.interactable = canContinue;
            endingContinueGroup.alpha = buttonReveal;
            Color background = Color.Lerp(settings.Brand.Black, resultsBackdrop, reveal);
            background.a = blackout;
            endingBackground.color = background;
            endingResults.alpha = reveal;
            endingResults.interactable = endingResults.blocksRaycasts = reveal >= 1f;
            resultPanel.SetActive(resultsAge >= 0f && !TelemetryOpen && !RankingOpen);
        }

        private void ContinueEnding()
        {
            if (endingContinueButton.interactable && float.IsPositiveInfinity(endingContinuedAt))
                endingContinuedAt = Time.unscaledTime;
        }

        private static float FadeIn(float age, float duration) =>
            age < 0f ? 0f : duration > 0f ? Mathf.SmoothStep(0f, 1f, age / duration) : 1f;

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
            SettingsOpen = false;
            ClearRankingOffer();
            endingBackground.gameObject.SetActive(false);
            scorePulseUntil = 0f;
            pinkGemMissUntil = 0f;
            previousGemBalance = -1;
            previousScore = -1;
            previousHudWidth = -1f;
            telemetryPanel?.SetReport(null);
            wordBannerUntil = 0;
            if (wordBanner != null) wordBanner.gameObject.SetActive(false);
        }

        private static void ResizeCounter(TMPro.TMP_Text label, float maxWidth)
        {
            var panel = (RectTransform)label.transform.parent;
            float padding = -label.rectTransform.sizeDelta.x;
            float textWidth = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Min(textWidth + padding, maxWidth));
        }

        public void ShowPinkGemMiss() => pinkGemMissUntil = Time.unscaledTime + 1.1f;

        private void RenderPinkGemStreak(RunSession session, bool running)
        {
            bool visible = running && !session.OfferPending;
            if (pinkGemBadge != null)
            {
                int multiplier = session.ScoreMultiplier;
                pinkGemBadge.gameObject.SetActive(visible && session.PinkGemStreak > 0);
                if (displayedPinkGemMultiplier != multiplier)
                {
                    displayedPinkGemMultiplier = multiplier;
                    pinkGemMultiplier.text = $"×{multiplier}";
                }
            }
            if (pinkGemMiss != null)
                pinkGemMiss.transform.parent.gameObject.SetActive(running && Time.unscaledTime < pinkGemMissUntil);
        }


        public void ShowWordProgress(string word, bool[] collected, bool complete)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < word.Length; i++)
            {
                if (word[i] == ' ')
                {
                    text.Append("   ");
                    continue;
                }
                if (i > 0 && word[i - 1] != ' ') text.Append(' ');
                text.Append(collected[i] ? word[i] : '_');
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
            qualityLabel.text = label;
            qualityButton.interactable = canChange;
            if (previousQuality != null) previousQuality.interactable = canChange;
        }
        public bool IsRaceButtonAt(Vector2 screenPoint) =>
            IsButtonAt(pause, screenPoint) || IsButtonAt(brake, screenPoint) || IsButtonAt(turboButton, screenPoint);

        private static bool IsButtonAt(UnityEngine.UI.Button button, Vector2 screenPoint) =>
            button.isActiveAndEnabled && button.IsInteractable() &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, screenPoint);
        public void Render(RunSession session, GameSettings settings, bool muted, DeviceFeedbackAndInput devices, bool showBoostLines)
        {
            bool running = session.State == RunState.Running;
            bool active = running || session.State == RunState.Paused;
            bool finished = session.State == RunState.Finished;
            bool openingEnding = finished && !endingBackground.gameObject.activeSelf;
            if (finished) SettingsOpen = false;
            readyPanel.SetActive(session.State == RunState.Ready && !SettingsOpen && !RankingOpen);
            bool showPause = session.State == RunState.Paused && !SettingsOpen;
            bool showOffer = running && session.OfferPending && !session.RecoveryImpactPending;
            pausePanel.SetActive(showPause);
            if (modalShade != null) modalShade.SetActive(showPause || showOffer);
            settingsPanel.SetActive(SettingsOpen);
            telemetryPanel?.SetReport(finished ? session.Telemetry : null);
            if (finished) RenderEnding(session.Ending, openingEnding, settings, session.BrakeMode);
            else resultPanel.SetActive(false);
            raceHud.SetActive(active); controls.SetActive(active);
            if (brakeProgressView != null)
                brakeProgressView.Render(active && !SettingsOpen && session.BrakeMode == BrakingMode.HoldToStop, session.BrakeProgress);
            turboButton.interactable = running && !SettingsOpen && session.CanActivateTurbo;
            turboFill.fillAmount = session.TurboChargeProgress;
            turboBolt.enabled = turboButton.interactable && turboBolt.sprite != null;

            if (boostSpeedLines != null) boostSpeedLines.Render(settings, session, showBoostLines);
            RenderWordBanner(running);
            RenderPinkGemStreak(session, running);
            pause.interactable = !SettingsOpen && !session.OfferPending;
            if (speedOffer != null)
            {
                speedOffer.gameObject.SetActive(showOffer && !session.RecoveryOfferPending);
                if (speedOffer.gameObject.activeSelf)
                {
                    bool upgrade = session.UpgradeCost > 0;
                    bool availableToBuy = !upgrade || session.CanBuySpeed;
                    bool showShortfall = upgrade && !availableToBuy;
                    if (paceShortfallBadge != null) paceShortfallBadge.SetActive(showShortfall);
                    if (paceAlmostBadge != null)
                        paceAlmostBadge.SetActive(showShortfall);
                    if (increaseSpeedButton.interactable != availableToBuy)
                    {
                        increaseSpeedButton.interactable = availableToBuy;
                        for (int i = 0; i < paceCardGraphics.Length; i++)
                        {
                            Color color = paceCardColors[i];
                            float gray = color.grayscale;
                            paceCardGraphics[i].color = availableToBuy ? color : new Color(gray, gray, gray, color.a);
                        }
                        paceCardIcon.sprite = availableToBuy ? availablePaceIcon : unavailablePaceIcon;
                        paceCardGemIcon.sprite = availableToBuy ? availableGemIcon : unavailableGemIcon;
                    }
                    increaseSpeedPriceBadge.gameObject.SetActive(upgrade);
                    if (upgrade)
                    {
                        string price = showShortfall
                            ? $"{session.GemBalance} / {session.UpgradeCost}"
                            : session.UpgradeCost.ToString();
                        increaseSpeedPrice.text = price;
                        float available = ((RectTransform)increaseSpeedPriceBadge.parent).rect.width * .88f;
                        float preferred = increaseSpeedPrice.GetPreferredValues(price).x;
                        increaseSpeedPriceBadge.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                            Mathf.Min(Mathf.Max(64f, preferred + 42f), Mathf.Max(48f, available)));
                    }
                    increaseSpeedDetail.text = upgrade
                        ? session.GemBalance < session.UpgradeCost ? $"Te faltan {session.UpgradeCost - session.GemBalance}" : ""
                        : $"Gratis · {session.SpeedOfferTurbo.DurationSeconds:0.#} s\nGemas ×{session.SpeedOfferTurbo.GemMultiplier} · Puntos ×{session.SpeedOfferTurbo.PointMultiplier}";
                }
                milestoneBar.gameObject.SetActive(active && session.HasDecisionProgress &&
                    !session.RecoveryImpactPending && !session.RecoveryOfferPending &&
                    !session.RecoveryPreparing && !session.RecoveryActive && !session.RecoveryRejoining);
                float progress = Mathf.Lerp(milestoneFill.anchorMin.y, milestoneFillEnd.y, session.OfferProgress);
                milestoneFill.anchorMax = new Vector2(milestoneFillEnd.x, progress);
                milestoneRider.anchorMin = new Vector2(milestoneRiderMin.x, milestoneRiderMin.y + progress);
                milestoneRider.anchorMax = new Vector2(milestoneRiderMax.x, milestoneRiderMax.y + progress);
            }
            RenderRecovery(session, settings);
            float hudWidth = ((RectTransform)raceHud.transform).rect.width;
            bool hudResized = !Mathf.Approximately(hudWidth, previousHudWidth);
            previousHudWidth = hudWidth;
            if (hudResized || session.GemBalance != previousGemBalance)
            {
                previousGemBalance = session.GemBalance;
                gems.text = $"{session.GemBalance} ×";
                ResizeCounter(gems, hudWidth * .27f);
            }
            if (hudResized || session.Score != previousScore)
            {
                if (running && previousScore >= 0 && session.Score > previousScore)
                    scorePulseUntil = Time.unscaledTime + ScorePulseDuration;
                previousScore = session.Score;
                score.text = session.Score.ToString();
                ResizeCounter(score, hudWidth * .40f);
            }
            float scorePulse = running ? Mathf.Clamp01((scorePulseUntil - Time.unscaledTime) / ScorePulseDuration) : 0f;
            float scoreScale = 1f + Mathf.Sin(scorePulse * Mathf.PI) * .18f;
            score.rectTransform.localScale = scoreRestScale * scoreScale;
            if (pinkGemBadge != null) pinkGemBadge.localScale = pinkGemBadgeRestScale * scoreScale;
            gauge.SetSpeed(session.Speed, session.PurchasedSpeed, session.Elapsed, settings.Brand,
                running && !session.OfferPending);
            if (SettingsOpen)
            {
                audioSliders.SetActive(!muted);
                soundSwitch.color = muted ? Color.Lerp(settings.Brand.Black, settings.Brand.White, .35f) : settings.Brand.Green;
                soundSwitchKnob.anchorMin = muted ? new Vector2(1f - soundOnMax.x, soundOnMin.y) : soundOnMin;
                soundSwitchKnob.anchorMax = muted ? new Vector2(1f - soundOnMin.x, soundOnMax.y) : soundOnMax;
                vibrationButton.interactable = devices.VibrationAvailable;
                vibrationLabel.text = devices.VibrationAvailable ? devices.VibrationLevelLabel : "NO DISPONIBLE";
                vibrationLabel.color = devices.VibrationAvailable ? settings.Brand.White : settings.Brand.Pink;
            }
            if (openingEnding)
            {
                SetRankingOffer(session.Score);
                resultTitle.text = session.Ending switch
                {
                    RunEnding.Brake => "VOS MARCASTE\nEL FINAL",
                    RunEnding.Overload => "FIN DE LA\nPARTIDA",
                    _ => "RECORRIDO\nCOMPLETADO"
                };
                resultPoints.text = $"{session.Score:N0} PUNTOS";
                resultRows[0].text = $"Recorriste {ResultHighlight($"{session.Distance / 1000f:0.0#} km", settings.Brand.White)}";
                resultRows[1].text = $"Recolectaste {ResultHighlight($"{session.Gems} gemas", settings.Brand.Pink)}";
                resultRows[2].text = $"Chocaste {ResultHighlight(Times(session.Collisions), settings.Brand.Red)}";
                resultRows[3].text = session.RecoveryOutcome switch
                {
                    "succeeded" => $"Recuperaste {ResultHighlight($"{session.GemsRecovered} gemas", settings.Brand.Green)}",
                    "failed" => "Intentaste recuperar y chocaste",
                    "interrupted" => "Recupero interrumpido al terminar",
                    "declined" => "Elegiste seguir sin intentar recuperar",
                    _ => $"Intentos de recupero: {session.RecoveryAttempts}"
                };
                resultRows[4].text = $"Aceleraste {ResultHighlight(Times(session.VoluntaryBoosts + session.SpeedUpgrades), settings.Brand.Yellow)}";
                if (resultRows.Length > 5 && resultRows[5] != null)
                    resultRows[5].text = $"Racha máxima {ResultHighlight($"×{session.BestPinkGemStreak}", settings.Brand.Yellow)}";
            }
        }

        private static string Times(int count) => count == 1 ? "1 vez" : $"{count} veces";
        private static string ResultHighlight(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{text}</b></color>";
    }
}
