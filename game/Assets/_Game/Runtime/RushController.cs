using UnityEngine;
using UnityEngine.InputSystem;

namespace Lotba.CityRush
{
    /// <summary>Input ordering, capability-gated devices and scene composition. Braking preempts simulation.</summary>
    public sealed class RushController : MonoBehaviour
    {
        [SerializeField] private RushSettings settings;
        [SerializeField] private RushWorld world;
        [SerializeField] private RushHud hud;
        [SerializeField] private RushAudio audioFeedback;
        [SerializeField] private RushDevices devices;
        private RushSession session;
        private bool startRequested, brakeRequested, jumpRequested, pauseRequested, menuRequested, loading;
        private int laneRequested;
        private bool muted, trackingSwipe;
        private float lastManualLane;
        private Vector2 swipeOrigin;
        private void Awake()
        {
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 60;
            muted = PlayerPrefs.GetInt("Lotba.CityRush.Muted", 0) != 0;
            audioFeedback.SetMuted(muted); devices.Initialize();
            world.Initialize(settings); world.ResetTrack(); session = new RushSession(settings);
            hud.StartRequested += RequestStart; hud.BrakeRequested += RequestBrake;
            hud.PauseRequested += RequestPause; hud.MenuRequested += RequestMenu;
            hud.SoundRequested += ToggleSound; hud.SettingsRequested += OpenSettings;
            hud.VibrationRequested += devices.ToggleVibration; hud.TiltRequested += devices.ToggleTilt;
            hud.LocationRequested += SelectLocation; world.EncounterFeedback += OnEncounter;
            hud.Render(session, settings, muted, devices);
        }
        private void Start() { if (RushSceneFlow.ConsumeStart()) ResetRun(true); }
        private void Update()
        {
            if (loading) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) RequestBrake();
            if (hud.SettingsOpen) return;
            ReadSwipe();
            if (session.State == RushState.Running && laneRequested == 0 && Time.unscaledTime - lastManualLane > .35f)
                laneRequested = devices.ReadLaneDirection(session.Lane);
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) RequestPause();
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) RequestLane(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) RequestLane(1);
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) jumpRequested = true;
        }
        private void LateUpdate()
        {
            if (loading) return;
            if (menuRequested && (session.State == RushState.Finished || session.State == RushState.Paused))
            {
                ResetRun(false);
                startRequested = brakeRequested = jumpRequested = pauseRequested = menuRequested = false; laneRequested = 0;
                hud.Render(session, settings, muted, devices);
                return;
            }
            if (startRequested && (session.State == RushState.Ready || session.State == RushState.Finished))
            {
                if (!RushSceneFlow.IsSelectedScene && RushSceneFlow.LoadSelected())
                { loading = true; hud.SetLoading(); return; }
                ResetRun(true);
            }
            if (brakeRequested) { session.Brake(); audioFeedback.Stop(); devices.CancelFeedback(); }
            else
            {
                if (pauseRequested)
                {
                    if (session.State == RushState.Paused) { session.Resume(); devices.Calibrate(); }
                    else { session.Pause(); devices.SetSampling(false); audioFeedback.Stop(); devices.CancelFeedback(); }
                }
                if (session.State == RushState.Running)
                {
                    if (laneRequested != 0) session.Move(laneRequested);
                    if (jumpRequested) world.TryJump(session.Elapsed);
                    float budget = Mathf.Min(Time.unscaledDeltaTime, .15f);
                    while (budget > 0 && session.State == RushState.Running)
                    {
                        float step = Mathf.Min(.02f, budget); session.Tick(step);
                        if (session.State == RushState.Running) world.Advance(session, step, false);
                        budget -= step;
                    }
                }
            }
            if (session.State == RushState.Finished) { audioFeedback.Stop(); devices.SetSampling(false); }
            startRequested = brakeRequested = jumpRequested = pauseRequested = menuRequested = false; laneRequested = 0;
            hud.Render(session, settings, muted, devices);
        }
        private void ResetRun(bool start)
        {
            session = new RushSession(settings); world.ResetTrack(); hud.ResetView(); audioFeedback.Stop(); devices.CancelFeedback(); trackingSwipe = false;
            devices.SetSampling(start);
            if (start) { session.Start(); devices.Calibrate(); }
        }
        private void ReadSwipe()
        {
            Pointer pointer = Pointer.current; if (pointer == null) return;
            if (pointer.press.wasPressedThisFrame)
            {
                swipeOrigin = pointer.position.ReadValue();
                trackingSwipe = session.State == RushState.Running && swipeOrigin.y > Screen.height * .2f && swipeOrigin.y < Screen.height * .9f;
            }
            if (!trackingSwipe) return;
            if (session.State != RushState.Running) { trackingSwipe = false; return; }
            Vector2 delta = pointer.position.ReadValue() - swipeOrigin;
            if (delta.magnitude < Mathf.Max(18, Screen.width * .055f))
            { if (pointer.press.wasReleasedThisFrame) trackingSwipe = false; return; }
            trackingSwipe = false;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) RequestLane(delta.x > 0 ? 1 : -1);
            else if (delta.y > 0) jumpRequested = true;
        }
        private void OnEncounter(long amount, bool impact) { hud.ShowFeedback(amount, impact); audioFeedback.Play(impact); devices.Feedback(impact); }
        private void RequestStart() => startRequested = true;
        private void RequestBrake() => brakeRequested = true;
        private void RequestPause() { if (!hud.SettingsOpen) pauseRequested = true; }
        private void RequestMenu() => menuRequested = true;
        private void RequestLane(int direction) { laneRequested = direction; lastManualLane = Time.unscaledTime; }
        private void SelectLocation(RushLocation location) { if (session.State == RushState.Ready) RushSceneFlow.Selected = location; }
        private void OpenSettings()
        {
            if (session.State == RushState.Running) session.Pause();
            hud.OpenSettings(); devices.SetSampling(false); devices.CancelFeedback(); audioFeedback.Stop();
        }
        private void ToggleSound()
        {
            muted = !muted; audioFeedback.SetMuted(muted);
            PlayerPrefs.SetInt("Lotba.CityRush.Muted", muted ? 1 : 0); PlayerPrefs.Save();
        }
        private void OnApplicationFocus(bool focus) { if (!focus) PauseForFocus(); }
        private void OnApplicationPause(bool paused) { if (paused) PauseForFocus(); }
        private void PauseForFocus()
        {
            if (session == null) return;
            session.Pause(); trackingSwipe = false; laneRequested = 0; jumpRequested = false;
            devices.SetSampling(false); devices.CancelFeedback(); audioFeedback.Stop();
        }
        private void OnDestroy()
        {
            if (hud == null) return;
            hud.StartRequested -= RequestStart; hud.BrakeRequested -= RequestBrake;
            hud.PauseRequested -= RequestPause; hud.MenuRequested -= RequestMenu;
            hud.SoundRequested -= ToggleSound; hud.SettingsRequested -= OpenSettings;
            hud.VibrationRequested -= devices.ToggleVibration; hud.TiltRequested -= devices.ToggleTilt;
            hud.LocationRequested -= SelectLocation; world.EncounterFeedback -= OnEncounter;
        }
    }
}
