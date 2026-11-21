using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Pooled runner presentation and swept encounters. Scenery never decides run state.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class RunnerWorld : MonoBehaviour
    {
        public const float LaneSpacing = RoadStandard.LaneSpacing;
        [SerializeField] private Transform rider;
        [SerializeField] private Transform[] wheels;
        [SerializeField] private Transform[] pedals;
        [SerializeField] private RoadStreamer roadStream;
        [SerializeField] private Camera worldCamera;
        public Vector2 RiderScreenPosition => worldCamera.WorldToScreenPoint(rider.position + Vector3.up * 1.2f);
        private float riderDistance;
        internal const float BrakeSlideSeconds = .50f;
        private float brakeSlideStarted = float.NegativeInfinity;
        private Transform riderVisual, steeringPivot, frontWheel, rearWheel;
        private RiderPart hips, torso, head;
        private RiderLimb[] legs, arms;
        private Quaternion brakeStartRotation, brakeStartSteering;
        private Vector3 brakeStartScale;
        private float brakeStartHeight, brakeTravel;
        private Vector3 brakeCameraPosition;
        private float brakeCameraFov, brakeInitialSpeed, nextBrakeParticle;
        private bool brakeCameraEffects, brakeGroundEffects;
        private ParticleSystem brakeDust;
        private Material brakeDustMaterial;
        private Mesh brakeDustMesh;
        private LineRenderer brakeMark;
        private int brakeMarkPoints;
        private float brakeMarkStarted = float.NegativeInfinity;
        private static readonly Color BrakeMarkColor = new Color32(40, 42, 45, 175);
        private static readonly ProfilerMarker PrepareOpeningMarker = new ProfilerMarker("Lotba.Track.PrepareOpening");
        private static readonly ProfilerMarker PopulateAheadMarker = new ProfilerMarker("Lotba.Track.PopulateAhead");

        private readonly struct RiderPart
        {
            public readonly Transform View;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public RiderPart(Transform view)
            {
                View = view;
                Position = view == null ? Vector3.zero : view.localPosition;
                Rotation = view == null ? Quaternion.identity : view.localRotation;
            }

            public void Apply(Vector3 offset, Quaternion rotation)
            {
                if (View != null) View.SetLocalPositionAndRotation(Position + offset, rotation * Rotation);
            }

            public void Reset() => Apply(Vector3.zero, Quaternion.identity);
        }

        private readonly struct RiderLimb
        {
            private readonly RiderPart upper, lower, end;
            private readonly Vector3 upperScale, lowerScale;
            public readonly Vector3 JointRoot, Endpoint;

            public RiderLimb(Transform upper, Transform lower, Transform end, Vector3 root)
            {
                this.upper = new RiderPart(upper);
                this.lower = new RiderPart(lower);
                this.end = new RiderPart(end);
                upperScale = upper != null ? upper.localScale : Vector3.one;
                lowerScale = lower != null ? lower.localScale : Vector3.one;
                JointRoot = root;
                Endpoint = this.end.Position;
            }

            public void Pose(Vector3 root, Vector3 endpoint, Vector3 bend, Quaternion endRotation)
            {
                if (upper.View == null || lower.View == null || end.View == null) return;
                Vector3 reach = endpoint - root;
                float distance = Mathf.Max(.001f, reach.magnitude);
                Vector3 direction = reach / distance;
                float extension = Mathf.Max(1f, distance / (2f * (upperScale.y + lowerScale.y) - .005f));
                float firstLength = 2f * upperScale.y * extension;
                float secondLength = 2f * lowerScale.y * extension;
                float along = Mathf.Clamp((distance * distance + firstLength * firstLength -
                    secondLength * secondLength) / (2f * distance), 0f, distance);
                float outward = Mathf.Sqrt(Mathf.Max(0f, firstLength * firstLength - along * along));
                Vector3 joint = root + direction * along + Vector3.ProjectOnPlane(bend, direction).normalized * outward;
                PoseSegment(upper.View, upperScale, root, joint);
                PoseSegment(lower.View, lowerScale, joint, endpoint);
                end.Apply(endpoint - Endpoint, endRotation);
            }

            private static void PoseSegment(Transform mesh, Vector3 restScale, Vector3 start, Vector3 end)
            {
                Vector3 segment = end - start;
                mesh.SetLocalPositionAndRotation((start + end) * .5f, Quaternion.FromToRotation(Vector3.up, segment));
                restScale.y = segment.magnitude * .25f + .015f;
                mesh.localScale = restScale;
            }

            public void Reset()
            {
                upper.Reset();
                lower.Reset();
                end.Reset();
                if (upper.View != null) upper.View.localScale = upperScale;
                if (lower.View != null) lower.View.localScale = lowerScale;
            }
        }
        private float depthBendStart, depthBendStrength;
        private bool refreshDepthBend;
        private bool depthBendRequested;
        private float letterLaneX;
        [SerializeField] private GameObject gemPrefab;
        private SpriteRenderer gemFlash;
        private PickupFlashSettings activeFlash;
        private Material defaultFlashMaterial;
        private MaterialPropertyBlock gemFlashProperties;
        private Quaternion gemFlashRoll;
        private static readonly int FlashTopTint = Shader.PropertyToID("_TopTint");
        private static readonly int FlashBottomTint = Shader.PropertyToID("_BottomTint");
        private static readonly int FlashRadialGradient = Shader.PropertyToID("_RadialGradient");
        private float gemFlashStarted;
        [SerializeField] private Transform riderShadow;
        private Vector3 riderShadowScale;
        [Header("Word collection")]
        [SerializeField] private bool wordCollectionEnabled;
        [SerializeField] private string[] collectibleWords = { "HABLAR ES GANAR" };
        [SerializeField] private TMPro.TMP_FontAsset letterFont;
        [SerializeField] private Material letterOutlineMaterial;
        [SerializeField, Min(20f)] private float letterInterval = 65f;
        [SerializeField, Min(0f)] private float letterClearance = 12f;
        [SerializeField, Min(0f)] private float letterHoverHeight = .18f;
        [SerializeField, Min(.1f)] private float letterHoverCycle = 2.8f;
        [SerializeField, Range(0f, 15f)] private float letterTilt = 6f;
        private const float LetterPickupHeight = 1.1f;
        private float letterHoverTime;
        private readonly Queue<int> pendingLetters = new Queue<int>();
        private readonly System.Random letterRandom = new System.Random();
        private TMPro.TextMeshPro letterPickup;
        private SpriteRenderer letterBeam;
        private MaterialPropertyBlock letterBeamProperties;
        private readonly SpriteRenderer[] letterMotes = new SpriteRenderer[10];
        private float letterBeamTime;
        private TMPro.TMP_FontAsset LetterFont => letterFont != null ? letterFont : TMPro.TMP_Settings.defaultFontAsset;
        private string activeWord;
        private bool[] collectedLetters;
        private int activeLetter = -1;
        private int nextWord;
        private float nextLetterDistance;
        private float scheduledLetterStart, scheduledLetterEnd;
        private int scheduledLettersPlaced;
        private bool[] presentedLetters;
        public event Action<string, bool[], bool> LetterCollected;
        private readonly JumpArc jump = new JumpArc();
        private readonly List<TrackItem> items = new List<TrackItem>(256);
        private readonly List<Crossing> crossings = new List<Crossing>(16);
        private Quaternion[] wheelOrigins;
        private Vector3 cameraOrigin;
        private Quaternion cameraRotation;
        private Vector3 cameraVelocity;
        private float boostCameraBlend;
        private const float GemSpinDegreesPerSecond = 120f;
        private bool pickupEffectsEnabled = true;
        private bool gemGlowEnabled;
        private Material[] regularGemMaterials, pinkGemMaterials;
        private const int NormalImpactScatterLimit = 5;
        private const int TurboImpactScatterLimit = 12;
        private readonly Transform[] scatteredGems = new Transform[TurboImpactScatterLimit];
        private readonly Vector3[] scatterTrajectories = new Vector3[TurboImpactScatterLimit];
        private readonly Vector3[] scatterSpins = new Vector3[TurboImpactScatterLimit];
        private readonly System.Random scatterRandom = new System.Random();
        private Vector3 gemScatterOrigin;
        private float gemScatterTime;
        private int scatteredGemCount;
        private TurboAuraController turboAura;
        private float referenceFieldOfView;
        private Renderer[] riderRenderers;
        private Material riderImpactMaterial;
        private Material[][] riderMaterials;
        private Material[][] riderFadeMaterials;
        private readonly Dictionary<Material, Material> riderFadeCopies = new Dictionary<Material, Material>();
        private Material[][] riderWhiteMaterials;
        private bool riderImpactWhite;
        private bool riderImpactTransparent;
        private ObstacleSet obstacleOverride;
        private readonly HashSet<ObstacleDefinition> registeredObstacles = new HashSet<ObstacleDefinition>();
        private GameSettings settings;
        private System.Random random;
        private const float FirstPatternDistance = 58f;
        private float nextPatternDistance;
        private float nextProfileObstacle;
        private float gemIntensity = 1f, obstacleIntensity = 1f;
        private int encounterSection = -1;
        private float lastPatternEnd;
        private float nextVehicleSafeDistance;
        private int gemRowsSincePink, pinkDodgeSequence;
        private float lastPinkGemDistance;
        private int nextDecisionGemSection;
        private float decisionGemSectionStart;
        private float laneTime;
        private float laneOrigin;
        private int visualLane = 1;
        private int safeLane = 1;
        private int previousVehicleLane = -1;
        private int wave;
        private const float LanePoseSeconds = .20f;
        private const float LandingPoseSeconds = .15f;
        private const float TakeoffPreloadSeconds = .055f;
        private float pedalPhase, wheelSpin, lanePoseAge, lanePoseDirection;
        private float sprintPose, sprintFrom, sprintAge;
        private float airPose, landingAge, takeoffAge;
        private float riderSway, riderBank, riderBrace, riderTakeoff, riderLanding;
        private bool sprinting, wasAirborne;
        private Vector2 tyreRadii;
        private const float CourseRampStart = 72f;
        private const float CourseRampLength = 12f;
        private const float CourseBusStart = CourseRampStart + CourseRampLength;
        private const float CourseRoofGap = 14f;
        private const float CourseFallGravity = 34f;
        private const float ObstacleLateralPadding = .27f;
        private const float RoofLandingEdgeTolerance = .08f;
        private ObstacleDefinition courseCar, courseBus, courseArticulatedBus;
        private float courseJumpBase, courseFallSpeed;
#if UNITY_EDITOR
        private float courseEnd;
#endif
        private const float ElevatedInterval = 240f;
        private float nextElevatedDistance;
        private int vehiclePatternIndex;
        private readonly List<ElevatedEncounter> elevatedEncounters = new List<ElevatedEncounter>();
        private readonly List<VehicleSurface> vehicleRoofs = new List<VehicleSurface>();
        private sealed class ElevatedEncounter
        {
            public Transform Ramp;
            public float Start, End;
            public readonly List<VehicleSurface> Surfaces = new List<VehicleSurface>(6);
        }
        private readonly struct VehicleSurface
        {
            public readonly TrackItem Vehicle;
            public readonly float Start, End, X, HalfWidth, Low, High;
            public VehicleSurface(float start, float end, float x, float halfWidth, float low, float high, TrackItem vehicle = null)
            { Start = start; End = end; X = x; HalfWidth = halfWidth; Low = low; High = high; Vehicle = vehicle; }
        }

#if UNITY_EDITOR
        private bool previewGems = true, previewObstacles = true, previewInvulnerable;
        private Vector3? lastDebugContact;
        private ObstacleDefinition previewRoofJumpBus;
        private static readonly float[] PreviewRoofGaps = { 12f, 17f, 22f };

        public void ConfigureRoofJumpCourse(ObstacleDefinition bus)
        {
            previewRoofJumpBus = bus;
            WarmObstacle(bus);
        }

        private void SpawnRoofJumpCourse()
        {
            const int launchLane = 1;
            const float connector = 1.4f;
            ObstacleDefinition bus = previewRoofJumpBus;
            float rampStart = 38f;
            for (int station = 0; station < PreviewRoofGaps.Length; station++)
            {
                float gap = PreviewRoofGaps[station];
                int landingLane = station == 0 ? launchLane : station == 1 ? 2 : 0;
                float landingX = (landingLane - launchLane) * LaneSpacing;
                float roofStart = rampStart + CourseRampLength;
                float takeoff = roofStart + bus.Length;
                float landing = takeoff + gap;
                float deckEnd = landing + 3f * bus.Length + 2f * connector;
                Transform scenery = new GameObject($"Salto de {gap:0} m").transform;
                scenery.SetParent(transform, false);
                scenery.localPosition = Vector3.forward * rampStart;
                scenery.gameObject.SetActive(previewObstacles);
                var encounter = new ElevatedEncounter { Ramp = scenery, Start = rampStart, End = deckEnd };
                elevatedEncounters.Add(encounter);

                CreateCourseSurface("Rampa de acceso", 0f, 0f, CourseRampLength, 0f, bus.Height, 2f, bus, scenery);
                encounter.Surfaces.Add(new VehicleSurface(rampStart, roofStart, 0f, 1f, 0f, bus.Height));
                PlaceCourseBus(bus, launchLane, roofStart);
                CreateCourseSurface("Marca de despegue", 0f, takeoff - rampStart - 1.8f,
                    takeoff - rampStart - 1.5f, bus.Height, bus.Height, bus.Width, bus, scenery, roofBridge: true);

                for (int i = 0; i < 3; i++)
                {
                    float busStart = landing + i * (bus.Length + connector);
                    PlaceCourseBus(bus, landingLane, busStart);
                    if (i == 2) continue;
                    float joint = busStart + bus.Length - rampStart;
                    CreateCourseSurface("Unión de techos", landingX, joint, joint + connector,
                        bus.Height, bus.Height, bus.Width, bus, scenery, roofBridge: true);
                    encounter.Surfaces.Add(new VehicleSurface(joint + rampStart, joint + rampStart + connector,
                        landingX, bus.Width * .5f, bus.Height, bus.Height));
                }
                for (int i = 0; i < 3; i++)
                    Place(ItemKind.Gem, landingLane, landing + 2f + i * 4f, 1.1f + bus.Height);
                for (int i = 1; i <= 4; i++)
                {
                    float rampProgress = i / 5f;
                    Place(ItemKind.Gem, launchLane, rampStart + CourseRampLength * rampProgress, 1.1f + bus.Height * rampProgress);
                }
                rampStart = deckEnd + 35f;
            }
        }

        public void ConfigureVehicleCourse(ObstacleDefinition car, ObstacleDefinition bus, ObstacleDefinition articulatedBus)
        {
            courseCar = car;
            courseBus = bus;
            courseArticulatedBus = articulatedBus;
            WarmObstacle(car);
            WarmObstacle(bus);
            WarmObstacle(articulatedBus);
        }

        private void SpawnVehicleCourse()
        {
            if (courseCar == null || courseBus == null || courseArticulatedBus == null) return;
            Transform scenery = new GameObject("Vehicle test course").transform;
            scenery.SetParent(transform, false);
            scenery.gameObject.SetActive(previewObstacles);
            float roof = courseBus.Height;
            float firstEnd = CourseBusStart + courseBus.Length;
            float adjacentStart = CourseBusStart + 2f;
            float courseAdjacentEnd = adjacentStart + courseBus.Length;
            float courseLandingStart = courseAdjacentEnd + CourseRoofGap;
            const float jointLength = 1.4f;
            float jointStart = courseLandingStart + courseArticulatedBus.Length;
            courseEnd = jointStart + jointLength + courseArticulatedBus.Length;
            var encounter = new ElevatedEncounter { Ramp = scenery, Start = 0f, End = courseEnd };
            elevatedEncounters.Add(encounter);
            Place(ItemKind.Obstacle, 1, 44f + courseCar.Length * .5f, 0f, courseCar);
            PlaceCourseBus(courseBus, 1, CourseBusStart);
            PlaceCourseBus(courseBus, 2, adjacentStart);
            PlaceCourseBus(courseArticulatedBus, 2, courseLandingStart);
            PlaceCourseBus(courseArticulatedBus, 2, jointStart + jointLength);
            CreateCourseSurface("Entry ramp", 0f, CourseRampStart, CourseBusStart, 0f, roof, 2f, courseBus, scenery);
            CreateCourseSurface("Roof crossing", LaneSpacing * .5f, adjacentStart, firstEnd,
                roof, roof, LaneSpacing, courseBus, scenery, roofBridge: true);
            CreateCourseSurface("Articulated joint", LaneSpacing, jointStart, jointStart + jointLength,
                roof, roof, courseArticulatedBus.Width, courseBus, scenery);
            encounter.Surfaces.Add(new VehicleSurface(CourseRampStart, CourseBusStart, 0f, 1f, 0f, roof));
            encounter.Surfaces.Add(new VehicleSurface(adjacentStart, firstEnd, LaneSpacing * .5f, LaneSpacing * .5f, roof, roof));
            encounter.Surfaces.Add(new VehicleSurface(jointStart, jointStart + jointLength, LaneSpacing,
                courseArticulatedBus.Width * .5f, roof, roof));
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 1) / 5f;
                Place(ItemKind.Gem, 1, CourseRampStart + CourseRampLength * t, 1.1f + roof * t);
                Place(ItemKind.Gem, 2, courseLandingStart + 1f + i * 4f, 1.1f + roof);
            }
            Place(ItemKind.Gem, 2, courseAdjacentEnd - 1f, 1.1f + roof);
            Place(ItemKind.Gem, 2, courseAdjacentEnd + CourseRoofGap * .5f, 2.35f + roof);
        }

        private void PlaceCourseBus(ObstacleDefinition bus, int lane, float start) =>
            Place(ItemKind.Obstacle, lane, start + bus.Length * .5f, 0f, bus);
