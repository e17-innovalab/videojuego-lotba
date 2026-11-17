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
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (testRoofJumps)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) speed = settings.InitialSpeed;
                    if (keyboard.digit2Key.wasPressedThisFrame) speed = settings.CruiseSpeed;
                    if (keyboard.digit3Key.wasPressedThisFrame) speed = settings.CruiseSpeed + RunSession.BoostSpeed;
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
                session.TickPreview(step, Mathf.Max(1, speed));
                world.Advance(session, step, false); budget -= step;
            }
        }

        private void ResetPreview()
        {
            world.ResetTrack();
            session = new RunSession(settings);
            session.Start();
        }

        private void ApplyPreviewSettings()
        {
            world.ConfigureArtPreview(showGems, showObstacles, showRider, enableCollisions);
            world.SetEncounterIntensity(gemIntensity, obstacleIntensity);
            world.ConfigureDepthBend(showWorldBend);
        }
        private void OnDestroy()
        {
            if (temporaryRoute != null) Destroy(temporaryRoute);
            if (temporaryMap != null) Destroy(temporaryMap);
        }
    }
}
#endif
