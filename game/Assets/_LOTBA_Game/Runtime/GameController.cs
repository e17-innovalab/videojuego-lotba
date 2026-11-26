using UnityEngine;
using UnityEngine.InputSystem;

namespace Lotba.Game
{
    /// <summary>Input ordering, capability-gated devices and scene composition. Completed braking preempts simulation.</summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        [SerializeField] private RunnerWorld world;
        [SerializeField] private GameHud hud;
        [SerializeField] private AudioManager audioFeedback;
        [SerializeField] private DeviceFeedbackAndInput devices;
        private RunSession session;
        private SessionReportTransport reportTransport;
        private JourneySession journeySession;
        private float nextReportSaveAttempt;
        private bool startRequested, brakeRequested, jumpRequested, pauseRequested, menuRequested, sessionReportSaved;
        private bool overloadRequested;
        private int laneRequested;
        private bool? speedChoiceRequested;
        private bool recoveryChoiceRequested;
        private bool? queuedSpeedChoice;
        private bool queuedRecoveryChoice, decisionCountdownActive, decisionChoiceReady;
        private float decisionCountdownRemaining;
        private string previousRecoveryOutcome;
        private bool muted, trackingSwipe;
        private const string QualityPreference = "Lotba.CityRush.Quality";
        private static readonly string[] QualityNames = { "Low", "Medium", "High" };
        private static readonly string[] QualityLabels = { "BAJA", "MEDIA", "ALTA" };
        private readonly int[] qualityLevels = new int[QualityNames.Length];
        private int qualityChoice = -1;
        private bool IsHighQuality => qualityChoice >= 0 && QualityNames[qualityChoice] == "High";
        private bool UseBoostCamera => qualityChoice > 0;
        private Vector2 swipeOrigin;
        private void Awake()
        {
            reportTransport = gameObject.AddComponent<SessionReportTransport>();
            journeySession = JourneySessionStore.Load();
            hud.InitializeJourneyAdmin(reportTransport, journeySession, ContinueJourney, CloseJourney);
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 60;
            muted = PlayerPrefs.GetInt("Lotba.CityRush.Muted", 0) != 0;
            InitializeQuality();
            audioFeedback.SetMuted(muted); devices.Initialize();
            world.Initialize(settings); world.ResetTrack(); session = new RunSession(settings, world.Route);
            if (IsHighQuality) world.InitializeTurboAura();
            world.ConfigureDepthBend(IsHighQuality);
            world.ConfigurePickupEffects(UseBoostCamera);
            world.ConfigureGemGlow(IsHighQuality);
            hud.StartRequested += RequestStart; hud.JourneyAdminRequested += hud.OpenJourneyAdmin; hud.BrakeRequested += RequestBrake;
            hud.PauseRequested += RequestPause; hud.MenuRequested += RequestMenu;
            hud.SoundRequested += ToggleSound; hud.SettingsRequested += OpenSettings;
            hud.MusicVolumeRequested += audioFeedback.SetMusicLevel;
            hud.EffectsVolumeRequested += audioFeedback.SetEffectsLevel;
            hud.VibrationRequested += devices.CycleVibrationLevel;
            hud.QualityRequested += CycleQuality;
            hud.SpeedChosen += ChooseSpeed;
            hud.RecoveryChosen += ChooseRecovery;
            hud.TurboRequested += ActivateTurbo;
            hud.UiClicked += audioFeedback.PlayUiClick;
            world.GemFeedback += OnGem;
            world.PinkGemMissed += OnPinkGemMissed;
            world.ImpactFeedback += OnImpact;
            world.LetterCollected += OnLetterCollected;
        }