#endif

        private Transform CreateCourseSurface(string name, float x, float start, float end, float low, float high, float width,
            ObstacleDefinition bus, Transform scenery, bool roofBridge = false)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visual.GetComponent<Collider>());
            visual.name = name;
            visual.transform.SetParent(scenery, false);
            float rise = high - low;
            float length = end - start;
            visual.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(rise, length) * Mathf.Rad2Deg, 0f, 0f);
            visual.transform.localScale = new Vector3(width, .16f, Mathf.Sqrt(length * length + rise * rise));
            visual.transform.localPosition = new Vector3(x, (low + high) * .5f - .08f +
                (roofBridge ? .025f : 0f), (start + end) * .5f);
            Renderer busRenderer = bus.Prefab.GetComponentInChildren<Renderer>(true);
            Renderer surfaceRenderer = visual.GetComponent<Renderer>();
            if (busRenderer != null) surfaceRenderer.sharedMaterial = busRenderer.sharedMaterial;
            if (roofBridge)
            {
                var properties = new MaterialPropertyBlock();
                Material material = surfaceRenderer.sharedMaterial;
                Color baseColor = material != null && material.HasProperty("_Color") ? material.color : Color.gray;
                Color bridgeColor = Color.Lerp(baseColor, new Color32(235, 209, 155, 255), .35f);
                properties.SetColor("_Color", bridgeColor);
                properties.SetColor("_BaseColor", bridgeColor);
                surfaceRenderer.SetPropertyBlock(properties);
            }
            if (rise > 0f)
            {
                for (int row = 1; row <= 3; row++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        mark.name = "Flecha de subida";
                        Destroy(mark.GetComponent<Collider>());
                        mark.transform.SetParent(visual.transform, false);
                        mark.transform.localPosition = new Vector3(side * .1f, .56f, row * .22f - .44f);
                        mark.transform.localRotation = Quaternion.Euler(0f, -side * 58f, 0f);
                        mark.transform.localScale = new Vector3(.045f, .08f, .22f);
                        var tint = new MaterialPropertyBlock();
                        tint.SetColor("_Color", new Color32(255, 230, 154, 255));
                        tint.SetColor("_BaseColor", new Color32(255, 230, 154, 255));
                        mark.GetComponent<Renderer>().SetPropertyBlock(tint);
                    }
                }
            }
            return visual.transform;
        }

#if UNITY_EDITOR
        private bool SkipVehicleCourse(ref float distance)
        {
            if (courseCar == null || distance > courseEnd + 12f) return false;
            lastPatternEnd = courseEnd;
            distance = courseEnd + 12.1f;
            return true;
        }
