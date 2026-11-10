using UnityEngine;
using UnityEngine.InputSystem;

namespace Lotba.Game
{
    /// <summary>Input ordering, capability-gated devices and scene composition. Braking preempts simulation.</summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        [SerializeField] private RunnerWorld world;
        [SerializeField] private GameHud hud;
        [SerializeField] private AudioManager audioFeedback;
        [SerializeField] private DeviceFeedbackAndInput devices;
        private RunSession session;
        private bool startRequested, brakeRequested, jumpRequested, pauseRequested, menuRequested;
        private int laneRequested;
        private bool? speedChoiceRequested;
        private bool muted, trackingSwipe;
        private const string QualityPreference = "Lotba.CityRush.Quality";
        private static readonly string[] QualityNames = { "Low", "Medium", "High" };
        private static readonly string[] QualityLabels = { "BAJA", "MEDIA", "ALTA" };
        private readonly int[] qualityLevels = new int[QualityNames.Length];
        private int qualityChoice = -1;
        private bool IsHighQuality => qualityChoice >= 0 && QualityNames[qualityChoice] == "High";
        private Vector2 swipeOrigin;
        private void Awake()
        {
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 60;
            muted = PlayerPrefs.GetInt("Lotba.CityRush.Muted", 0) != 0;
            InitializeQuality();
            audioFeedback.SetMuted(muted); devices.Initialize();
            world.Initialize(settings); world.ResetTrack(); session = new RunSession(settings, world.Route);
            world.InitializeTurboAura();
            world.ConfigureDepthBend(IsHighQuality);
            hud.StartRequested += RequestStart; hud.BrakeRequested += RequestBrake;
            hud.PauseRequested += RequestPause; hud.MenuRequested += RequestMenu;
            hud.SoundRequested += ToggleSound; hud.SettingsRequested += OpenSettings;
            hud.VibrationRequested += devices.CycleVibrationLevel;
            hud.QualityRequested += CycleQuality;
            hud.SpeedChosen += ChooseSpeed;
            world.GemFeedback += OnGem;
            world.ImpactFeedback += OnImpact;
            world.StreakLevelUp += audioFeedback.PlayStreakLevelUp;
            world.LetterCollected += OnLetterCollected;
            hud.Render(session, settings, muted, devices);
        }
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
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
            world.ConfigureDepthBend(IsHighQuality);
            if (menuRequested && (session.State == RunState.Finished || session.State == RunState.Paused))
            {
                ResetRun(false);
                speedChoiceRequested = null;
                startRequested = brakeRequested = jumpRequested = pauseRequested = menuRequested = false; laneRequested = 0;
                hud.Render(session, settings, muted, devices);
                return;
            }
            if (startRequested && (session.State == RunState.Ready || session.State == RunState.Finished))
            {
                ResetRun(true);
            }
            if (brakeRequested) { session.Brake(); audioFeedback.Stop(); devices.CancelFeedback(); }
            else
            {
                if (pauseRequested)
                {
                    if (session.State == RunState.Paused) { session.Resume(); }
                    else { session.Pause(); audioFeedback.Stop(); devices.CancelFeedback(); }
                }
                if (speedChoiceRequested.HasValue) session.ChooseSpeed(speedChoiceRequested.Value);
                if (session.State == RunState.Running && !session.OfferPending)
                {
                    if (laneRequested != 0 && session.Move(laneRequested)) audioFeedback.PlayLaneSlide();
                    if (jumpRequested && world.TryJump(session.Elapsed))
                    {
                        session.RecordJump();
                        audioFeedback.PlayJump();
                    }
                    float budget = Mathf.Min(Time.unscaledDeltaTime, .15f);
                    while (budget > 0 && session.State == RunState.Running && !session.OfferPending)
                    {
                        float step = Mathf.Min(.02f, budget); session.Tick(step);
                        if (session.State == RunState.Running) world.Advance(session, step, false);
                        budget -= step;
                    }
                }
            }
            if (session.State == RunState.Finished)
            {
                audioFeedback.Stop();
                world.ClearImpactBlink();
                world.ClearTurboAura();
            }
            speedChoiceRequested = null;
            startRequested = brakeRequested = jumpRequested = pauseRequested = menuRequested = false; laneRequested = 0;
            hud.Render(session, settings, muted, devices);
        }
        private void ResetRun(bool start)
        {
            session = new RunSession(settings, world.Route); world.ResetTrack(); hud.ResetView(); audioFeedback.Stop(); devices.CancelFeedback(); trackingSwipe = false;
            if (start) { session.Start(); }
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
            if (delta.magnitude < Mathf.Max(18, Screen.width * .055f))
            { if (pointer.press.wasReleasedThisFrame) trackingSwipe = false; return; }
            trackingSwipe = false;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) RequestLane(delta.x > 0 ? 1 : -1);
            else if (delta.y > 0) jumpRequested = true;
        }
        private void OnGem(int airborneNote)
        {
            audioFeedback.PlayGemPickup(airborneNote);
            devices.Feedback(false);
        }
        private void OnImpact(long loss, bool lostStreak)
        {
            audioFeedback.PlayCollision();
            devices.Feedback(true);
            hud.ShowImpact(loss, lostStreak);
        }
        private void OnLetterCollected(string word, bool[] collected, bool complete)
        {
            hud.ShowWordProgress(word, collected, complete);
            audioFeedback.PlayLetterPickup();
            if (complete) audioFeedback.PlayWordCompleted();
        }
        private void RequestStart() => startRequested = true;
        private void RequestBrake() => brakeRequested = true;
        private void ChooseSpeed(bool accelerate)
        {
            if (session.OfferPending && !speedChoiceRequested.HasValue) speedChoiceRequested = accelerate;
        }
        private void RequestPause() { if (!hud.SettingsOpen) pauseRequested = true; }
        private void RequestMenu() => menuRequested = true;
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

        private void CycleQuality()
        {
            if (qualityChoice < 0) return;
            for (int step = 1; step < QualityNames.Length; step++)
            {
                int next = (qualityChoice + step) % QualityNames.Length;
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
            hud.StartRequested -= RequestStart; hud.BrakeRequested -= RequestBrake;
            hud.PauseRequested -= RequestPause; hud.MenuRequested -= RequestMenu;
            hud.SoundRequested -= ToggleSound; hud.SettingsRequested -= OpenSettings;
            hud.VibrationRequested -= devices.CycleVibrationLevel;
            hud.QualityRequested -= CycleQuality;
            hud.SpeedChosen -= ChooseSpeed;
            world.GemFeedback -= OnGem;
            world.ImpactFeedback -= OnImpact;
            world.StreakLevelUp -= audioFeedback.PlayStreakLevelUp;
            world.LetterCollected -= OnLetterCollected;
        }
    }
}