        private void Start()
        {
            hud.SetAudioLevels(audioFeedback.MusicLevel, audioFeedback.EffectsLevel);
            world.PrepareOpening();
            hud.Render(session, settings, muted, devices, IsHighQuality);
        }
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (hud.DevelopmentOpen)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) hud.CloseDevelopment();
                return;
            }
            if (hud.RankingOpen)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) hud.CloseRanking();
                return;
            }
            if (hud.TelemetryOpen)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) hud.CloseTelemetry();
                return;
            }
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) RequestBrake();
            if (session.OfferPending) return;
            if (hud.SettingsOpen) return;
            ReadSwipe();
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) RequestPause();
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) RequestLane(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) RequestLane(1);
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) jumpRequested = true;
        }
        private void LateUpdate()
        {
            bool endForOverload = overloadRequested;
            overloadRequested = false;
            world.ConfigureDepthBend(IsHighQuality);
            if (menuRequested && (session.State == RunState.Finished || session.State == RunState.Paused))
            {
                ResetRun(false);
                audioFeedback.UpdateMusic(session.State);
                speedChoiceRequested = null;
                recoveryChoiceRequested = false;
                startRequested = brakeRequested = jumpRequested = pauseRequested = menuRequested = false; laneRequested = 0;
                hud.Render(session, settings, muted, devices, IsHighQuality);
                return;
            }
            if (startRequested && session.State == RunState.Ready)
            {
                session.Start();
            }
            else if (startRequested && session.State == RunState.Finished)
            {
                ResetRun(true);
            }
            bool wasFinished = session.State == RunState.Finished;
            session.AdvancePresentationClock(Time.unscaledDeltaTime);
            float speedBeforeBrake = session.Speed;
            bool heldBrake = Application.isFocused && !hud.SettingsOpen &&
                (hud.BrakeHeld || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed));
            bool wasBrakeHeld = session.BrakeHeld;
            session.UpdateBrakeInput(heldBrake, brakeRequested, Mathf.Min(Time.unscaledDeltaTime, .15f));
            if (session.BrakeMode == BrakingMode.HoldToStop)
            {
                if (!wasBrakeHeld && session.BrakeHeld) audioFeedback.PlayBrakePress();
                else if (wasBrakeHeld && !session.BrakeHeld) audioFeedback.StopBrakePress();
            }
            if (session.State != RunState.Finished && endForOverload) session.EndForOverload();
            else if (session.State != RunState.Finished)
            {
                if (pauseRequested)
                {
                    if (session.State == RunState.Paused) { session.Resume(); }
                    else
                    {
                        session.Pause();
                        audioFeedback.Stop();
                        devices.CancelFeedback();
                        Debug.Log($"[PAUSA] Metro: {session.Distance:F1}m · Semilla: {settings.Seed} · Velocidad: {session.Speed:F1}m/s");
                    }
                }
                if (!decisionCountdownActive && !decisionChoiceReady && session.State == RunState.Running)
                {
                    if (speedChoiceRequested.HasValue)
                    {
                        queuedSpeedChoice = speedChoiceRequested;
                        speedChoiceRequested = null;
                        StartDecisionCountdown();
                    }
                    else if (recoveryChoiceRequested)
                    {
                        queuedRecoveryChoice = true;
                        recoveryChoiceRequested = false;
                        StartDecisionCountdown();
                    }
                }

                if (decisionCountdownActive && session.State == RunState.Running)
                {
                    decisionCountdownRemaining -= Time.unscaledDeltaTime;
                    if (decisionCountdownRemaining <= 0f)
                    {
                        decisionCountdownActive = false;
                        decisionChoiceReady = true;
                        hud.ShowDecisionCountdown(false, 0);
                    }
                    else hud.ShowDecisionCountdown(true, Mathf.CeilToInt(decisionCountdownRemaining), queuedRecoveryChoice);
                }

                if (decisionChoiceReady && session.State == RunState.Running)
                {
                    if (queuedSpeedChoice.HasValue)
                    {
                        int previousBoosts = session.VoluntaryBoosts;
                        session.ChooseTransitionUpgrade(queuedSpeedChoice.Value);
                        if (session.VoluntaryBoosts > previousBoosts) audioFeedback.PlayBoostStart();
                        queuedSpeedChoice = null;
                    }
                    else if (queuedRecoveryChoice)
                    {
                        session.ChooseTransitionRecovery();
                        queuedRecoveryChoice = false;
                        if (session.RecoveryActive)
                            world.PrepareRecoveryChallenge(session);
                        else
                            world.ClearGemsAhead(session.Distance, Mathf.Max(session.RecoveryZoneEnd + 10f, session.Distance + 600f));
                    }
                    decisionChoiceReady = false;
                    if (!session.OfferPending) { laneRequested = 0; jumpRequested = false; trackingSwipe = false; }
                }
                if (session.State == RunState.Running)
                {
                    if (laneRequested != 0 && session.Move(laneRequested)) audioFeedback.PlayLaneSlide();
                    if (jumpRequested && world.TryJump(session.Elapsed))
                    {
                        session.RecordJump();
                        audioFeedback.PlayJump();
                    }
                    float budget = Mathf.Min(Time.unscaledDeltaTime, .15f);
                    while (budget > 0 && session.State == RunState.Running)
                    {
                        float step = Mathf.Min(.02f, budget); session.Tick(step);
                        if (session.State == RunState.Running)
                        {
                            float obstacleMult = session.RecoveryActive ? 1.6f : (1f + .75f * session.AccumulatedRisk);
                            world.SetEncounterIntensity(1f + .35f * session.AccumulatedRisk,
                                settings.BaseObstacleIntensity * obstacleMult);
                            int previousDecisions = session.DecisionsShown;
                            world.Advance(session, step, false, UseBoostCamera, IsHighQuality);
                            if (session.DecisionsShown > previousDecisions) audioFeedback.PlayDecisionShown();
                        }
                        budget -= step;
                    }
                    if (previousRecoveryOutcome != session.RecoveryOutcome)
                    {
                        previousRecoveryOutcome = session.RecoveryOutcome;
                        if (session.RecoveryOutcome == "succeeded")
                        {
                            audioFeedback.PlayRecoveryCelebration();
                            devices.Feedback(false);
                        }
                        else if (session.RecoveryOutcome == "failed")
                        {
                            audioFeedback.PlayRecoveryNearMiss();
                        }
                    }
                }
            }
            if (!wasFinished && session.State == RunState.Finished)
            {
                bool braked = session.Ending == RunEnding.Brake;
                bool progressiveBrake = braked && session.BrakeMode == BrakingMode.HoldToStop;
                audioFeedback.EndRun(braked && !progressiveBrake, progressiveBrake);
                devices.CancelFeedback();
                world.EndRun(braked, speedBeforeBrake, UseBoostCamera,
                    braked && session.BrakeMode == BrakingMode.HoldToStop);
            }
            if (session.State == RunState.Finished && !sessionReportSaved && Time.unscaledTime >= nextReportSaveAttempt)
            {
                nextReportSaveAttempt = Time.unscaledTime + 5f;
                string reportPath = SessionReportWriter.Save(session.Telemetry, journey: journeySession?.Snapshot());
                sessionReportSaved = reportPath != null;
                if (sessionReportSaved) reportTransport.TrackSaved(reportPath);
            }
            world.RenderRecoveryCurtain(session);
            world.RenderRecoveryFork(session);
            world.RenderSectionMarkers(session.Distance, session.State == RunState.Running || session.State == RunState.Paused, session.RecoveryForkActive);
            world.RenderHeldBrake(session.BrakeMode == BrakingMode.HoldToStop && session.BrakeHeld &&
                (session.State == RunState.Running || session.State == RunState.Paused),
                session.State == RunState.Running,
                UseBoostCamera, session.Speed, !session.BrakingSpeedRestricted);
            devices.UpdateBoostFeedback(session.State == RunState.Running && !session.OfferPending && session.BoostActive);
            if (session.State != RunState.Finished) audioFeedback.UpdateMusic(session.State);
            speedChoiceRequested = null;
            recoveryChoiceRequested = false;
            startRequested = brakeRequested = jumpRequested = pauseRequested = menuRequested = false; laneRequested = 0;
            hud.SetSpeedLinesOrigin(world.RiderScreenPosition);
            hud.Render(session, settings, muted, devices, IsHighQuality);
        }
        private void ResetRun(bool start)
        {
            hud.ClearBrakeHold();
            sessionReportSaved = false;
            nextReportSaveAttempt = 0f;
            previousRecoveryOutcome = null;
            queuedSpeedChoice = null; queuedRecoveryChoice = decisionCountdownActive = decisionChoiceReady = false;
            decisionCountdownRemaining = 0f; hud.ShowDecisionCountdown(false, 0);
            session = new RunSession(settings, world.Route); world.ResetTrack(); hud.ResetView(); audioFeedback.Stop(); devices.CancelFeedback(); trackingSwipe = false;
            if (start) { session.Start(); }
            else world.PrepareOpening();
        }
        private void ReadSwipe()
        {
            Pointer pointer = Pointer.current; if (pointer == null) return;
            if (pointer.press.wasPressedThisFrame)
            {
                swipeOrigin = pointer.position.ReadValue();
                trackingSwipe = session.State == RunState.Running && !hud.IsRaceButtonAt(swipeOrigin);
            }
            if (!trackingSwipe) return;
            if (session.State != RunState.Running) { trackingSwipe = false; return; }
            Vector2 delta = pointer.position.ReadValue() - swipeOrigin;
            // A wider tablet must not require a proportionally longer swipe.
            float gestureWidth = Mathf.Min(Screen.width, Screen.height * (9f / 16f));
            if (delta.magnitude < Mathf.Clamp(gestureWidth * .055f, 18f, 48f))
            { if (pointer.press.wasReleasedThisFrame) trackingSwipe = false; return; }
            trackingSwipe = false;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) RequestLane(delta.x > 0 ? 1 : -1);
            else if (delta.y > 0) jumpRequested = true;
        }
        private void OnGem(int airborneNote, bool turboJustReady)
        {
            audioFeedback.PlayGemPickup(airborneNote);
            if (turboJustReady) audioFeedback.PlayBoostReady();
            devices.Feedback(false);
        }
        private void OnPinkGemMissed(bool nearMiss, bool streakLost)
        {
            if (nearMiss)
            {
                hud.ShowPinkGemMiss();
                audioFeedback.PlayPinkGemMiss();
            }
            if (streakLost) audioFeedback.PlayPinkGemStreakLost();
        }
        private void OnImpact(bool duringBoost, int gemsLost, bool streakLost)
        {
            audioFeedback.PlayCollision(duringBoost);
            if (streakLost) audioFeedback.PlayPinkGemStreakLost();
            if (gemsLost > 0) audioFeedback.PlayGemLoss(duringBoost);
            devices.Feedback(true, duringBoost);
        }
        private void OnLetterCollected(string word, bool[] collected, bool complete)
        {
            hud.ShowWordProgress(word, collected, complete);
            audioFeedback.PlayLetterPickup();
            if (complete) audioFeedback.PlayWordCompleted();
        }
        private void RequestStart()
        {
            journeySession = JourneySessionStore.Load();
            if (journeySession == null)
            {
                hud.OpenJourneyAdmin();
                return;
            }
            startRequested = true;
        }
        private void RequestBrake() => brakeRequested = true;
        public void RequestOverloadEnd() => overloadRequested = true;
        private void ActivateTurbo()
        {
            if (!brakeRequested && !hud.SettingsOpen && session.TryActivateTurbo())
            {
                audioFeedback.PlayBoostButton();
                audioFeedback.PlayBoostStart();
            }
        }
        private void ChooseSpeed(bool accelerate)
        {
            if (session.OfferPending && !speedChoiceRequested.HasValue) speedChoiceRequested = accelerate;
        }
        private void ChooseRecovery()
        {
            if (session.OfferPending && !recoveryChoiceRequested) recoveryChoiceRequested = true;
        }
        private void StartDecisionCountdown()
        {
            decisionCountdownRemaining = settings.DecisionCountdownSeconds;
            decisionCountdownActive = true;
            hud.ShowDecisionCountdown(true, Mathf.CeilToInt(decisionCountdownRemaining), queuedRecoveryChoice);
        }
        private void RequestPause() { if (!hud.SettingsOpen) pauseRequested = true; }
        private void RequestMenu() => menuRequested = true;
        private void ContinueJourney()
        {
            journeySession = JourneySessionStore.Load();
            hud.CloseJourneyAdmin();
        }
        private void CloseJourney()
        {
            journeySession = JourneySessionStore.Load();
        }
        private void RequestLane(int direction) => laneRequested = direction;
        private void OpenSettings()
        {
            if (session.State == RunState.Running) session.Pause();
            hud.OpenSettings(); devices.CancelFeedback(); audioFeedback.Stop();
        }
        private void ToggleSound()
        {
            muted = !muted; audioFeedback.SetMuted(muted);
            PlayerPrefs.SetInt("Lotba.CityRush.Muted", muted ? 1 : 0); PlayerPrefs.Save();
        }
        private void InitializeQuality()
        {
            string[] available = QualitySettings.names;
            for (int i = 0; i < QualityNames.Length; i++)
                qualityLevels[i] = System.Array.IndexOf(available, QualityNames[i]);

            int preferred = System.Array.IndexOf(QualityNames, PlayerPrefs.GetString(QualityPreference, "Medium"));
            if (preferred < 0 || qualityLevels[preferred] < 0)
                preferred = qualityLevels[1] >= 0 ? 1 : System.Array.FindIndex(qualityLevels, level => level >= 0);
            ApplyQuality(preferred);
        }

        private void CycleQuality(int direction)
        {
            if (qualityChoice < 0) return;
            for (int step = 1; step < QualityNames.Length; step++)
            {
                int next = (qualityChoice + direction * step + QualityNames.Length) % QualityNames.Length;
                if (qualityLevels[next] < 0) continue;
                ApplyQuality(next);
                PlayerPrefs.SetString(QualityPreference, QualityNames[next]);
                PlayerPrefs.Save();
                return;
            }
        }

        private void ApplyQuality(int choice)
        {
            qualityChoice = choice;
            if (session != null)
            {
                world.ConfigurePickupEffects(UseBoostCamera);
                world.ConfigureGemGlow(IsHighQuality);
                if (IsHighQuality) world.InitializeTurboAura();
                else world.ClearTurboAura();
            }
            if (choice >= 0 && QualitySettings.GetQualityLevel() != qualityLevels[choice])
                QualitySettings.SetQualityLevel(qualityLevels[choice], true);
            hud.SetQuality(choice >= 0 ? QualityLabels[choice] : "NO DISPONIBLE",
                System.Array.FindAll(qualityLevels, level => level >= 0).Length > 1);
        }
        private void OnApplicationFocus(bool focus) { if (!focus) PauseForFocus(); }
        private void OnApplicationPause(bool paused) { if (paused) PauseForFocus(); }
        private void PauseForFocus()
        {
            if (session == null) return;
            session.Pause(); trackingSwipe = false; laneRequested = 0; jumpRequested = false;
            devices.CancelFeedback(); audioFeedback.Stop();
        }

        private void OnDestroy()
        {
            if (hud == null) return;
            hud.StartRequested -= RequestStart; hud.JourneyAdminRequested -= hud.OpenJourneyAdmin; hud.BrakeRequested -= RequestBrake;
            hud.PauseRequested -= RequestPause; hud.MenuRequested -= RequestMenu;
            hud.SoundRequested -= ToggleSound; hud.SettingsRequested -= OpenSettings;
            hud.MusicVolumeRequested -= audioFeedback.SetMusicLevel;
            hud.EffectsVolumeRequested -= audioFeedback.SetEffectsLevel;
            hud.VibrationRequested -= devices.CycleVibrationLevel;
            hud.QualityRequested -= CycleQuality;
            hud.SpeedChosen -= ChooseSpeed;
            hud.RecoveryChosen -= ChooseRecovery;
            hud.TurboRequested -= ActivateTurbo;
            hud.UiClicked -= audioFeedback.PlayUiClick;
            world.GemFeedback -= OnGem;
            world.PinkGemMissed -= OnPinkGemMissed;
            world.ImpactFeedback -= OnImpact;
            world.LetterCollected -= OnLetterCollected;
        }
    }
}