#endif

        private bool TrySpawnElevatedEncounter(float riderDistance, float speed)
        {
            float start = nextPatternDistance;
            if (start < nextElevatedDistance || start < nextVehicleSafeDistance) return false;
#if UNITY_EDITOR
            if (courseCar != null) return false;
#endif
            RouteDefinition route = roadStream.Route;
            int index = route.SectionAt(start, out float sectionStart);
            RouteSection section = route.Sections[index];
            ObstacleSet catalog = section.Map.Obstacles;
            ObstacleDefinition bus = catalog?.ElevatedBus;
            VehiclePattern[] patterns = catalog?.VehiclePatterns;
            if (section.Transition || bus == null || patterns == null || patterns.Length == 0 ||
                start < sectionStart + 48f) return false;

            VehiclePattern pattern = patterns[vehiclePatternIndex % patterns.Length];
            if (pattern == VehiclePattern.ArticulatedPair) pattern = VehiclePattern.ArticulatedRoofBridge;
            if (bus.Id == "wood_container" &&
                (pattern == VehiclePattern.BusConvoy || pattern == VehiclePattern.AlternatingBuses))
                pattern = VehiclePattern.RampConvoy;
            const float joint = 1.4f;
            float articulatedLength = 2f * bus.Length + joint;
            float roofStart = start + CourseRampLength;
            // Reserve time to change lanes after a drop and deck length for a jump with turbo.
            float takeoff = roofStart + bus.Length;
            float roofGap = Mathf.Max(8f, speed * .42f);
            float landing = takeoff + roofGap;
            int extraLandingBuses = Mathf.Max(1, Mathf.CeilToInt(
                ((speed + RunSession.BoostSpeed) * JumpArc.Duration - roofGap + 3f - articulatedLength) /
                (bus.Length + joint)));
            float laneSwitchGap = VehicleLaneSwitchGap(speed, catalog);
            float convoyGap = Mathf.Max(6f, speed * .25f);
            float alternatingGap = Mathf.Max(9f, speed * .35f);
            float end;
            switch (pattern)
            {
                case VehiclePattern.Articulated: end = start + articulatedLength; break;
                case VehiclePattern.RoofBridge: end = roofStart + bus.Length + 2f; break;
                case VehiclePattern.ArticulatedRoofBridge: end = roofStart + articulatedLength + 2f; break;
                case VehiclePattern.RoofJump: end = landing + articulatedLength + extraLandingBuses * (bus.Length + joint); break;
                case VehiclePattern.RampBarricade: end = roofStart + bus.Length + 2f; break;
                case VehiclePattern.BusConvoy: end = start + 3f * bus.Length + 2f * convoyGap; break;
                case VehiclePattern.RampConvoy: end = roofStart + 3f * bus.Length + 2f * laneSwitchGap; break;
                case VehiclePattern.FlankedRamp: end = roofStart + 2f * bus.Length + laneSwitchGap; break;
                case VehiclePattern.AlternatingBuses: end = start + 3f * bus.Length + 2f * alternatingGap; break;
                default: end = roofStart + articulatedLength; break;
            }
            float exitClearance = Mathf.Max(28f, laneSwitchGap);
            if (route.NeedsGentlePatterns(start - 6f, end + exitClearance) ||
                route.TouchesDecisionApproach(start - 1f, end + 1f)) return false;

            int lane = previousVehicleLane < 0 ? random.Next(3) : (previousVehicleLane + 1 + random.Next(2)) % 3;
            int neighbor = lane == 0 ? 1 : lane == 2 ? 1 : random.Next(2) * 2;
            bus = catalog.PickPatternVehicle(random);
            var scenery = new GameObject(pattern.ToString()).transform;
            scenery.SetParent(transform, false);
            scenery.localPosition = Vector3.forward * (start - riderDistance);
            var encounter = new ElevatedEncounter { Ramp = scenery, Start = start, End = end };
            elevatedEncounters.Add(encounter);
#if UNITY_EDITOR
            scenery.gameObject.SetActive(previewObstacles);
#endif

            float LaneX(int target) => (target - 1) * LaneSpacing;
            void Surface(string name, float a, float b, float x, float width, float low, float high, bool roofBridge = false)
            {
                encounter.Surfaces.Add(new VehicleSurface(a, b, x, width * .5f, low, high));
                if (name != null) CreateCourseSurface(name, x, a - start, b - start, low, high, width, bus, scenery, roofBridge);
            }
            void Bus(int target, float at, ObstacleDefinition body = null)
            {
                body ??= catalog.PickPatternVehicle(random);
                Place(ItemKind.Obstacle, target, at - riderDistance + bus.Length * .5f, 0f, body);
            }
            void Articulated(int target, float at)
            {
                var body = catalog.PickPatternVehicle(random);
                Bus(target, at, body);
                float seam = at + bus.Length;
                Bus(target, seam + joint, body);
                float x = LaneX(target);
                Surface("Articulated joint", seam, seam + joint, x, bus.Width, bus.Height, bus.Height);
            }
            void Gem(int target, float at, float height) =>
                Place(ItemKind.Gem, target, at - riderDistance, 1.1f + height);
            void RoofGemTrail(int target, float edge)
            {
                for (int i = 3; i > 0; i--) Gem(target, edge - 1.5f * i, bus.Height);
            }

            if (pattern == VehiclePattern.BusConvoy)
            {
                for (int i = 0; i < 3; i++) Bus(lane, start + i * (bus.Length + convoyGap));
            }
            else if (pattern == VehiclePattern.AlternatingBuses)
            {
                for (int i = 0; i < 3; i++) Bus(i % 2 == 0 ? lane : neighbor, start + i * (bus.Length + alternatingGap));
            }
            else if (pattern == VehiclePattern.Articulated)
            {
                Articulated(lane, start);
            }
            else
            {
                Surface("Entry ramp", start, roofStart, LaneX(lane), 2f, 0f, bus.Height);
                for (int i = 1; i <= 4; i++) Gem(lane, start + CourseRampLength * i / 5f, bus.Height * i / 5f);
                switch (pattern)
                {
                    case VehiclePattern.RampConvoy:
                        Bus(lane, roofStart);
                        for (int i = 1; i <= 2; i++) Bus(lane, roofStart + i * (bus.Length + laneSwitchGap));
                        RoofGemTrail(lane, takeoff);
                        break;
                    case VehiclePattern.FlankedRamp:
                        Articulated(lane, roofStart);
                        Bus(neighbor, roofStart);
                        Surface("Roof crossing", roofStart, roofStart + bus.Length,
                            (LaneX(lane) + LaneX(neighbor)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Bus(3 - lane - neighbor, roofStart + bus.Length + laneSwitchGap);
                        Gem(lane, roofStart + articulatedLength - 2f, bus.Height);
                        break;
                    case VehiclePattern.RoofBridge:
                        Bus(lane, roofStart);
                        Bus(neighbor, roofStart + 2f);
                        Surface("Roof crossing", roofStart + 2f, roofStart + bus.Length,
                            (LaneX(lane) + LaneX(neighbor)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Gem(neighbor, end - 1f, bus.Height);
                        break;
                    case VehiclePattern.ArticulatedRoofBridge:
                        Articulated(lane, roofStart);
                        Articulated(neighbor, roofStart + 2f);
                        Surface("Roof crossing", roofStart + 2f, roofStart + bus.Length,
                            (LaneX(lane) + LaneX(neighbor)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Gem(lane, roofStart + 1f, bus.Height);
                        Gem(neighbor, roofStart + bus.Length - 1f, bus.Height);
                        Gem(neighbor, end - 2f, bus.Height);
                        break;
                    case VehiclePattern.RoofJump:
                        Bus(lane, roofStart);
                        Articulated(neighbor, landing);
                        for (int i = 0; i < extraLandingBuses; i++)
                        {
                            float seam = landing + articulatedLength + i * (bus.Length + joint);
                            Surface("Roof landing joint", seam, seam + joint, LaneX(neighbor),
                                bus.Width, bus.Height, bus.Height, roofBridge: true);
                            Bus(neighbor, seam + joint);
                        }
                        RoofGemTrail(lane, takeoff);
                        for (int i = 1; i <= 3; i++) Gem(neighbor, landing + i * 3f, bus.Height);
                        break;
                    case VehiclePattern.RampBarricade:
                        for (int i = 0; i < 3; i++) Bus(i, roofStart + (i == lane ? 0f : 2f));
                        for (int i = 0; i < 2; i++)
                            Surface("Roof crossing", roofStart + 2f, roofStart + bus.Length,
                                (LaneX(i) + LaneX(i + 1)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Gem(lane, roofStart + bus.Length - 1f, bus.Height);
                        break;
                    default:
                        Articulated(lane, roofStart);
                        Gem(lane, end - 2f, bus.Height);
                        break;
                }
            }
            vehiclePatternIndex++;
            previousVehicleLane = lane;
            lastPatternEnd = end;
            nextVehicleSafeDistance = end + exitClearance;
            nextPatternDistance = end + exitClearance;
            nextElevatedDistance = start + (section.EncounterProfile?.VehiclePatternInterval ?? ElevatedInterval) / obstacleIntensity;
            return true;
        }

        private TrackContact UpdateVehicleCourse(float distance, float travel, float oldX, float x, float time, float dt)
        {
            float initialDistance = distance - travel;
            float initialHeight = rider.localPosition.y;
            float jumpHeight = jump.HeightAt(time);
            float beyondArc = jump.FallTimeAfterArc(time, dt);
            bool grounded = jumpHeight <= 0f && beyondArc < 0f && courseFallSpeed == 0f;
            float fallTime = beyondArc >= 0f ? beyondArc : dt;
            float fallSpeed = beyondArc >= 0f ? JumpArc.EndDescentSpeed : courseFallSpeed;
            float freeEndHeight = jumpHeight > 0f ? courseJumpBase + jumpHeight :
                (beyondArc >= 0f ? courseJumpBase : initialHeight) -
                (fallSpeed + .5f * CourseFallGravity * fallTime) * fallTime;
            float rampRise = 0f;
            float supportHeight = initialHeight <= TrackContact.SurfaceTolerance ? 0f : -1f;
            if (jumpHeight > 0f) supportHeight = -1f;
            float contactAt = supportHeight >= 0f ? 0f : 1f;
            float contactHeight = 0f;
            if (freeEndHeight <= 0f && supportHeight < 0f)
            {
                supportHeight = 0f;
                contactAt = Mathf.InverseLerp(initialHeight, freeEndHeight, 0f);
            }

            for (int i = elevatedEncounters.Count - 1; i >= 0; i--)
            {
                ElevatedEncounter encounter = elevatedEncounters[i];
                if (distance > encounter.End + 12f)
                {
                    Destroy(encounter.Ramp.gameObject);
                    elevatedEncounters.RemoveAt(i);
                    continue;
                }
                Vector3 position = encounter.Ramp.localPosition;
                position.z = encounter.Start - distance;
                encounter.Ramp.localPosition = position;
                if (!grounded) continue;
                foreach (VehicleSurface surface in encounter.Surfaces)
                {
                    if (surface.High <= surface.Low || initialDistance < surface.Start || initialDistance > surface.End ||
                        Mathf.Abs(oldX - surface.X) > surface.HalfWidth) continue;
                    float prior = SurfaceHeight(surface, initialDistance);
                    if (Mathf.Abs(prior - initialHeight) <= TrackContact.SurfaceTolerance)
                        rampRise = Mathf.Max(rampRise, SurfaceHeight(surface, distance) - prior);
                }
            }

            void ConsiderSurface(VehicleSurface surface)
            {
                bool atRoofEdge = surface.Vehicle != null &&
                    Mathf.Abs((visualLane - 1) * LaneSpacing - surface.X) < .01f &&
                    freeEndHeight <= initialHeight && initialHeight >= surface.High - RoofLandingEdgeTolerance;
                float halfWidth = surface.HalfWidth + (atRoofEdge ? ObstacleLateralPadding : 0f);
                if (distance < surface.Start || distance > surface.End ||
                    Mathf.Abs(x - surface.X) > halfWidth) return;
                float enter = 0f, exit = 1f;
                if (!TrackContact.Clip(initialDistance, distance, surface.Start, surface.End, ref enter, ref exit) ||
                    !TrackContact.Clip(oldX, x, surface.X - halfWidth, surface.X + halfWidth, ref enter, ref exit))
                    return;
                float entryHeight = SurfaceHeight(surface, Mathf.Lerp(initialDistance, distance, enter));
                float endHeight = SurfaceHeight(surface, distance);
                if (grounded && entryHeight <= initialHeight + rampRise + TrackContact.SurfaceTolerance &&
                    (surface.High > surface.Low || endHeight >= initialHeight - TrackContact.SurfaceTolerance))
                {
                    if (endHeight <= supportHeight) return;
                    supportHeight = endHeight;
                    contactAt = enter;
                    contactHeight = entryHeight;
                    return;
                }
                float aboveAtEntry = Mathf.Lerp(initialHeight, freeEndHeight, enter) - entryHeight;
                float aboveAtEnd = freeEndHeight - endHeight;
                float edgeTolerance = atRoofEdge ? RoofLandingEdgeTolerance : TrackContact.SurfaceTolerance;
                if (aboveAtEntry < -edgeTolerance || aboveAtEnd > 0f) return;
                float at = Mathf.Lerp(enter, 1f, Mathf.InverseLerp(aboveAtEntry, aboveAtEnd, 0f));
                if (at > contactAt || (at == contactAt && endHeight <= supportHeight)) return;
                contactAt = at;
                contactHeight = SurfaceHeight(surface, Mathf.Lerp(initialDistance, distance, at));
                supportHeight = endHeight;
            }
            foreach (ElevatedEncounter encounter in elevatedEncounters)
                foreach (VehicleSurface surface in encounter.Surfaces) ConsiderSurface(surface);
            foreach (VehicleSurface surface in vehicleRoofs) ConsiderSurface(surface);

            if (supportHeight >= 0f)
            {
                courseJumpBase = supportHeight;
                courseFallSpeed = 0f;
                jump.Reset();
                if (contactAt == 0f)
                {
                    return new TrackContact(oldX, x, initialHeight, supportHeight);
                }
                return new TrackContact(oldX, x, initialHeight, freeEndHeight, contactAt, contactHeight, supportHeight);
            }
            if (jumpHeight <= 0f)
                courseFallSpeed = fallSpeed + CourseFallGravity * fallTime;
            return new TrackContact(oldX, x, initialHeight, freeEndHeight);
        }

        private static float SurfaceHeight(VehicleSurface surface, float distance) =>
            Mathf.Lerp(surface.Low, surface.High, Mathf.InverseLerp(surface.Start, surface.End, distance));

#if UNITY_EDITOR
        public void ConfigureArtPreview(bool gems, bool obstacles, bool showRider, bool enableCollisions)
        {
            bool visibilityChanged = gems != previewGems || obstacles != previewObstacles;
            previewGems = gems; previewObstacles = obstacles; previewInvulnerable = !enableCollisions;
            rider.gameObject.SetActive(showRider); riderShadow.gameObject.SetActive(showRider);
            foreach (ElevatedEncounter encounter in elevatedEncounters) encounter.Ramp.gameObject.SetActive(obstacles);
            if (visibilityChanged)
                foreach (TrackItem item in items)
                    item.View.gameObject.SetActive(item.Active && (item.Kind == ItemKind.Gem ? gems : obstacles));
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color32(255, 114, 90, 255);
            foreach (TrackItem item in items)
            {
                if (!item.Active || item.Kind != ItemKind.Obstacle || item.ObstacleHit) continue;
                ObstacleDefinition obstacle = item.Definition;
                Gizmos.DrawWireCube(new Vector3(item.LaneX, obstacle.CollisionHeight * .5f, item.View.localPosition.z),
                    new Vector3(obstacle.Width + 2f * ObstacleLateralPadding, obstacle.CollisionHeight,
                        obstacle.Length + 2f * obstacle.ContactPadding));
            }
            Gizmos.color = Color.cyan;
            foreach (VehicleSurface surface in vehicleRoofs)
                Gizmos.DrawWireCube(new Vector3(surface.X, surface.High, (surface.Start + surface.End) * .5f - riderDistance),
                    new Vector3(surface.HalfWidth * 2f, 0f, surface.End - surface.Start));
            foreach (ElevatedEncounter encounter in elevatedEncounters)
            {
                foreach (VehicleSurface surface in encounter.Surfaces)
                {
                    float offset = encounter.Ramp.localPosition.z - encounter.Start;
                    Vector3 a = new Vector3(surface.X - surface.HalfWidth, surface.Low, surface.Start + offset);
                    Vector3 b = new Vector3(surface.X + surface.HalfWidth, surface.Low, surface.Start + offset);
                    Vector3 c = new Vector3(surface.X + surface.HalfWidth, surface.High, surface.End + offset);
                    Vector3 d = new Vector3(surface.X - surface.HalfWidth, surface.High, surface.End + offset);
                    Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
                }
            }
            if (lastDebugContact.HasValue)
            {
                Vector3 marker = lastDebugContact.Value;
                marker.z -= riderDistance;
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(marker, .15f);
            }
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
        public void ConfigureDepthBend(bool enabled) => depthBendRequested = enabled;
        public void ConfigurePickupEffects(bool enabled)
        {
            bool stopGemSpin = pickupEffectsEnabled && !enabled;
            pickupEffectsEnabled = enabled;
            if (enabled) return;
            if (stopGemSpin)
            {
                foreach (TrackItem item in items)
                    if (item.Active && item.Kind == ItemKind.Gem)
                        item.View.localRotation = Quaternion.identity;
            }
            ClearGemScatter();
            if (gemFlash != null) gemFlash.enabled = false;
            if (letterBeam != null) letterBeam.enabled = false;
            UpdateLetterMotes(null, 0f);
        }

        public void ConfigureGemGlow(bool enabled)
        {
            if (gemGlowEnabled == enabled) return;
            gemGlowEnabled = enabled;
            foreach (TrackItem item in items)
                if (item.Active && item.Kind == ItemKind.Gem) UpdateGemGlow(item);
        }

        private void UpdateGemGlow(TrackItem item)
        {
            if (gemGlowEnabled && item.GemGlow == null && settings.GemFlash.Sprite != null)
            {
                var glow = new GameObject("Brillo de gema").AddComponent<SpriteRenderer>();
                glow.transform.SetParent(item.View, false);
                glow.transform.localScale = Vector3.one * (1.35f / Mathf.Max(.01f, settings.GemFlash.Sprite.bounds.size.x));
                glow.sprite = settings.GemFlash.Sprite;
                item.GemGlow = glow;
            }
            if (item.GemGlow == null) return;
            item.GemGlow.enabled = gemGlowEnabled;
            Color tint = item.Pink ? settings.Brand.Pink : settings.Brand.White;
            tint.a = .19f;
            item.GemGlow.color = tint;
        }

        private void UpdateDepthBend()
        {
            if (settings == null) return;
            depthBendStart = settings.DepthBendStart;
            float strength = depthBendRequested && settings.DepthBendAvailable
                ? Mathf.Abs(settings.DepthBendStrength) * roadStream.Route.TransitionBendAt(riderDistance, settings.TransitionBendLeadDistance)
                : 0f;
            bool enabled = roadStream.ConfigureDepthBend(strength != 0f, depthBendStart, strength);
            refreshDepthBend = depthBendStrength != 0f || enabled;
            depthBendStrength = enabled ? strength : 0f;
        }
        public event Action<int, bool> GemFeedback;
        public event Action<bool, bool> PinkGemMissed;
        public event Action<bool, int, bool> ImpactFeedback;
        public RouteDefinition Route => roadStream.Route;
        private enum ItemKind { Gem, Obstacle }
        private sealed class TrackItem
        {
            public Transform View;
            public float LaneX;
            public ItemKind Kind;
            public ObstacleDefinition Definition;
            public int AirborneNote;
            public bool ObstacleHit;
            public float? ImpactTime;
            public bool Active;
            public bool Pink;
            public bool PinkGemApproached;
            public int PinkDodgeEncounter;
            public MeshRenderer GemMesh;
            public Vector3 GemRestScale;
            public SpriteRenderer GemGlow;
        }
        private readonly struct Crossing
        {
            public readonly TrackItem Item;
            public readonly float Fraction;
            public Crossing(TrackItem item, float fraction) { Item = item; Fraction = fraction; }
        }
        private static readonly Comparison<Crossing> CrossingOrder = (a, b) => a.Fraction.CompareTo(b.Fraction);

        public void Initialize(GameSettings configuration)
        {
            settings = configuration;
            riderShadowScale = riderShadow.localScale;
            if (settings.GemFlash.Sprite == null)
                Debug.LogWarning("GameSettings has no Gem Flash Sprite assigned.", this);
            cameraOrigin = worldCamera.transform.localPosition;
            cameraRotation = worldCamera.transform.localRotation;
            riderRenderers = rider.GetComponentsInChildren<Renderer>(true);
            riderMaterials = new Material[riderRenderers.Length][];
            riderFadeMaterials = new Material[riderRenderers.Length][];
            for (int i = 0; i < riderRenderers.Length; i++)
            {
                Material[] original = riderMaterials[i] = riderRenderers[i].sharedMaterials;
                riderFadeMaterials[i] = new Material[original.Length];
                for (int slot = 0; slot < original.Length; slot++)
                {
                    Material material = original[slot];
                    if (material == null || material.shader.name != "Standard")
                    {
                        riderFadeMaterials[i][slot] = material;
                        continue;
                    }
                    if (!riderFadeCopies.TryGetValue(material, out Material fade))
                    {
                        fade = new Material(material);
                        Color color = fade.color;
                        color.a *= .45f;
                        fade.color = color;
                        fade.SetOverrideTag("RenderType", "Transparent");
                        fade.SetFloat("_Mode", 2f);
                        fade.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        fade.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        fade.SetInt("_ZWrite", 0);
                        fade.DisableKeyword("_ALPHATEST_ON");
                        fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        fade.EnableKeyword("_ALPHABLEND_ON");
                        fade.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        riderFadeCopies.Add(material, fade);
                    }
                    riderFadeMaterials[i][slot] = fade;
                }
            }
            if (settings.RiderImpactShader != null)
            {
                riderImpactMaterial = new Material(settings.RiderImpactShader);
                riderWhiteMaterials = new Material[riderRenderers.Length][];
                for (int i = 0; i < riderRenderers.Length; i++)
                {
                    riderWhiteMaterials[i] = new Material[riderMaterials[i].Length];
                    for (int slot = 0; slot < riderWhiteMaterials[i].Length; slot++)
                        riderWhiteMaterials[i][slot] = riderImpactMaterial;
                }
            }
            roadStream.Initialize(settings.DepthBendShader);
            wheelOrigins = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) wheelOrigins[i] = wheels[i].localRotation;
            PrepareRiderVisuals();
            for (int i = 0; i < 72; i++) AddToPool(gemPrefab, ItemKind.Gem);
            foreach (var section in roadStream.Route.Sections) WarmSet(section.Map.Obstacles);
        }
        public void ResetTrack()
        {
#if UNITY_EDITOR
            lastDebugContact = null;
#endif
            brakeSlideStarted = float.NegativeInfinity;
            brakeMarkStarted = float.NegativeInfinity;
            if (brakeMark != null) brakeMark.positionCount = 0;
            if (brakeDust != null) brakeDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            riderVisual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            riderVisual.localScale = Vector3.one;
            steeringPivot.localRotation = Quaternion.identity;
            hips.Reset(); torso.Reset(); head.Reset();
            foreach (RiderLimb limb in legs) limb.Reset();
            foreach (RiderLimb limb in arms) limb.Reset();
            turboAura?.Clear();
            ClearGemScatter();
            riderDistance = 0f;
            ClearImpactBlink();
            random = new System.Random(settings.Seed);
            foreach (TrackItem item in items) Hide(item);
            jump.Reset(); nextPatternDistance = FirstPatternDistance; wave = 0; safeLane = visualLane = 1;
            nextProfileObstacle = 100f;
            nextVehicleSafeDistance = 0f;
            gemRowsSincePink = pinkDodgeSequence = 0;
            lastPinkGemDistance = 19f;
            nextDecisionGemSection = 0;
            decisionGemSectionStart = 0f;
            previousVehicleLane = -1;
            encounterSection = -1;
            lastPatternEnd = 19f; // End of the opening gem row.
            ResetLetters();
            foreach (ElevatedEncounter encounter in elevatedEncounters) Destroy(encounter.Ramp.gameObject);
            elevatedEncounters.Clear();
            courseJumpBase = courseFallSpeed = nextElevatedDistance = 0f;
            vehiclePatternIndex = 0;
            laneOrigin = laneTime = pedalPhase = wheelSpin = lanePoseDirection = 0f;
            lanePoseAge = LanePoseSeconds;
            sprintPose = sprintFrom = sprintAge = airPose = 0f;
            riderSway = riderBank = riderBrace = riderTakeoff = riderLanding = 0f;
            landingAge = LandingPoseSeconds;
            takeoffAge = TakeoffPreloadSeconds;
            sprinting = wasAirborne = false;
            rider.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            UpdateRiderShadow(0f, 0f, 0f);
            roadStream.ResetRoad();
            for (int i = 0; i < wheels.Length; i++) wheels[i].localRotation = wheelOrigins[i];
            cameraVelocity = Vector3.zero;
            boostCameraBlend = 0f;
            worldCamera.transform.SetLocalPositionAndRotation(cameraOrigin, cameraRotation);
            if (gemFlash != null) gemFlash.enabled = false;
#if UNITY_EDITOR
            if (previewRoofJumpBus != null)
            {
                SpawnRoofJumpCourse();
                return;
            }
#endif
            for (int i = 0; i < 4; i++) Place(ItemKind.Gem, 1, 10 + i * 3, 1.1f, pink: i == 3);
#if UNITY_EDITOR
            SpawnVehicleCourse();
#endif
            SetFieldOfView(60f);
        }
        public void InitializeTurboAura()
        {
            if (turboAura != null || settings.TurboAuraShader == null || settings.TurboAuraTexture == null) return;
            var aura = new GameObject("Turbo aura");
            aura.transform.SetParent(rider, false);
            turboAura = aura.AddComponent<TurboAuraController>();
            turboAura.Initialize(settings.TurboAuraShader, settings.TurboAuraTexture);
        }

        public void ClearTurboAura() => turboAura?.Clear();

        public void ClearGemScatter()
        {
            for (int i = 0; i < scatteredGemCount; i++)
                scatteredGems[i].gameObject.SetActive(false);
            scatteredGemCount = 0;
        }

        private void ScatterGems(int count, bool duringBoost)
        {
            ClearGemScatter();
            if (!pickupEffectsEnabled) return;
            count = Mathf.Min(count, duringBoost ? TurboImpactScatterLimit : NormalImpactScatterLimit);
            gemScatterOrigin = rider.localPosition + Vector3.up * .9f;
            gemScatterTime = 0f;
            for (int i = 0; i < count; i++)
            {
                // Jitter each sector of the burst so trajectories spread without forming rows.
                float angle = 2f * Mathf.PI * (i + (float)scatterRandom.NextDouble()) / count;
                float reach = Mathf.Lerp(1.4f, 4f, (float)scatterRandom.NextDouble());
                scatterTrajectories[i] = new Vector3(Mathf.Cos(angle) * reach,
                    Mathf.Lerp(.8f, 1.8f, (float)scatterRandom.NextDouble()), Mathf.Sin(angle) * reach - .8f);
                scatterSpins[i] = new Vector3(scatterRandom.Next(-300, 301),
                    scatterRandom.Next(-540, 541), scatterRandom.Next(-180, 181));
                if (scatteredGems[i] == null) scatteredGems[i] = Instantiate(gemPrefab, transform).transform;
                scatteredGems[i].SetLocalPositionAndRotation(gemScatterOrigin, Quaternion.identity);
                scatteredGems[i].gameObject.SetActive(true);
            }
            scatteredGemCount = count;
        }

        private void AdvanceGemScatter(float dt)
        {
            if (scatteredGemCount == 0) return;
            gemScatterTime += dt;
            if (gemScatterTime >= .8f)
            {
                ClearGemScatter();
                return;
            }

            float t = gemScatterTime / .8f;
            float hop = Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI)) * (1f - t);
            for (int i = 0; i < scatteredGemCount; i++)
            {
                Vector3 trajectory = scatterTrajectories[i];
                Vector3 offset = new Vector3(trajectory.x * t, trajectory.y * hop - .7f * t, trajectory.z * t);
                scatteredGems[i].SetLocalPositionAndRotation(gemScatterOrigin + offset, Quaternion.Euler(scatterSpins[i] * t));
            }
        }

        public bool TryJump(float time)
        {
            if (courseFallSpeed != 0f || !jump.TryStart(time)) return false;
            takeoffAge = 0f;
            return true;
        }
        public void Advance(RunSession session, float dt, bool reducedMotion, bool boostCamera = false, bool boostAura = false)
        {
            AdvanceGemScatter(dt);
            if (boostAura) turboAura?.Advance(session.BoostActive, reducedMotion, dt, settings);
            else turboAura?.Clear();
            riderDistance = session.Distance;
            float oldX = rider.localPosition.x;
            if (session.Lane != visualLane)
            {
                laneOrigin = oldX; laneTime = 0; visualLane = session.Lane;
                lanePoseAge = 0f;
                lanePoseDirection = Mathf.Sign((visualLane - 1) * LaneSpacing - oldX);
            }
            laneTime += dt;
            float targetX = (visualLane - 1) * LaneSpacing;
            float blend = Mathf.Clamp01(laneTime / settings.LaneChangeTime);
            // Smoothstep has a short fixed duration and can be redirected during a transition.
            float x = Mathf.Lerp(laneOrigin, targetX, blend * blend * (3 - 2 * blend));
            float travel = session.Speed * dt;
            TrackContact step = UpdateVehicleCourse(session.Distance, travel, oldX, x, session.Elapsed, dt);
            rider.localPosition = new Vector3(x, step.EndHeight, 0);
            AnimateRider(session.BoostActive, jump.HeightAt(session.Elapsed) > 0f || courseFallSpeed > 0f,
                (step.EndHeight - step.StartHeight) / dt, travel, reducedMotion, dt);
            UpdateRiderShadow(x, step.EndHeight, 0f);
            roadStream.Advance(session.Distance);
            crossings.Clear();
            bool spinGems = pickupEffectsEnabled && !reducedMotion;
            Quaternion gemRotation = spinGems
                ? Quaternion.AngleAxis(Mathf.Repeat(session.Elapsed * GemSpinDegreesPerSecond, 360f), Vector3.up)
                : Quaternion.identity;
            Quaternion glowRotation = gemGlowEnabled ? Quaternion.Inverse(gemRotation) : Quaternion.identity;
            foreach (TrackItem item in items)
            {
                if (!item.Active) continue;
                float previousZ = item.View.localPosition.z;
                item.View.localPosition += Vector3.back * travel;
                if (item.ImpactTime.HasValue && item.Definition.TumblesOnImpact)
                {
                    float progress = Mathf.Clamp01((session.Elapsed - item.ImpactTime.Value) / .4f);
                    float direction = item.LaneX < 0 ? -1f : 1f;
                    Vector3 position = item.View.localPosition;
                    position.x = item.LaneX + direction * 1.8f * progress;
                    position.y = 2.4f * progress * (1f - progress);
                    item.View.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, 0f, -direction * 150f * progress));
                }
                if (item.Kind == ItemKind.Gem)
                {
                    if (pickupEffectsEnabled) item.View.localRotation = gemRotation;
                    if (gemGlowEnabled && item.GemGlow != null)
                        item.GemGlow.transform.localRotation = glowRotation;
                    if (item.Pink && !item.PinkGemApproached)
                        item.PinkGemApproached = PassedNearPinkGem(item, previousZ, step, session.Speed);
                    if (previousZ >= 0 && item.View.localPosition.z < 0)
                        crossings.Add(new Crossing(item, Mathf.Clamp01(previousZ / travel)));
                }
                else if (!item.ObstacleHit && TryObstacleContact(item, previousZ, step, out float fraction))
                    crossings.Add(new Crossing(item, fraction));
            }
            crossings.Sort(CrossingOrder);
            foreach (Crossing crossing in crossings)
            {
                if (session.State != RunState.Running) break;
                TrackItem item = crossing.Item;
                float fraction = crossing.Fraction;
                float crossingX = step.XAt(fraction);
                float crossingHeight = step.HeightAt(fraction);
                bool sameLane = Mathf.Abs(item.LaneX - crossingX) < .83f;
                if (sameLane && item.Kind == ItemKind.Gem && JumpArc.CanCollect(crossingHeight, item.View.localPosition.y))
                {
                    bool turboWasReady = session.CanActivateTurbo;
                    session.CollectGem(item.AirborneNote, item.Pink, item.PinkDodgeEncounter);
                    if (item.View.gameObject.activeInHierarchy)
                        EmitPickupFlash(transform.TransformPoint(new Vector3(item.LaneX, item.View.localPosition.y, 0)), settings.GemFlash);
                    Hide(item);
                    GemFeedback?.Invoke(item.AirborneNote, !turboWasReady && session.CanActivateTurbo);
                }
                else if (item.Pink)
                {
                    bool nearMiss = item.PinkGemApproached;
                    bool streakLost = session.MissPinkGem(nearMiss, item.PinkDodgeEncounter);
                    Hide(item);
                    PinkGemMissed?.Invoke(nearMiss, streakLost);
                }
                else if (item.Kind == ItemKind.Obstacle
#if UNITY_EDITOR
                    && !previewInvulnerable
#endif
                )
                {
                    item.ObstacleHit = true;
                    bool hasRoofSupport = item.Definition.RideableRoof;
                    // Vehicles stay clear of the camera after contact. Small obstacles tumble aside.
                    if (!item.Definition.TumblesOnImpact)
                    {
                        Hide(item);
                    }
                    if (session.IsProtected) continue;
                    item.ImpactTime = session.Elapsed;
                    bool duringBoost = session.BoostActive;
                    int previousGemsLost = session.GemsLost;
                    bool streakLost = session.PinkGemStreak > 0;
                    session.Hit(item.Definition.Id, item.PinkDodgeEncounter);
                    int gemsLost = session.GemsLost - previousGemsLost;
                    if (settings.LogObstacleCollisions)
                    {
                        float contactDistance = session.Distance - travel * (1f - fraction);
#if UNITY_EDITOR
                        lastDebugContact = new Vector3(crossingX, crossingHeight, contactDistance);
#endif
                        string zone = roadStream.Route.MapAtDistance(contactDistance).DisplayName;
                        int obstacleLane = Mathf.RoundToInt(item.LaneX / LaneSpacing) + 2;
                        Debug.Log($"[Choque] Ciclista → {item.Definition.Id} · {zone} · {contactDistance:F1} m · " +
                            $"carril destino {session.Lane + 1}, obstáculo {obstacleLane} · " +
                            $"altura al contacto {crossingHeight:F2} m, volumen {item.Definition.CollisionHeight:F2} m · " +
                            $"techo transitable {hasRoofSupport} · altura en el paso {step.StartHeight:F2} → {step.EndHeight:F2} m · " +
                            $"desfase lateral {crossingX - item.LaneX:F2} m · " +
                            $"velocidad {session.Speed:F1} m/s · turbo {duringBoost} · gemas perdidas {gemsLost}", item.View);
                    }
                    if (duringBoost)
                    {
                        turboAura?.Clear();
                        var position = worldCamera.transform.localPosition;
                        position.z = cameraOrigin.z;
                        worldCamera.transform.localPosition = position;
                        cameraVelocity.z = 0f;
                        SetFieldOfView(Mathf.Max(60f, referenceFieldOfView - settings.BoostCameraFieldOfView * boostCameraBlend));
                        boostCameraBlend = 0f;
                    }
                    if (gemsLost > 0) ScatterGems(gemsLost, duringBoost);
                    ImpactFeedback?.Invoke(duringBoost, gemsLost, streakLost);
                }
            }
            foreach (TrackItem item in items)
            {
                float behind = item.Definition != null ? -item.Definition.Length * .5f - 9 : -8;
                if (item.Active && item.Definition != null && item.PinkDodgeEncounter > 0 &&
                    item.View.localPosition.z < -item.Definition.Length * .5f - item.Definition.ContactPadding)
                {
                    if (!item.ObstacleHit) session.ClearPinkDodge(item.PinkDodgeEncounter);
                    item.PinkDodgeEncounter = 0;
                }
                if (item.Active && item.View.localPosition.z < behind) Hide(item);
            }
            AdvanceLetter(travel, step, session, dt);
            using (PopulateAheadMarker.Auto())
                PopulateAhead(session.Distance, session.Speed);
            bool safeForRecovery = jump.HeightAt(session.Elapsed) <= 0f && courseFallSpeed == 0f;
            foreach (TrackItem item in items)
                if (item.Active && item.Definition != null && Mathf.Abs(item.LaneX - x) < .83f &&
                    item.View.localPosition.z > -item.Definition.Length * .5f &&
                    item.View.localPosition.z < session.Speed * 1.5f + item.Definition.Length * .5f)
                    safeForRecovery = false;
            session.UpdateRecovery(safeForRecovery);
            session.ShowSpeedOffer();
            float protection = session.ProtectionProgress;
            const float whiteFlashEnd = .07f;
            bool showImpact = !reducedMotion && session.IsProtected;
            SetRiderImpactWhite(showImpact && protection < whiteFlashEnd);
            float blinkPhase = (protection - whiteFlashEnd) * 3f / (1f - whiteFlashEnd);
            SetRiderImpactTransparent(showImpact && blinkPhase >= 0f && Mathf.Repeat(blinkPhase, 1f) >= .65f);
            float boostTarget = session.BoostActive && !reducedMotion ? 1f : 0f;
            boostCameraBlend = reducedMotion || !boostCamera ? 0f :
                Mathf.Lerp(boostCameraBlend, boostTarget, 1f - Mathf.Exp(-settings.BoostCameraBlendRate * dt));
            FollowRider(x, reducedMotion, dt);
            if (session.IsProtected && !reducedMotion) return;
            float pace = session.Speed - (session.BoostActive ? RunSession.BoostSpeed : 0f);
            float intensity = Mathf.InverseLerp(settings.InitialSpeed, settings.CruiseSpeed, pace);
            float targetFieldOfView = reducedMotion ? 60f : Mathf.Lerp(60f, 68f, intensity) +
                settings.BoostCameraFieldOfView * boostCameraBlend;
            SetFieldOfView(Mathf.Lerp(referenceFieldOfView, targetFieldOfView, 1f - Mathf.Exp(-10f * dt)));
        }

        internal void PrepareOpening()
        {
            using (PrepareOpeningMarker.Auto())
            {
                SetEncounterIntensity(1f, settings.BaseObstacleIntensity);
                PopulateAhead(0f, settings.InitialSpeed);
            }
        }

        private void PopulateAhead(float distance, float speed)
        {
#if UNITY_EDITOR
            if (previewRoofJumpBus != null) return;
#endif
            // Speed changes the reveal horizon while the route positions stay fixed.
            float lookAhead = Mathf.Max(settings.MinimumPatternLookAhead, speed * settings.PreviewTime);
            if (settings.PreparePatternsBeyondVisibility)
                lookAhead = Mathf.Max(lookAhead, roadStream.VisibilityDistance + settings.PatternVisibilityMargin);
            float spawnHorizon = distance + lookAhead;
            SpawnDecisionGems(spawnHorizon, distance);
            float clearance = Mathf.Max(0f, letterClearance);
            while (true)
            {
                float letterDistance = Mathf.Max(nextLetterDistance, lastPatternEnd + clearance);
                if (nextLetterDistance <= nextPatternDistance && CanPlaceLetter(letterDistance, clearance))
                {
                    if (letterDistance > spawnHorizon) break;
                    if (TryPlaceLetter(letterDistance, distance))
                    {
                        lastPatternEnd = letterDistance;
                        nextPatternDistance = Mathf.Max(nextPatternDistance, letterDistance + clearance);
                    }
                }
                if (nextPatternDistance > spawnHorizon) break;
#if UNITY_EDITOR
                if (SkipVehicleCourse(ref nextPatternDistance)) continue;
#endif

                if (TrySpawnElevatedEncounter(distance, speed) || TrySpawnProfileEncounter(distance, speed)) continue;
                if (nextPatternDistance < nextVehicleSafeDistance)
                {
                    nextPatternDistance = nextVehicleSafeDistance;
                    continue;
                }
                lastPatternEnd = SpawnPattern(nextPatternDistance, distance, speed);
                nextPatternDistance = Mathf.Max(nextPatternDistance + settings.PatternSpacingAt(nextPatternDistance) / obstacleIntensity,
                    lastPatternEnd + settings.PatternClearance);
            }
        }

        private void UpdateRiderShadow(float x, float height, float z)
        {
            Vector3 forward = riderVisual.localRotation * Vector3.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            riderShadow.SetLocalPositionAndRotation(new Vector3(x, .025f, z), Quaternion.AngleAxis(yaw, Vector3.up));
            float size = Mathf.Clamp(1f - height * .12f, .35f, 1f);
            riderShadow.localScale = new Vector3(riderShadowScale.x * size, riderShadowScale.y, riderShadowScale.z * size);
        }

        private void FollowRider(float x, bool reducedMotion, float dt)
        {
            Vector3 cameraTarget = cameraOrigin + (reducedMotion ? Vector3.zero : Vector3.right * (x * settings.CameraFollowStrength));
            cameraTarget.y += courseFallSpeed > 0f ? rider.localPosition.y : courseJumpBase;
            cameraTarget += Vector3.back * (settings.BoostCameraPullback * boostCameraBlend);
            worldCamera.transform.localPosition = Vector3.SmoothDamp(worldCamera.transform.localPosition, cameraTarget, ref cameraVelocity, settings.CameraFollowTime, Mathf.Infinity, dt);
        }

        public void ClearImpactBlink()
        {
            SetRiderImpactWhite(false);
            SetRiderImpactTransparent(false);
        }

        private void SetRiderImpactTransparent(bool transparent)
        {
            if (riderImpactTransparent == transparent) return;
            riderImpactTransparent = transparent;
            ApplyRiderImpactMaterials();
        }

        private void SetRiderImpactWhite(bool white)
        {
            if (riderImpactMaterial == null || riderImpactWhite == white) return;
            riderImpactWhite = white;
            ApplyRiderImpactMaterials();
        }

        private void ApplyRiderImpactMaterials()
        {
            Material[][] materials = riderImpactWhite ? riderWhiteMaterials :
                riderImpactTransparent ? riderFadeMaterials : riderMaterials;
            for (int i = 0; i < riderRenderers.Length; i++)
                riderRenderers[i].sharedMaterials = materials[i];
        }

        private void OnDestroy()
        {
            if (brakeDustMaterial != null) Destroy(brakeDustMaterial);
            if (brakeDustMesh != null) Destroy(brakeDustMesh);
            if (riderImpactMaterial != null) Destroy(riderImpactMaterial);
            foreach (Material fade in riderFadeCopies.Values) Destroy(fade);
            if (pinkGemMaterials != null)
                foreach (Material material in pinkGemMaterials) Destroy(material);
        }
        /// <summary>Scale upcoming profiled encounters independently. Existing encounters stay in place.</summary>
        public void SetEncounterIntensity(float gems, float obstacles)
        {
            gemIntensity = Mathf.Clamp(gems, .25f, 4f);
            obstacleIntensity = Mathf.Clamp(obstacles, .25f, 4f);
        }

        private float nextProfileGems;

        private void SpawnDecisionGems(float horizon, float distance)
        {
            RouteDefinition route = roadStream.Route;
            while (nextDecisionGemSection < route.Sections.Length)
            {
                float length = route.Sections[nextDecisionGemSection].ModuleCount * RoadStandard.ModuleLength;
                float midpoint = decisionGemSectionStart + length * .5f;
                float lead = Mathf.Min(24f, length * .25f);
                if (midpoint - lead > horizon) return;
                if (route.HasDecision(nextDecisionGemSection))
                {
                    for (int i = 1; i <= 6; i++)
                        Place(ItemKind.Gem, 1, midpoint - lead * (1f - i / 7f) - distance, 1.1f);
                }
                nextDecisionGemSection++;
                decisionGemSectionStart += length;
            }
        }

        private bool TrySpawnProfileEncounter(float riderDistance, float speed)
        {
            float distance = nextPatternDistance;
            RouteDefinition route = roadStream.Route;
            int index = route.SectionAt(distance, out float start);
            RouteSection section = route.Sections[index];
            EncounterProfile profile = section.EncounterProfile;
            if (profile == null) return false;

            float end = index == route.Sections.Length - 1
                ? float.PositiveInfinity : start + section.ModuleCount * RoadStandard.ModuleLength;
            if (index != encounterSection)
            {
                encounterSection = index;
                nextProfileGems = distance;
                nextProfileObstacle = Mathf.Max(100f, distance + 6f);
            }
            // Earlier encounters, letters and decisions can advance the shared cursor.
            bool spawnGems = nextProfileGems <= nextProfileObstacle;
            if (!spawnGems && distance < nextVehicleSafeDistance)
            {
                nextProfileObstacle = Mathf.Max(nextProfileObstacle, nextVehicleSafeDistance);
                nextPatternDistance = Mathf.Min(end, Mathf.Max(distance, Mathf.Min(nextProfileGems, nextProfileObstacle)));
                return true;
            }
            float occupiedEnd = distance;
            if (spawnGems)
            {
                int count = profile.GemsPerThread;
                float rowEnd = distance + (count - 1) * profile.GemSpacing;
                occupiedEnd = rowEnd;
                if (occupiedEnd + 2f < end && !route.TouchesDecisionApproach(distance, occupiedEnd))
                {
                    safeLane = Mathf.Clamp(safeLane + random.Next(-1, 2), 0, 2);
                    int threads = profile.GemThreads;
                    if (threads > 1 && profile.ParallelThreadChance < 1f &&
                        random.NextDouble() >= profile.ParallelThreadChance) threads = 1;
                    int lane = safeLane;
                    int turnAt = threads == 1 && count >= 5 && profile.LaneChangeChance > 0f &&
                        random.NextDouble() < profile.LaneChangeChance ? random.Next(2, count - 1) : -1;
                    int roseLane = turnAt >= 0 ? PickNeighborLane(lane) : lane;
                    gemRowsSincePink++;
                    bool pink = profile.PinkGemEveryRows > 0 && gemRowsSincePink >= profile.PinkGemEveryRows &&
                        occupiedEnd - lastPinkGemDistance >= profile.PinkGemMinimumSpacing;
                    int dodgeEncounter = 0;
                    int dodgeExitLane = -1;
                    if (pink && profile.PinkGemDodgeChance > 0f && random.NextDouble() < profile.PinkGemDodgeChance)
                    {
                        var catalog = obstacleOverride != null ? obstacleOverride : section.Map.Obstacles;
                        pink = TrySpawnPinkDodge(profile, catalog, distance, rowEnd, end, riderDistance, speed,
                            roseLane, out dodgeEncounter, out dodgeExitLane, out occupiedEnd);
                    }
                    if (pink)
                    {
                        gemRowsSincePink = 0;
                        lastPinkGemDistance = rowEnd;
                    }
                    for (int gem = 0; gem < count; gem++)
                    {
                        if (gem == turnAt)
                        {
                            lane = roseLane;
                            // Leave one slot empty so the lane change fits at boost speed.
                            continue;
                        }
                        for (int thread = 0; thread < threads; thread++)
                            Place(ItemKind.Gem, (lane + thread) % 3,
                                distance - riderDistance + gem * profile.GemSpacing, 1.1f,
                                pink: pink && thread == 0 && gem == count - 1,
                                pinkDodgeEncounter: pink && thread == 0 && gem == count - 1 ? dodgeEncounter : 0);
                    }
                    safeLane = dodgeExitLane >= 0 ? dodgeExitLane : lane;
                }
                float variation = profile.GemIntervalVariation;
                float jitter = variation > 0f ? 1f + variation * (2f * (float)random.NextDouble() - 1f) : 1f;
                nextProfileGems = distance + profile.GemInterval * jitter / gemIntensity;
                if (occupiedEnd > rowEnd)
                    nextProfileGems = Mathf.Max(nextProfileGems, occupiedEnd + Mathf.Max(8f, (nextVehicleSafeDistance - occupiedEnd) * .5f));
            }
            else
            {
                ObstacleSet catalog = obstacleOverride != null ? obstacleOverride : section.Map.Obstacles;
                // Leave a free path and enough distance to read and change lanes.
                float clearance = Mathf.Max(profile.ObstacleClearance, VehicleLaneSwitchGap(speed, catalog));
                float maximumLength = Mathf.Min(distance < 220f ? 4.5f : float.PositiveInfinity, end - distance - clearance);
                int lane = (safeLane + 1 + random.Next(2)) % 3;
                float occupied = PlaceObstacle(catalog, wave % 3 == 0, lane, distance - riderDistance, maximumLength);
                float extraObstacleChance = Mathf.Clamp01(obstacleIntensity - 1f + profile.ExtraObstacleChance);
                if (occupied > 0f && ((catalog?.ElevatedBus != null && wave % 2 == 1) ||
                    (extraObstacleChance > 0f && random.NextDouble() < extraObstacleChance)))
                {
                    int otherLane = 3 - safeLane - lane;
                    occupied = Mathf.Max(occupied,
                        PlaceObstacle(catalog, false, otherLane, distance - riderDistance, maximumLength));
                }
                occupiedEnd = distance + occupied;
                if (occupied > 0f)
                {
                    nextVehicleSafeDistance = occupiedEnd + clearance;
                    nextProfileGems = Mathf.Max(nextProfileGems, occupiedEnd + Mathf.Max(8f, clearance * .5f));
                }
                nextProfileObstacle = Mathf.Max(distance + profile.ObstacleInterval / obstacleIntensity, occupiedEnd + clearance);
                wave++;
            }
            lastPatternEnd = Mathf.Min(end, occupiedEnd);
            nextPatternDistance = Mathf.Min(end, Mathf.Max(Mathf.Min(nextProfileGems, nextProfileObstacle), occupiedEnd + 2f));
            return true;
        }

        private int PickNeighborLane(int lane) => lane == 1 ? (random.Next(2) == 0 ? 0 : 2) : 1;

        private bool TrySpawnPinkDodge(EncounterProfile profile, ObstacleSet catalog, float rowStart,
            float rowEnd, float sectionEnd, float riderDistance, float speed, int lane,
            out int encounter, out int exitLane, out float occupiedEnd)
        {
            encounter = 0;
            exitLane = -1;
            occupiedEnd = rowEnd;
            ObstacleDefinition vehicle = catalog?.ElevatedBus;
            if (vehicle == null || vehicle.CollisionHeight <= JumpArc.ApexHeight ||
                vehicle.Width * .5f + ObstacleLateralPadding >= LaneSpacing || rowStart < 100f)
                return false;

            RouteDefinition route = roadStream.Route;
            float boostedSpeed = Mathf.Max(speed, settings.CruiseSpeed + route.SpeedBonusAt(rowEnd)) +
                RunSession.BoostSpeed + 2f;
            float front = rowEnd + boostedSpeed * (settings.LaneChangeTime + profile.PinkGemDodgeReactionTime);
            float length = vehicle.Length + 2f * vehicle.ContactPadding;
            float vehicleEnd = front + length;
            float clearance = Mathf.Max(profile.ObstacleClearance, VehicleLaneSwitchGap(boostedSpeed, catalog));
            if (front < nextVehicleSafeDistance || vehicleEnd + clearance >= sectionEnd ||
                route.TouchesDecisionApproach(rowStart, vehicleEnd + clearance)) return false;

            encounter = ++pinkDodgeSequence;
            exitLane = PickNeighborLane(lane);
            occupiedEnd = vehicleEnd;
            Place(ItemKind.Obstacle, lane, front + length * .5f - riderDistance, 0f, vehicle,
                pinkDodgeEncounter: encounter);
            nextVehicleSafeDistance = vehicleEnd + clearance;
            nextProfileObstacle = Mathf.Max(nextProfileObstacle, nextVehicleSafeDistance);
            return true;
        }

        private float VehicleLaneSwitchGap(float speed, ObstacleSet catalog = null)
        {
            // Budget for a turbo activation while approaching an encounter already on the track.
            float approachSpeed = Mathf.Max(speed, settings.InitialSpeed) + RunSession.BoostSpeed;
            // Allow time to read the second row and make up to two lane changes.
            return approachSpeed * (settings.LaneChangeTime * 2f +
                (catalog?.ElevatedBus?.Id == "wood_container" ? .8f : .25f)) + 1.2f;
        }

        private float SpawnPattern(float distance, float riderDistance, float speed)
        {
            safeLane = Mathf.Clamp(safeLane + random.Next(-1, 2), 0, 2);
            float z = distance - riderDistance;
            float spacing = settings.GemSpacingAt(distance);
            float patternEnd = distance + spacing * 3;
            if (roadStream.Route.TouchesDecisionApproach(distance, patternEnd)) return patternEnd;
            bool gentle = roadStream.Route.NeedsGentlePatterns(distance - 6, patternEnd + 6);
            bool jumping = !gentle && distance > 155f && wave % 4 == 2;
            for (int i = 0; i < 4; i++)
                Place(ItemKind.Gem, safeLane, z + i * spacing, jumping ? (i == 0 || i == 3 ? 2.05f : 2.7f) : 1.1f,
                    airborneNote: jumping ? i : -1);
            ObstacleSet obstacleSet = obstacleOverride != null ? obstacleOverride : roadStream.Route.MapAtDistance(distance).Obstacles;
            float maximumLength = gentle || distance < 220f ? 4.5f : float.PositiveInfinity;
            float obstacleOffset = spacing * 1.5f;
            float obstacleEnd = distance + obstacleOffset;
            if (jumping) obstacleEnd += PlaceObstacle(obstacleSet, false, safeLane, z + obstacleOffset, maximumLength);
            if (distance > 100f)
            {
                int blockedLane = (safeLane + 1 + random.Next(2)) % 3;
                obstacleEnd = Mathf.Max(obstacleEnd, distance + obstacleOffset +
                    PlaceObstacle(obstacleSet, wave % 3 == 0, blockedLane, z + obstacleOffset, maximumLength));
                if (!gentle && !jumping && distance > 300f && wave % 3 == 1)
                {
                    int otherLane = 3 - safeLane - blockedLane;
                    obstacleEnd = Mathf.Max(obstacleEnd, distance + obstacleOffset +
                        PlaceObstacle(obstacleSet, false, otherLane, z + obstacleOffset, maximumLength));
                }
            }
            // A jump row blocks at most two lanes; every other row keeps the gem lane clear.
            wave++;
            return Mathf.Max(patternEnd, obstacleEnd + VehicleLaneSwitchGap(speed, obstacleSet));
        }

        private void ResetLetters()
        {
            if (letterPickup != null) letterPickup.gameObject.SetActive(false);
            if (letterBeam != null) letterBeam.enabled = false;
            activeLetter = -1;
            activeWord = null;
            pendingLetters.Clear();
            nextLetterDistance = 100f;
            scheduledLettersPlaced = 0;
            scheduledLetterStart = scheduledLetterEnd = 0f;
            RouteDefinition route = roadStream.Route;
            if (route.LastLetterFinalSectionModule > 0 && route.Sections.Length > 1)
            {
                float moduleLength = RoadStandard.ModuleLength;
                scheduledLetterStart = route.Sections[0].ModuleCount * moduleLength;
                if (route.Sections[1].Transition) scheduledLetterStart += route.Sections[1].ModuleCount * moduleLength;
                scheduledLetterStart += letterClearance;
                for (int i = 0; i < route.Sections.Length - 1; i++)
                {
                    if (route.LastLetterZone != null && route.Sections[i].Map == route.LastLetterZone &&
                        !route.Sections[i].Transition) break;
                    scheduledLetterEnd += route.Sections[i].ModuleCount * moduleLength;
                }
                scheduledLetterEnd += route.LastLetterFinalSectionModule * moduleLength;
                nextLetterDistance = scheduledLetterStart;
            }
        }

        private bool PrepareWord()
        {
            var font = LetterFont;
            if (font == null || collectibleWords == null) return false;

            for (int attempt = 0; attempt < collectibleWords.Length; attempt++)
            {
                int index = nextWord % collectibleWords.Length;
                nextWord = index + 1;
                string word = collectibleWords[index]?.Trim().Normalize().ToUpperInvariant();
                if (string.IsNullOrEmpty(word)) continue;
                if (Array.Exists(word.ToCharArray(), c => c != ' ' && !font.HasCharacter(c)))
                {
                    Debug.LogWarning($"Word collection skipped '{word}' because the font is missing a character.", this);
                    continue;
                }
                activeWord = word;
                collectedLetters = new bool[word.Length];
                presentedLetters = new bool[word.Length];
                var positions = new List<int>(word.Length);
                for (int i = 0; i < word.Length; i++)
                    if (word[i] != ' ') positions.Add(i);
                for (int i = positions.Count - 1; i > 0; i--)
                {
                    int j = letterRandom.Next(i + 1);
                    (positions[i], positions[j]) = (positions[j], positions[i]);
                }
                foreach (int position in positions) pendingLetters.Enqueue(position);
                return true;
            }
            return false;
        }

        private bool CanPlaceLetter(float distance, float clearance) =>
            wordCollectionEnabled && activeLetter < 0 && (activeWord == null || pendingLetters.Count > 0) &&
            !roadStream.Route.NeedsGentlePatterns(distance - clearance, distance + clearance) &&
            !roadStream.Route.TouchesDecisionApproach(distance, distance);

        private bool TryPlaceLetter(float distance, float riderDistance)
        {
            if (activeWord == null && !PrepareWord())
            {
                nextLetterDistance = float.PositiveInfinity;
                return false;
            }
            if (pendingLetters.Count == 0) return false;

            activeLetter = pendingLetters.Dequeue();
            SetLetterText(activeWord[activeLetter]);
            // The reserved gap clears all lanes, so choose a lane other than the previous gem row.
            int letterLane = (safeLane + letterRandom.Next(1, 3)) % 3;
            letterLaneX = (letterLane - 1) * LaneSpacing;
            letterHoverTime = 0f;
            letterBeamTime = 0f;
            letterPickup.transform.SetLocalPositionAndRotation(
                new Vector3(letterLaneX, LetterPickupHeight, distance - riderDistance),
                Quaternion.identity);
            letterPickup.gameObject.SetActive(true);
            UpdateLetterBeam(letterPickup.transform.localPosition, 0f);
            if (!presentedLetters[activeLetter])
            {
                presentedLetters[activeLetter] = true;
                scheduledLettersPlaced++;
            }
            int letterCount = 0;
            foreach (char character in activeWord)
                if (character != ' ') letterCount++;
            nextLetterDistance = scheduledLetterEnd > scheduledLetterStart && scheduledLettersPlaced < letterCount
                ? Mathf.Max(distance + 20f, Mathf.Lerp(scheduledLetterStart, scheduledLetterEnd,
                    scheduledLettersPlaced / (float)(letterCount - 1)))
                : distance + Mathf.Max(20f, letterInterval);
            return true;
        }

        private void SetLetterText(char character)
        {
            bool createText = letterPickup == null;
            if (createText)
            {
                var visual = new GameObject("Letter pickup", typeof(TMPro.TextMeshPro));
                visual.transform.SetParent(transform, false);
                letterPickup = visual.GetComponent<TMPro.TextMeshPro>();
                letterPickup.fontSize = 20f;
                letterPickup.fontStyle = TMPro.FontStyles.Bold;
                letterPickup.alignment = TMPro.TextAlignmentOptions.Center;
                letterPickup.rectTransform.sizeDelta = new Vector2(4f, 4f);
                letterPickup.extraPadding = true;
                letterPickup.enableVertexGradient = true;
                Color top = new Color32(255, 226, 89, 255);
                Color bottom = new Color32(255, 169, 52, 255);
                letterPickup.colorGradient = new TMPro.VertexGradient(top, top, bottom, bottom);
            }

            var font = LetterFont;
            if (createText || letterPickup.font != font)
            {
                letterPickup.font = font;
                if (letterOutlineMaterial != null && letterOutlineMaterial.mainTexture == font.atlasTexture)
                    letterPickup.fontSharedMaterial = letterOutlineMaterial;
                else
                {
                    letterPickup.outlineColor = Color.black;
                    letterPickup.outlineWidth = .3f;
                    Material material = letterPickup.fontMaterial;
                    material.SetFloat("_FaceDilate", .3f);
                    material.EnableKeyword("OUTLINE_ON");
                    letterPickup.UpdateMeshPadding();
                }
            }
            letterPickup.text = character.ToString();
        }
        private void AdvanceLetter(float travel, TrackContact step, RunSession session, float dt)
        {
            if (activeLetter < 0) return;
            Vector3 position = letterPickup.transform.localPosition;
            float nextZ = position.z - travel;
            if (wordCollectionEnabled && nextZ >= 0f)
            {
                float cycle = Mathf.Max(.1f, letterHoverCycle);
                letterHoverTime = Mathf.Repeat(letterHoverTime + dt, cycle);
                float phase = letterHoverTime / cycle;
                float hover = .5f - .5f * Mathf.Cos(4f * Mathf.PI * phase);
                float tilt = Mathf.SmoothStep(0f, 1f, phase * 4f)
                    - Mathf.SmoothStep(0f, 1f, (phase - .5f) * 4f);
                letterPickup.transform.SetLocalPositionAndRotation(
                    new Vector3(letterLaneX, LetterPickupHeight + hover * letterHoverHeight, nextZ),
                    Quaternion.AngleAxis(tilt * letterTilt, Vector3.forward));
                UpdateLetterBeam(letterPickup.transform.localPosition, dt);
                return;
            }

            int index = activeLetter;
            activeLetter = -1;
            letterPickup.gameObject.SetActive(false);
            if (letterBeam != null) letterBeam.enabled = false;
            float fraction = travel > 0f ? Mathf.Clamp01(position.z / travel) : 0f;
            if (!wordCollectionEnabled || Mathf.Abs(letterLaneX - step.XAt(fraction)) >= .83f ||
                !JumpArc.CanCollect(step.HeightAt(fraction), LetterPickupHeight))
            {
                pendingLetters.Enqueue(index);
                return;
            }
            collectedLetters[index] = true;
            session.RecordLetter(activeWord, index, pendingLetters.Count == 0);
            EmitPickupFlash(transform.TransformPoint(new Vector3(letterLaneX, position.y, 0f)), settings.LetterFlash);
            LetterCollected?.Invoke(activeWord, collectedLetters, pendingLetters.Count == 0);
        }

        private void UpdateLetterBeam(Vector3 letterPosition, float dt)
        {
            Sprite sprite = pickupEffectsEnabled ? settings.LetterBeamSprite : null;
            if (sprite == null)
            {
                if (letterBeam != null) letterBeam.enabled = false;
                UpdateLetterMotes(null, 0f);
                return;
            }
            if (letterBeam == null)
            {
                var visual = new GameObject("Letter beam", typeof(SpriteRenderer));
                visual.transform.SetParent(transform, false);
                letterBeam = visual.GetComponent<SpriteRenderer>();
                letterBeamProperties = new MaterialPropertyBlock();
            }

            float duration = settings.LetterBeamPulseDuration;
            letterBeamTime = Mathf.Repeat(letterBeamTime + dt, duration);
            // Traverse the curves in both directions to return smoothly to the initial state.
            float phase = Mathf.PingPong(2f * letterBeamTime / duration, 1f);
            Bounds bounds = sprite.bounds;
            float baseScale = settings.LetterBeamBaseScale;
            float heightPulse = settings.LetterBeamHeightPulse * Mathf.Clamp01(settings.LetterBeamHeightCurve.Evaluate(phase));
            float verticalScale = baseScale + heightPulse / bounds.size.y;
            letterBeam.sprite = sprite;
            letterBeam.sortingOrder = settings.LetterBeamInFront ? 1 : -1;
            if (settings.LetterBeamMaterial != null)
            {
                letterBeam.sharedMaterial = settings.LetterBeamMaterial;
                letterBeam.GetPropertyBlock(letterBeamProperties);
                letterBeamProperties.SetColor(FlashTopTint, settings.LetterBeamTopTint);
                letterBeamProperties.SetColor(FlashBottomTint, settings.LetterBeamBottomTint);
                letterBeamProperties.SetFloat(FlashRadialGradient, 0f);
                letterBeam.SetPropertyBlock(letterBeamProperties);
            }
            letterBeam.transform.localScale = new Vector3(baseScale, verticalScale, 1f);
            Vector3 basePosition = new Vector3(letterPosition.x, 0f, letterPosition.z) + settings.LetterBeamBaseOffset;
            letterBeam.transform.localPosition = basePosition - Vector3.up * (bounds.min.y * verticalScale);
            letterBeam.transform.rotation = Quaternion.Euler(0f, worldCamera.transform.eulerAngles.y, 0f);
            Color color = Color.white;
            color.a *= Mathf.Lerp(settings.LetterBeamMinOpacity, settings.LetterBeamMaxOpacity,
                settings.LetterBeamOpacityCurve.Evaluate(phase));
            letterBeam.color = color;
            letterBeam.enabled = true;
            UpdateLetterMotes(settings.LetterMoteSprite, letterBeamTime / duration);
        }

        private void UpdateLetterMotes(Sprite sprite, float phase)
        {
            for (int i = 0; i < letterMotes.Length; i++)
            {
                SpriteRenderer mote = letterMotes[i];
                if (sprite == null)
                {
                    if (mote != null) mote.enabled = false;
                    continue;
                }
                if (mote == null)
                {
                    var visual = new GameObject("Letter mote", typeof(SpriteRenderer));
                    visual.transform.SetParent(letterPickup.transform, false);
                    mote = letterMotes[i] = visual.GetComponent<SpriteRenderer>();
                    mote.sortingOrder = 2;
                }
                float angle = (phase + (float)i / letterMotes.Length) * 2f * Mathf.PI;
                mote.transform.localPosition = new Vector3(.85f * Mathf.Cos(angle),
                    .52f * Mathf.Sin(2f * angle + i), .42f * Mathf.Sin(angle));
                mote.transform.rotation = worldCamera.transform.rotation;
                float scale = (.65f + .25f * Mathf.Sin(angle + i)) * settings.LetterMoteScaleMultiplier;
                mote.transform.localScale = Vector3.one * scale;
                Color color = settings.LetterMoteTint;
                color.a *= Mathf.Lerp(.28f, .7f, .5f + .5f * Mathf.Cos(angle - i));
                mote.sprite = sprite;
                mote.color = color;
                mote.enabled = true;
            }
        }
        private void AddToPool(GameObject prefab, ItemKind kind, ObstacleDefinition definition = null)
        {
            GameObject instance = Instantiate(prefab, transform); instance.SetActive(false);
            if (definition != null) instance.GetComponent<ObstacleView>().Apply(definition);
            items.Add(new TrackItem { View = instance.transform, Kind = kind, Definition = definition,
                GemMesh = kind == ItemKind.Gem ? instance.GetComponent<MeshRenderer>() : null,
                GemRestScale = instance.transform.localScale });
        }

        private void ApplyGemAppearance(TrackItem item, bool pink)
        {
            if (item.Pink != pink)
            {
                if (item.GemMesh != null)
                {
                    if (pinkGemMaterials == null)
                    {
                        regularGemMaterials = item.GemMesh.sharedMaterials;
                        pinkGemMaterials = new Material[regularGemMaterials.Length];
                        Color pinkTint = settings.Brand.Pink;
                        for (int i = 0; i < pinkGemMaterials.Length; i++)
                        {
                            var material = new Material(regularGemMaterials[i]);
                            Color tint = i == 1 ? Color.Lerp(pinkTint, settings.Brand.Red, .28f)
                                : i == 2 ? Color.Lerp(pinkTint, settings.Brand.White, .38f) : pinkTint;
                            material.color = tint;
                            material.EnableKeyword("_EMISSION");
                            material.SetColor("_EmissionColor", tint * .65f);
                            pinkGemMaterials[i] = material;
                        }
                    }
                    item.GemMesh.sharedMaterials = pink ? pinkGemMaterials : regularGemMaterials;
                }
                item.View.localScale = item.GemRestScale * (pink ? 1.22f : 1f);
                item.Pink = pink;
            }
            UpdateGemGlow(item);
        }
        private TrackItem Place(ItemKind kind, int lane, float z, float height, ObstacleDefinition definition = null,
            int airborneNote = -1, bool pink = false, int pinkDodgeEncounter = 0)
        {
            TrackItem item = null;
            foreach (TrackItem candidate in items)
            {
                if (candidate.Active || candidate.Kind != kind || candidate.Definition != definition) continue;
                item = candidate;
                break;
            }
            if (item == null)
            {
                AddToPool(definition == null ? gemPrefab : definition.Prefab.gameObject, kind, definition);
                item = items[items.Count - 1];
            }

            item.Active = true;
            item.ObstacleHit = false;
            item.ImpactTime = null;
            item.AirborneNote = airborneNote;
            item.PinkGemApproached = false;
            item.PinkDodgeEncounter = pinkDodgeEncounter;
            item.LaneX = (lane - 1) * LaneSpacing;
            if (definition != null && definition.RideableRoof)
            {
                float center = riderDistance + z;
                float halfLength = definition.Length * .5f;
                vehicleRoofs.Add(new VehicleSurface(center - halfLength, center + halfLength, item.LaneX,
                    definition.Width * .5f, definition.Height, definition.Height, item));
            }
            item.View.SetLocalPositionAndRotation(new Vector3(item.LaneX, kind == ItemKind.Gem ? height : 0, z), Quaternion.identity);
            if (kind == ItemKind.Gem) ApplyGemAppearance(item, pink);
            bool visible = true;
#if UNITY_EDITOR
            visible = kind == ItemKind.Gem ? previewGems : previewObstacles;
#endif
            item.View.gameObject.SetActive(visible);
            return item;
        }
        /// <summary>Override map selection for an event or experiment; pass null to restore map-defined traffic.</summary>
        public void SetObstacleOverride(ObstacleSet replacement)
        {
            WarmSet(replacement); obstacleOverride = replacement;
        }
        private void WarmSet(ObstacleSet set)
        {
            if (set == null) return;
            if (set.Low != null) foreach (var definition in set.Low) WarmObstacle(definition);
            if (set.Traffic != null) foreach (var definition in set.Traffic) WarmObstacle(definition);
            WarmObstacle(set.ElevatedBus);
        }
        private void WarmObstacle(ObstacleDefinition definition)
        {
            if (definition == null || !registeredObstacles.Add(definition)) return;
            for (int i = 0; i < definition.PoolCapacity; i++) AddToPool(definition.Prefab.gameObject, ItemKind.Obstacle, definition);
        }
        private float PlaceObstacle(ObstacleSet set, bool vehicle, int lane, float z, float maximumLength)
        {
            if (set == null) return 0;
            ObstacleDefinition definition = set.Pick(random, vehicle, maximumLength, vehicle && wave % 3 == 0 && float.IsPositiveInfinity(maximumLength));
            if (definition == null) return 0;
            float padding = definition.ContactPadding;
            float start = riderDistance + z;
            if (roadStream.Route.TouchesDecisionApproach(start, start + definition.Length + 2f * padding)) return 0f;
            Place(ItemKind.Obstacle, lane, z + definition.Length * .5f + padding, 0, definition);
            // Contact starts at z and ends after the full padded collider passes.
            return definition.Length + 2f * padding;
        }
        private static bool PassedNearPinkGem(TrackItem item, float previousZ, TrackContact step, float speed)
        {
            const float lateralMargin = 1.12f;
            const float verticalMargin = 1f;
            float enter = 0f, exit = 1f;
            float approachDistance = Mathf.Min(5f, speed * .12f);
            if (!TrackContact.Clip(previousZ, item.View.localPosition.z, 0f, approachDistance, ref enter, ref exit) ||
                !TrackContact.Clip(step.StartX, step.EndX, item.LaneX - lateralMargin,
                    item.LaneX + lateralMargin, ref enter, ref exit)) return false;

            float gemHeight = item.View.localPosition.y - 1.1f;
            float split = Mathf.Clamp(step.LandingFraction, enter, exit);
            return HeightsOverlap(step.HeightAt(enter), step.HeightAt(split)) ||
                   HeightsOverlap(step.HeightAt(split), step.HeightAt(exit));

            bool HeightsOverlap(float first, float second) =>
                Mathf.Min(first, second) <= gemHeight + verticalMargin &&
                Mathf.Max(first, second) >= gemHeight - verticalMargin;
        }

        private static bool TryObstacleContact(TrackItem item, float previousZ, TrackContact step, out float fraction)
        {
            fraction = 0f;
            ObstacleDefinition obstacle = item.Definition;
            float halfLength = obstacle.Length * .5f + obstacle.ContactPadding;
            float halfWidth = obstacle.Width * .5f + ObstacleLateralPadding;
            float enter = 0f, exit = 1f;
            return TrackContact.Clip(previousZ, item.View.localPosition.z, -halfLength, halfLength, ref enter, ref exit) &&
                TrackContact.Clip(step.StartX, step.EndX, item.LaneX - halfWidth, item.LaneX + halfWidth, ref enter, ref exit) &&
                step.TryHit(enter, exit, obstacle.CollisionHeight, out fraction);
        }
        private void EmitPickupFlash(Vector3 position, PickupFlashSettings flash)
        {
            if (!pickupEffectsEnabled) return;
            if (flash.Sprite == null) return;
            if (gemFlash == null)
            {
                var visual = new GameObject("Gem flash", typeof(SpriteRenderer));
                visual.transform.SetParent(transform, false);
                gemFlash = visual.GetComponent<SpriteRenderer>();
                gemFlashProperties = new MaterialPropertyBlock();
                defaultFlashMaterial = gemFlash.sharedMaterial;
            }
            activeFlash = flash;
            gemFlash.sprite = flash.Sprite;
            gemFlash.sharedMaterial = flash.Material != null ? flash.Material : defaultFlashMaterial;
            gemFlash.GetPropertyBlock(gemFlashProperties);
            gemFlashProperties.SetColor(FlashTopTint, flash.TopTint);
            gemFlashProperties.SetColor(FlashBottomTint, flash.BottomTint);
            gemFlashProperties.SetFloat(FlashRadialGradient, flash.GradientMode == FlashGradientMode.Radial ? 1f : 0f);
            gemFlash.SetPropertyBlock(gemFlashProperties);
            float maxRoll = flash.RandomRotation;
            gemFlashRoll = Quaternion.AngleAxis(UnityEngine.Random.Range(-maxRoll, maxRoll), Vector3.forward);
            Vector3 flashPosition = position + transform.TransformDirection(flash.Offset);
            gemFlash.transform.SetPositionAndRotation(flashPosition, worldCamera.transform.rotation * gemFlashRoll);
            gemFlash.transform.localScale = Vector3.one * (flash.StartScale * flash.Size);
            gemFlash.color = new Color(1f, 1f, 1f, 0f);
            gemFlash.enabled = true;
            gemFlashStarted = Time.unscaledTime;
        }

        public void EndRun(bool braked, float speedBeforeBrake, bool cameraAndParticles)
        {
            ClearImpactBlink();
            ClearTurboAura();
            ClearGemScatter();
            if (gemFlash != null) gemFlash.enabled = false;
            if (letterBeam != null) letterBeam.enabled = false;
            UpdateLetterMotes(null, 0f);
            if (!braked) return;

            brakeSlideStarted = Time.unscaledTime;
            brakeInitialSpeed = Mathf.Max(0f, speedBeforeBrake);
            brakeStartRotation = riderVisual.localRotation;
            brakeStartSteering = steeringPivot.localRotation;
            brakeStartScale = riderVisual.localScale;
            brakeStartHeight = riderVisual.localPosition.y;
            brakeTravel = 0f;
            brakeCameraPosition = worldCamera.transform.localPosition;
            brakeCameraFov = referenceFieldOfView;
            brakeCameraEffects = cameraAndParticles;
            brakeGroundEffects = cameraAndParticles && rearWheel != null && brakeDust != null &&
                rider.localPosition.y <= .06f;
            nextBrakeParticle = 0f;
            brakeMarkPoints = 0;
            if (!brakeGroundEffects) return;
            brakeMark.positionCount = 0;
            brakeMark.startColor = brakeMark.endColor = BrakeMarkColor;
            brakeMarkStarted = brakeSlideStarted;
            brakeDust.Play();
            if (brakeInitialSpeed > 0f) AddBrakeContact(0f, brakeInitialSpeed);
        }

        private void PrepareRiderVisuals()
        {
            riderVisual = new GameObject("Visual del ciclista").transform;
            riderVisual.SetParent(rider, false);
            for (int i = rider.childCount - 2; i >= 0; i--)
            {
                Transform part = rider.GetChild(i);
                if (part.GetComponentInChildren<Collider>(true) == null)
                    part.SetParent(riderVisual, false);
            }

            var bars = riderVisual.Find("Handlebars");
            var fork = riderVisual.Find("Fork");
            steeringPivot = CreateVisualPivot("Dirección", riderVisual, bars != null ? bars.localPosition : Vector3.zero);
            if (bars != null) bars.SetParent(steeringPivot, true);
            if (fork != null) fork.SetParent(steeringPivot, true);
            foreach (Transform wheel in wheels)
            {
                if (wheel.parent != riderVisual) continue;
                if (frontWheel == null || wheel.localPosition.z > frontWheel.localPosition.z) frontWheel = wheel;
                if (rearWheel == null || wheel.localPosition.z < rearWheel.localPosition.z) rearWheel = wheel;
            }
            if (frontWheel != null && frontWheel != rearWheel) frontWheel.SetParent(steeringPivot, true);
            if (rearWheel != null && rearWheel.Find("Tyre") is Transform tyre &&
                tyre.TryGetComponent(out MeshFilter mesh) && mesh.sharedMesh != null)
            {
                Vector3 extents = Vector3.Scale(mesh.sharedMesh.bounds.extents, tyre.localScale);
                tyreRadii = new Vector2(extents.x, extents.y - extents.x);
            }

            Transform seat = riderVisual.Find("Seat");
            hips = new RiderPart(CreateVisualPivot("Cadera", riderVisual, seat != null ? seat.localPosition : Vector3.zero));
            torso = new RiderPart(CreateVisualPivot("Torso", hips.View, new Vector3(0f, .10f, .04f)));
            Transform headMesh = riderVisual.Find("Head");
            head = new RiderPart(CreateVisualPivot("Cabeza", torso.View, headMesh != null
                ? torso.View.InverseTransformPoint(headMesh.position) : Vector3.zero));
            var upperArms = new Transform[2];
            var forearms = new Transform[2];
            var hands = new Transform[2];
            for (int i = riderVisual.childCount - 1; i >= 0; i--)
            {
                Transform part = riderVisual.GetChild(i);
                int side = part.localPosition.x < 0f ? 0 : 1;
                if (part == headMesh || part.name.StartsWith("Helmet", System.StringComparison.Ordinal))
                    part.SetParent(head.View, true);
                else if (part.name == "Hoodie" || part.name == "Hood")
                    part.SetParent(torso.View, true);
                else if (part.name == "Upper arm") upperArms[side] = part;
                else if (part.name == "Forearm") forearms[side] = part;
                else if (part.name == "Hand") hands[side] = part;
            }
            arms = new RiderLimb[2];
            for (int i = 0; i < arms.Length; i++)
            {
                Transform upper = upperArms[i];
                if (upper == null) continue;
                Vector3 shoulder = upper.localPosition - upper.localRotation * (Vector3.up * upper.localScale.y);
                arms[i] = new RiderLimb(upper, forearms[i], hands[i], shoulder);
            }
            legs = new RiderLimb[pedals.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                Transform leg = pedals[i];
                if (leg.parent != riderVisual) continue;
                Transform thigh = leg.Find("Thigh"), shin = leg.Find("Shin"), shoe = leg.Find("Shoe");
                Vector3 hip = leg.localPosition;
                if (thigh != null) thigh.SetParent(riderVisual, true);
                if (shin != null) shin.SetParent(riderVisual, true);
                if (shoe != null) shoe.SetParent(riderVisual, true);
                legs[i] = new RiderLimb(thigh, shin, shoe, hip);
            }
            CreateBrakeGroundEffects();
        }

        private static Transform CreateVisualPivot(string name, Transform parent, Vector3 position)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            return pivot;
        }

        private void AnimateRider(bool turbo, bool airborne, float verticalSpeed, float travel, bool reducedMotion, float dt)
        {
            if (turbo != sprinting)
            {
                sprinting = turbo;
                sprintFrom = sprintPose;
                sprintAge = 0f;
            }
            sprintAge += dt;
            float sprintProgress = Mathf.Clamp01(sprintAge / (sprinting ? .15f : .30f));
            sprintPose = Mathf.SmoothStep(sprintFrom, sprinting ? 1f : 0f, sprintProgress);
            if (!sprinting && sprintProgress > .6f && sprintProgress < 1f)
                sprintPose -= .065f * Mathf.Max(0f, sprintFrom) * Mathf.Sin(Mathf.PI * (sprintProgress - .6f) / .4f);

            float sprint = reducedMotion ? 0f : sprintPose;
            pedalPhase = Mathf.Repeat(pedalPhase + travel * 35f * Mathf.Deg2Rad *
                (1f + .4f * Mathf.Clamp01(sprint)), 2f * Mathf.PI);
            float phase = pedalPhase;
            AnimateWheels(travel, true);

            lanePoseAge += dt;
            float lane = Mathf.Clamp01(lanePoseAge / LanePoseSeconds);
            float bank = lane < .72f
                ? Mathf.Sin(Mathf.PI * Mathf.SmoothStep(0f, 1f, lane / .72f))
                : -.12f * Mathf.Sin(Mathf.PI * (lane - .72f) / .28f);
            bank *= reducedMotion ? 0f : lanePoseDirection;
            float yaw = reducedMotion ? 0f : 17f * lanePoseDirection *
                Mathf.SmoothStep(0f, 1f, lane / .30f) *
                (1f - Mathf.SmoothStep(0f, 1f, (lane - .30f) / .60f));

            if (wasAirborne && !airborne) landingAge = 0f;
            wasAirborne = airborne;
            landingAge = airborne ? LandingPoseSeconds : Mathf.Min(LandingPoseSeconds, landingAge + dt);
            float landing = reducedMotion ? 0f : Mathf.SmoothStep(0f, 1f, landingAge / .035f) *
                (1f - Mathf.SmoothStep(0f, 1f, (landingAge - .035f) / .115f));
            float damping = 1f - Mathf.Exp(-28f * dt);
            airPose = Mathf.Lerp(airPose, airborne && !reducedMotion ? 1f : 0f, damping);
            float rise = airborne ? Mathf.Clamp01(verticalSpeed / JumpArc.EndDescentSpeed) : 0f;
            float fall = airborne ? Mathf.Clamp01(-verticalSpeed / JumpArc.EndDescentSpeed) : 0f;
            takeoffAge = Mathf.Min(TakeoffPreloadSeconds, takeoffAge + dt);
            float lift = Mathf.SmoothStep(0f, 1f, takeoffAge / TakeoffPreloadSeconds);
            float preload = reducedMotion ? 0f : Mathf.Sin(Mathf.PI * lift) * .32f;
            float pitch = reducedMotion ? 0f : -20f * rise * lift + 13f * fall;
            float braced = Mathf.Max(airPose, Mathf.Max(landing, Mathf.Clamp01(Mathf.Abs(bank) * 1.4f)));
            float sway = Mathf.Cos(phase + .36f * Mathf.Clamp01(sprint) * (1f - Mathf.Cos(phase))) * (1f - braced);
            float roll = -33f * bank + 4f * Mathf.Clamp01(sprint) * sway;
            Quaternion turning = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(roll, Vector3.forward);
            Quaternion flight = Quaternion.AngleAxis(pitch, Vector3.right);
            float frameDamping = lane < 1f ? 1f - Mathf.Exp(-60f * dt) : damping;
            Quaternion rotation = Quaternion.Slerp(riderVisual.localRotation, turning * flight, frameDamping);
            Quaternion steering = Quaternion.AngleAxis(22f * bank, Vector3.up);
            steeringPivot.localRotation = Quaternion.Slerp(steeringPivot.localRotation, steering, frameDamping);
            float compression = Mathf.Max(landing, preload);
            Vector3 scale = new Vector3(1f + .06f * compression,
                1f + .05f * rise * airPose * lift - .12f * compression, 1f);
            riderVisual.localScale = scale;
            riderVisual.SetLocalPositionAndRotation(Vector3.up * WheelClearance(rotation, scale), rotation);
            riderSway = sway;
            riderBank = bank;
            riderBrace = braced;
            riderTakeoff = airPose * rise * lift;
            riderLanding = compression;
            PoseRiderBody(phase, sprint, sway, bank, braced, riderTakeoff, compression);
        }

        private void AnimateWheels(float travel, bool rollRear)
        {
            wheelSpin = Mathf.Repeat(wheelSpin + travel * Mathf.Rad2Deg / .45f, 360f);
            Quaternion spin = Quaternion.AngleAxis(wheelSpin, Vector3.right);
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] == frontWheel || (rollRear && wheels[i] == rearWheel))
                    wheels[i].localRotation = wheelOrigins[i] * spin;
        }

        private float WheelClearance(Quaternion rotation, Vector3 scale)
        {
            if (frontWheel == null || rearWheel == null || tyreRadii.x <= 0f) return 0f;
            Vector3 up = Vector3.Scale(Quaternion.Inverse(rotation) * Vector3.up, scale);
            Quaternion steering = steeringPivot.localRotation;
            Vector3 front = steeringPivot.localPosition + steering * frontWheel.localPosition;
            // A tyre is a ring in its local YZ plane, with a round cross-section.
            float frontBottom = Vector3.Dot(up, front) - TyreRadiusAlong(Quaternion.Inverse(steering) * up);
            float rearBottom = Vector3.Dot(up, rearWheel.localPosition) - TyreRadiusAlong(up);
            return -Mathf.Min(frontBottom, rearBottom);
        }

        private float TyreRadiusAlong(Vector3 direction) =>
            tyreRadii.x * direction.magnitude + tyreRadii.y * new Vector2(direction.y, direction.z).magnitude;

        private void PoseRiderBody(float phase, float sprint, float sway, float bank, float braced,
            float takeoff, float landing, float skid = 0f)
        {
            float rock = Mathf.Clamp01(sprint);
            float hipRoll = -5f * sway;
            Vector3 hipOffset = new Vector3(sway * (.036f + .015f * rock),
                .20f * sprint + .008f * Mathf.Sin(2f * phase) * rock * (1f - braced) - .09f * landing,
                .16f * sprint - .115f * takeoff);
            Quaternion hipRotation = Quaternion.AngleAxis(hipRoll, Vector3.forward);
            // The hoodie already leans forward by 16 degrees in the prefab.
            Quaternion torsoRotation = Quaternion.Euler(9f + 15f * sprint + 9f * landing,
                -4f * sway + 11f * bank, 4f * sway - 7f * bank + 11f * skid);
            hips.Apply(hipOffset, hipRotation);
            torso.Apply(Vector3.zero, torsoRotation);
            Quaternion upperBody = hipRotation * torsoRotation;
            Quaternion headCounter = Quaternion.Inverse(upperBody) * Quaternion.Inverse(riderVisual.localRotation);
            head.Apply(Vector3.zero, Quaternion.Slerp(Quaternion.identity, headCounter, .90f));

            Vector3 torsoOrigin = hips.View.localPosition + hipRotation * torso.Position;
            Quaternion steering = steeringPivot.localRotation;
            for (int i = 0; i < arms.Length; i++)
            {
                RiderLimb arm = arms[i];
                Vector3 shoulder = torsoOrigin + upperBody * (arm.JointRoot - hips.Position - torso.Position);
                Vector3 grip = steeringPivot.localPosition + steering * (arm.Endpoint - steeringPivot.localPosition);
                arm.Pose(shoulder, grip, new Vector3(i == 0 ? -1f : 1f, -.45f, 0f), steering);
            }
            Vector3 localUp = Quaternion.Inverse(riderVisual.localRotation) * Vector3.up;
            float footLevel = Mathf.SmoothStep(0f, 1f, Mathf.Abs(bank)) * localUp.x / Mathf.Max(.5f, localUp.y);
            foreach (RiderLimb leg in legs)
            {
                bool left = leg.JointRoot.x < 0f;
                float angle = phase + (left ? 0f : Mathf.PI);
                angle += .36f * rock * (1f - Mathf.Cos(angle));
                float down = Mathf.Cos(angle);
                float fore = Mathf.Lerp(Mathf.Sin(angle), left ? -1f : 1f, braced);
                Vector3 foot = leg.Endpoint + new Vector3(left ? 0f : .09f * skid,
                    .11f * down * (1f - braced) - (left ? 0f : .08f * skid), .16f * fore);
                foot.y -= footLevel * foot.x;
                Vector3 hip = hips.View.localPosition + hipRotation * (leg.JointRoot - hips.Position);
                Quaternion ankle = Quaternion.AngleAxis(10f * Mathf.Max(0f, -down) * (1f - braced), Vector3.right);
                leg.Pose(hip, foot, Vector3.forward, ankle);
            }
        }

        private void CreateBrakeGroundEffects()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            brakeDustMaterial = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            var emitter = new GameObject("Partículas de frenada");
            emitter.transform.SetParent(transform, false);
            brakeDust = emitter.AddComponent<ParticleSystem>();
            brakeDust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = brakeDust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .36f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = brakeDust.emission;
            emission.enabled = false;
            var shape = brakeDust.shape;
            shape.enabled = false;
            var colorOverLifetime = brakeDust.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

            brakeDustMesh = new Mesh { name = "Polvo low poly" };
            brakeDustMesh.vertices = new[]
            {
                new Vector3(-.5f, -.35f, -.35f), new Vector3(.5f, -.35f, -.35f),
                new Vector3(0f, -.35f, .55f), new Vector3(0f, .6f, 0f)
            };
            brakeDustMesh.triangles = new[] { 0, 1, 2, 0, 3, 1, 1, 3, 2, 2, 3, 0 };
            brakeDustMesh.RecalculateNormals();
            var dustRenderer = emitter.GetComponent<ParticleSystemRenderer>();
            dustRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            dustRenderer.mesh = brakeDustMesh;
            dustRenderer.sharedMaterial = brakeDustMaterial;

            var mark = new GameObject("Marca de frenada");
            mark.transform.SetParent(transform, false);
            mark.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            brakeMark = mark.AddComponent<LineRenderer>();
            brakeMark.sharedMaterial = brakeDustMaterial;
            brakeMark.useWorldSpace = true;
            brakeMark.alignment = LineAlignment.TransformZ;
            brakeMark.startWidth = .10f;
            brakeMark.endWidth = .06f;
            brakeMark.positionCount = 0;
        }

        private void AddBrakeContact(float age, float visualSpeed)
        {
            Vector3 contact = rearWheel.position;
            contact.y = riderShadow.position.y + .009f;
            brakeMark.positionCount = ++brakeMarkPoints;
            brakeMark.SetPosition(brakeMarkPoints - 1, contact);
            if (visualSpeed <= 0f || age < nextBrakeParticle) return;

            Vector3 velocity = transform.TransformDirection(new Vector3(-.65f, .65f, -.35f)) *
                Mathf.Clamp(visualSpeed, .35f, 2f);
            var particle = new ParticleSystem.EmitParams
            {
                position = contact,
                velocity = velocity,
                startColor = new Color32(218, 217, 210, 175),
                startSize = .17f
            };
            brakeDust.Emit(particle, 2);
            if (age < .14f)
            {
                particle.velocity = velocity * 1.8f;
                particle.startColor = new Color32(255, 215, 134, 235);
                particle.startSize = .045f;
                particle.startLifetime = .10f;
                brakeDust.Emit(particle, 1);
            }
            nextBrakeParticle = age + .035f;
        }

        private void UpdateBrakeSlide()
        {
            float age = Time.unscaledTime - brakeSlideStarted;
            float progress = Mathf.Clamp01(age / BrakeSlideSeconds);
            if (!float.IsNegativeInfinity(brakeSlideStarted))
            {
                // The integral of the normalized exponential keeps the visual drift decelerating to zero.
                const float decay = 3.5f;
                const float tail = .03019738f;
                float exponential = Mathf.Exp(-decay * progress);
                float travel = brakeInitialSpeed * BrakeSlideSeconds *
                    ((1f - exponential) / decay - tail * progress) / (1f - tail);
                float visualSpeed = brakeInitialSpeed * Mathf.Max(0f, (exponential - tail) / (1f - tail));
                float driftScale = .16f * (.55f + .25f * progress);
                float drift = travel * driftScale / Mathf.Max(1f, brakeInitialSpeed * .16f / 4.5f);
                float turn = Mathf.SmoothStep(0f, 1f, progress / .55f);
                float overshoot = 7f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((progress - .42f) / .58f));
                float lean = Mathf.SmoothStep(0f, 1f, progress / .48f);
                Quaternion body = Quaternion.Euler(0f, 65f + overshoot, -24f);
                Quaternion rotation = Quaternion.Slerp(brakeStartRotation, body, turn);
                Vector3 scale = Vector3.Lerp(brakeStartScale, Vector3.one, turn);
                steeringPivot.localRotation = Quaternion.Slerp(brakeStartSteering,
                    Quaternion.AngleAxis(-40f, Vector3.up), turn);
                float clearance = Mathf.Lerp(brakeStartHeight, WheelClearance(rotation, scale), turn);
                riderVisual.localScale = scale;
                riderVisual.SetLocalPositionAndRotation(new Vector3(drift, clearance, travel), rotation);
                AnimateWheels(travel - brakeTravel, false);
                brakeTravel = travel;
                float riding = 1f - turn;
                PoseRiderBody(pedalPhase, sprintPose * riding, riderSway * riding, riderBank * riding,
                    Mathf.Lerp(riderBrace, 1f, turn), riderTakeoff * riding,
                    Mathf.Lerp(riderLanding, 1f, lean), lean);
                UpdateRiderShadow(rider.localPosition.x + drift, rider.localPosition.y, travel);

                // Follow the visual slide so the scenery keeps scrolling as the run ends.
                Vector3 cameraPosition = brakeCameraPosition + Vector3.forward * travel;
                if (brakeCameraEffects)
                {
                    float envelope = progress < 1f
                        ? Mathf.Sin(Mathf.PI * Mathf.SmoothStep(0f, 1f, progress))
                        : 0f;
                    float shake = Mathf.Sin(age * Mathf.PI * 14f) * .022f * envelope;
                    cameraPosition += new Vector3(shake, shake * .3f, 0f);
                    SetFieldOfView(brakeCameraFov - 3f * envelope);
                }
                worldCamera.transform.localPosition = cameraPosition;
                if (brakeGroundEffects && brakeInitialSpeed > 0f) AddBrakeContact(age, visualSpeed);
                if (progress >= 1f) brakeSlideStarted = float.NegativeInfinity;
            }

            if (float.IsNegativeInfinity(brakeMarkStarted)) return;
            float markAge = Time.unscaledTime - brakeMarkStarted;
            Color color = BrakeMarkColor;
            color.a *= 1f - Mathf.InverseLerp(.55f, 2.5f, markAge);
            brakeMark.startColor = brakeMark.endColor = color;
            if (markAge >= 2.5f)
            {
                brakeMark.positionCount = 0;
                brakeMarkStarted = float.NegativeInfinity;
            }
        }

        private void LateUpdate()
        {
            UpdateBrakeSlide();
            UpdateDepthBend();
            if (refreshDepthBend)
            {
                foreach (TrackItem item in items)
                {
                    if (!item.Active) continue;
                    BendPickup(item.View, item.LaneX);
                    if (item.Kind == ItemKind.Obstacle)
                    {
                        float distance = Mathf.Max(0f, item.View.localPosition.z - depthBendStart);
                        float yaw = Mathf.Atan(2f * depthBendStrength * distance) * Mathf.Rad2Deg;
                        item.View.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
                if (activeLetter >= 0)
                {
                    BendPickup(letterPickup.transform, letterLaneX);
                    if (letterBeam != null && letterBeam.enabled)
                        BendPickup(letterBeam.transform, letterLaneX + settings.LetterBeamBaseOffset.x, letterPickup.transform.localPosition.z);
                }
                refreshDepthBend = depthBendStrength != 0f;
            }
            if (gemFlash == null || !gemFlash.enabled) return;
            float progress = (Time.unscaledTime - gemFlashStarted) / activeFlash.Duration;
            if (progress >= 1f)
            {
                gemFlash.enabled = false;
                return;
            }
            float peak = activeFlash.GrowthFraction;
            float size = activeFlash.Size;
            float scale = Mathf.SmoothStep(activeFlash.StartScale * size, size, progress / peak);
            float maxOpacity = activeFlash.Opacity;
            float opacity = progress < peak
                ? Mathf.SmoothStep(0f, maxOpacity, progress / peak)
                : Mathf.SmoothStep(maxOpacity, 0f, (progress - peak) / (1f - peak));
            gemFlash.color = new Color(1f, 1f, 1f, opacity);
            gemFlash.transform.localScale = Vector3.one * scale;
            gemFlash.transform.rotation = worldCamera.transform.rotation * gemFlashRoll;
        }

        private void BendPickup(Transform visual, float laneX, float? referenceZ = null)
        {
            Vector3 position = visual.localPosition;
            float distance = Mathf.Max(0f, (referenceZ ?? position.z) - depthBendStart);
            position.x = laneX + depthBendStrength * distance * distance;
            visual.localPosition = position;
        }
        private void Hide(TrackItem item)
        {
            if (item.Active && item.Definition != null && item.Definition.RideableRoof)
                for (int i = vehicleRoofs.Count - 1; i >= 0; i--)
                    if (vehicleRoofs[i].Vehicle == item) vehicleRoofs.RemoveAt(i);
            item.Active = false;
            item.View.gameObject.SetActive(false);
        }
        private void SetFieldOfView(float reference)
        {
            referenceFieldOfView = reference;
            float scale = Mathf.Max(1, (9f / 16f) / Mathf.Max(.1f, worldCamera.aspect));
            worldCamera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(reference * Mathf.Deg2Rad * .5f) * scale) * Mathf.Rad2Deg;
        }
    }
}
