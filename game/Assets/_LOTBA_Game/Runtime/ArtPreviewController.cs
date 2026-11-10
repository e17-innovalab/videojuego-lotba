#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lotba.Game
{
    public enum ArtPreviewMode { Route = 1, Module = 2 }

    /// <summary>Editor-only artist playback using the production world and obstacle patterns.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtPreviewController : MonoBehaviour
    {
        [SerializeField] private ArtPreviewMode mode = ArtPreviewMode.Route;
        [Tooltip("Provides the environment and obstacle catalog for Module mode.")]
        [SerializeField] private MapDefinition zone;
        [SerializeField] private RouteDefinition route;
        [SerializeField] private RoadModule module;
        [SerializeField, Min(1)] private float speed = 22;
        [SerializeField] private bool showRider = true, showCoins = true, showObstacles = true;
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
                if (mode == ArtPreviewMode.Route) return route == null ? "Assign a route." : route.ValidationError;
                if (zone == null) return "Assign a zone.";
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
            if (mode == ArtPreviewMode.Module)
            {
                temporaryMap = MapDefinition.CreatePreview(zone, module);
                selected = temporaryRoute = RouteDefinition.CreatePreview(temporaryMap);
            }
            roadStream.SetRoute(selected);
            world.Initialize(settings);
            ApplyPreviewSettings();
            world.ResetTrack();
            session = new RunSession(settings); session.Start();
        }
        private void Update()
        {
            if (session == null) return;
            // Apply Inspector visibility changes even while playback is paused.
            ApplyPreviewSettings();
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
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
        private void ApplyPreviewSettings()
        {
            world.ConfigureArtPreview(showCoins, showObstacles, showRider);
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
