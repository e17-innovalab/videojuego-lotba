#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lotba.Game
{
    public enum ArtPreviewMode { Route = 1, Module = 2, Zone = 3 }

    /// <summary>Editor-only artist playback using the production world and obstacle patterns.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtPreviewController : MonoBehaviour
    {
        [SerializeField] private ArtPreviewMode mode = ArtPreviewMode.Route;
        [Tooltip("Zone to preview, or environment and obstacle catalog for Module mode.")]
        [SerializeField] private MapDefinition zone;
        [SerializeField] private RouteDefinition route;
        [SerializeField] private EncounterProfile encounterProfile;
        [SerializeField] private RoadModule module;
        [SerializeField, Min(1)] private float speed = 22;
        [SerializeField] private bool showTurbo;
        [SerializeField] private bool showRider = true, showGems = true, showObstacles = true;
        [SerializeField] private bool enableCollisions;
        [SerializeField, Range(.25f, 4f)] private float gemIntensity = 1f, obstacleIntensity = 1f;
        [SerializeField] private bool testVehicleCourse;
        [SerializeField] private bool testRoofJumps;
        [SerializeField] private ObstacleDefinition testCar, testBus, testArticulatedBus;
        [SerializeField] private bool showWorldBend = true;
        [SerializeField] private GameSettings settings;
        [SerializeField] private RunnerWorld world;
        [SerializeField] private RoadStreamer roadStream;
        private RouteDefinition temporaryRoute;
        private MapDefinition temporaryMap;
        private RunSession session;
        private BoostSpeedLines turboLines;
        private GameObject turboCanvas;
        public ArtPreviewMode Mode => mode;
        public string ValidationError
        {
            get
            {
                if (settings == null || world == null || roadStream == null) return "Assign settings, world and road streamer.";
                if (testRoofJumps && testBus == null) return "Assign a bus for the roof jump course.";
                if (!testRoofJumps && testVehicleCourse && (testCar == null || testBus == null || testArticulatedBus == null))
                    return "Assign the vehicle course definitions.";
                if (mode == ArtPreviewMode.Route) return route == null ? "Assign a route." : route.ValidationError;
                if (zone == null) return "Assign a zone.";
                if (mode == ArtPreviewMode.Zone) return zone.ValidationError;
                return module == null ? "Assign a module." : module.ValidationError;
            }
        }
        private void Start()
        {
            if (ValidationError != null)
            {
                Debug.LogError(ValidationError, this); enabled = false; return;
            }
            RouteDefinition selected = route;
            if (mode == ArtPreviewMode.Zone)
                selected = temporaryRoute = RouteDefinition.CreatePreview(zone, encounterProfile);
            if (mode == ArtPreviewMode.Module)
            {
                temporaryMap = MapDefinition.CreatePreview(zone, module);
                selected = temporaryRoute = RouteDefinition.CreatePreview(temporaryMap, encounterProfile);
            }
            roadStream.SetRoute(selected);
            world.Initialize(settings);
            if (testRoofJumps)
            {
                world.ConfigureRoofJumpCourse(testBus);
                speed = settings.InitialSpeed;
            }
            else if (testVehicleCourse) world.ConfigureVehicleCourse(testCar, testBus, testArticulatedBus);
            ApplyPreviewSettings();
            ResetPreview();
        }
        private void Update()
        {
            if (session == null) return;
            // Apply Inspector visibility changes even while playback is paused.
            ApplyPreviewSettings();
            if (turboLines != null)
            {
                turboLines.SetScreenOrigin(world.RiderScreenPosition);
                turboLines.Render(settings, session, showTurbo);
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (testRoofJumps)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) speed = settings.InitialSpeed;
                    if (keyboard.digit2Key.wasPressedThisFrame) speed = settings.CruiseSpeed;
                    if (keyboard.digit3Key.wasPressedThisFrame) speed = settings.CruiseSpeed + session.ManualTurbo.SpeedBonus;
                    if (keyboard.rKey.wasPressedThisFrame)
                    {
                        ResetPreview();
                        return;
                    }
                }
                if (keyboard.pKey.wasPressedThisFrame)
                {
                    if (session.State == RunState.Paused) session.Resume(); else session.Pause();
                }
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) session.Move(-1);
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) session.Move(1);
                if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) world.TryJump(session.Elapsed);
            }
            if (session.State != RunState.Running) return;
            float budget = Mathf.Min(Time.unscaledDeltaTime, .15f);
            while (budget > 0)
            {
                float step = Mathf.Min(.02f, budget);
                session.TickPreview(step, Mathf.Max(1, speed) + (showTurbo ? session.ManualTurbo.SpeedBonus : 0f));
                world.Advance(session, step, false, showTurbo, showTurbo); budget -= step;
            }
        }

        private void ResetPreview()
        {
            world.ResetTrack();
            session = new RunSession(settings);
            session.Start();
            session.SetPreviewTurbo(showTurbo);
        }

        private void ApplyPreviewSettings()
        {
            world.ConfigureArtPreview(showGems, showObstacles, showRider, enableCollisions);
            world.SetEncounterIntensity(gemIntensity, obstacleIntensity);
            world.ConfigureDepthBend(showWorldBend);
            session?.SetPreviewTurbo(showTurbo);
            if (showTurbo && turboLines == null) CreateTurboOverlay();
        }

        private void CreateTurboOverlay()
        {
            // Reuse the production UI effect without loading GameHud or starting a tracked game.
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_LOTBA_Game/Prefabs/UI/Líneas de velocidad.prefab");
            if (prefab == null) return;
            turboCanvas = new GameObject("Art Preview Turbo", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            turboCanvas.transform.SetParent(transform, false);
            turboCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = turboCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540f, 960f);
            scaler.matchWidthOrHeight = .5f;
            turboLines = Instantiate(prefab, turboCanvas.transform, false).GetComponent<BoostSpeedLines>();
            world.InitializeTurboAura();
        }
        private void OnDestroy()
        {
            if (turboCanvas != null) Destroy(turboCanvas);
            if (temporaryRoute != null) Destroy(temporaryRoute);
            if (temporaryMap != null) Destroy(temporaryMap);
        }
    }
}
#endif
