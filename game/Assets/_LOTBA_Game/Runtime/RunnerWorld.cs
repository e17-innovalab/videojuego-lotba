using System;
using System.Collections.Generic;
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
        private float riderDistance;
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
        [Header("Word collection")]
        [SerializeField] private bool wordCollectionEnabled;
        [SerializeField] private string[] collectibleWords = { "CICLISMO", "BUENOS AIRES" };
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
        public event Action<string, bool[], bool> LetterCollected;
        private readonly JumpArc jump = new JumpArc();
        private readonly List<TrackItem> items = new List<TrackItem>(256);
        private readonly List<Crossing> crossings = new List<Crossing>(16);
        private Quaternion[] wheelOrigins;
        private Vector3 cameraOrigin;
        private Quaternion cameraRotation;
        private Vector3 cameraVelocity;
        private float boostCameraBlend;
        private bool pickupEffectsEnabled = true;
        private readonly Transform[] scatteredGems = new Transform[RunSession.BoostImpactGemLoss];
        private Vector3 gemScatterOrigin;
        private float gemScatterTime;
        private int scatteredGemCount;
        private TurboAuraController turboAura;
        private float referenceFieldOfView;
        private Renderer[] riderRenderers;
        private Material riderImpactMaterial;
        private Material[][] riderMaterials;
        private Material[][] riderWhiteMaterials;
        private bool riderImpactWhite;
        private ObstacleSet obstacleOverride;
        private readonly HashSet<ObstacleDefinition> registeredObstacles = new HashSet<ObstacleDefinition>();
        private GameSettings settings;
        private System.Random random;
        private const float FirstPatternDistance = 58f;
        private float nextPatternDistance;
        private float lastPatternEnd;
        private float laneTime;
        private float laneOrigin;
        private int visualLane = 1;
        private int safeLane = 1;
        private int wave;
        private float pedalAngle;
#if UNITY_EDITOR
        private bool previewGems = true, previewObstacles = true, previewInvulnerable;
        public void ConfigureArtPreview(bool gems, bool obstacles, bool showRider)
        {
            bool visibilityChanged = gems != previewGems || obstacles != previewObstacles;
            previewGems = gems; previewObstacles = obstacles; previewInvulnerable = true;
            rider.gameObject.SetActive(showRider); riderShadow.gameObject.SetActive(showRider);
            if (visibilityChanged)
                foreach (TrackItem item in items)
                    item.View.gameObject.SetActive(item.Active && (item.Kind == ItemKind.Gem ? gems : obstacles));
        }
#endif
        public void ConfigureDepthBend(bool enabled) => depthBendRequested = enabled;
        public void ConfigurePickupEffects(bool enabled)
        {
            pickupEffectsEnabled = enabled;
            if (enabled) return;
            ClearGemScatter();
            if (gemFlash != null) gemFlash.enabled = false;
            if (letterBeam != null) letterBeam.enabled = false;
            UpdateLetterMotes(null, 0f);
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
        public event Action<int> GemFeedback;
        public event Action<bool> ImpactFeedback;
        public RouteDefinition Route => roadStream.Route;
        public event Action StreakLevelUp;
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
            if (settings.GemFlash.Sprite == null)
                Debug.LogWarning("GameSettings has no Gem Flash Sprite assigned.", this);
            cameraOrigin = worldCamera.transform.localPosition;
            cameraRotation = worldCamera.transform.localRotation;
            riderRenderers = rider.GetComponentsInChildren<Renderer>(true);
            if (settings.RiderImpactShader != null)
            {
                riderImpactMaterial = new Material(settings.RiderImpactShader);
                riderMaterials = new Material[riderRenderers.Length][];
                riderWhiteMaterials = new Material[riderRenderers.Length][];
                for (int i = 0; i < riderRenderers.Length; i++)
                {
                    riderMaterials[i] = riderRenderers[i].sharedMaterials;
                    riderWhiteMaterials[i] = new Material[riderMaterials[i].Length];
                    for (int slot = 0; slot < riderWhiteMaterials[i].Length; slot++)
                        riderWhiteMaterials[i][slot] = riderImpactMaterial;
                }
            }
            roadStream.Initialize(settings.DepthBendShader);
            wheelOrigins = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) wheelOrigins[i] = wheels[i].localRotation;
            for (int i = 0; i < 72; i++) AddToPool(gemPrefab, ItemKind.Gem);
            foreach (var section in roadStream.Route.Sections) WarmSet(section.Map.Obstacles);
        }
        public void ResetTrack()
        {
            turboAura?.Clear();
            ClearGemScatter();
            riderDistance = 0f;
            ClearImpactBlink();
            random = new System.Random(settings.Seed);
            foreach (TrackItem item in items) Hide(item);
            jump.Reset(); nextPatternDistance = FirstPatternDistance; wave = 0; safeLane = visualLane = 1;
            lastPatternEnd = 19f; // End of the opening gem row.
            ResetLetters();
            laneOrigin = laneTime = pedalAngle = 0;
            rider.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            riderShadow.localPosition = new Vector3(0, .025f, 0);
            roadStream.ResetRoad();
            for (int i = 0; i < wheels.Length; i++) wheels[i].localRotation = wheelOrigins[i];
            foreach (Transform pedal in pedals) pedal.localRotation = Quaternion.identity;
            cameraVelocity = Vector3.zero;
            boostCameraBlend = 0f;
            worldCamera.transform.SetLocalPositionAndRotation(cameraOrigin, cameraRotation);
            if (gemFlash != null) gemFlash.enabled = false;
            for (int i = 0; i < 4; i++) Place(ItemKind.Gem, 1, 10 + i * 3, 1.1f);
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

        private void ScatterGems(int count)
        {
            ClearGemScatter();
            if (!pickupEffectsEnabled) return;
            gemScatterOrigin = rider.localPosition + Vector3.up * .9f;
            gemScatterTime = 0f;
            for (int i = 0; i < count; i++)
            {
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
            float height = 1.4f * Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI)) * (1f - t);
            for (int i = 0; i < scatteredGemCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 offset = new Vector3(side * (1.4f + i * .28f) * t, height - .7f * t, (.7f - i * .58f) * t);
                scatteredGems[i].SetLocalPositionAndRotation(gemScatterOrigin + offset, Quaternion.Euler(0f, t * 540f, side * t * 85f));
            }
        }

        public bool TryJump(float time) => jump.TryStart(time);
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
            }
            laneTime += dt;
            float targetX = (visualLane - 1) * LaneSpacing;
            float blend = Mathf.Clamp01(laneTime / settings.LaneChangeTime);
            // Smoothstep has a short fixed duration and can be redirected during a transition.
            float x = Mathf.Lerp(laneOrigin, targetX, blend * blend * (3 - 2 * blend));
            float height = jump.HeightAt(session.Elapsed);
            rider.localPosition = new Vector3(x, height, 0);
            riderShadow.localPosition = new Vector3(x, .025f, 0);
            riderShadow.localScale = new Vector3(1.1f, 1, 1.8f) * (1 - height * .12f);
            float lean = reducedMotion ? 0 : Mathf.Clamp((oldX - x) / Mathf.Max(.001f, dt) * .55f, -17, 17);
            Quaternion pose = Quaternion.Euler(reducedMotion ? 0 : -height * 4, 0, lean);
            rider.localRotation = Quaternion.Slerp(rider.localRotation, pose, 18 * dt);
            float travel = session.Speed * dt;
            foreach (Transform wheel in wheels) wheel.Rotate(Vector3.right, travel * Mathf.Rad2Deg / .45f, Space.Self);
            pedalAngle += travel * 35;
            for (int i = 0; i < pedals.Length; i++) pedals[i].localRotation = Quaternion.Euler(Mathf.Sin((pedalAngle + i * 180) * Mathf.Deg2Rad) * 22, 0, 0);
            roadStream.Advance(session.Distance);
            crossings.Clear();
            foreach (TrackItem item in items)
            {
                if (!item.Active) continue;
                float previousZ = item.View.localPosition.z;
                item.View.localPosition += Vector3.back * travel;
                if (item.ImpactTime.HasValue && item.Definition.Jumpable)
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
                    if (previousZ >= 0 && item.View.localPosition.z < 0)
                        crossings.Add(new Crossing(item, Mathf.Clamp01(previousZ / travel)));
                }
                else if (!item.ObstacleHit && TryObstacleContact(item, previousZ, travel, oldX, x, session.Elapsed, dt, out float fraction))
                    crossings.Add(new Crossing(item, fraction));
            }
            crossings.Sort(CrossingOrder);
            foreach (Crossing crossing in crossings)
            {
                if (session.State != RunState.Running) break;
                TrackItem item = crossing.Item;
                float fraction = crossing.Fraction;
                float crossingX = Mathf.Lerp(oldX, x, fraction);
                float crossingHeight = jump.HeightAt(session.Elapsed - dt + dt * fraction);
                bool sameLane = Mathf.Abs(item.LaneX - crossingX) < .83f;
                if (sameLane && item.Kind == ItemKind.Gem && JumpArc.CanCollect(crossingHeight, item.View.localPosition.y))
                {
                    int previousMultiplier = session.Multiplier;
                    session.CollectGem(item.AirborneNote);
                    if (item.View.gameObject.activeInHierarchy)
                        EmitPickupFlash(transform.TransformPoint(new Vector3(item.LaneX, item.View.localPosition.y, 0)), settings.GemFlash);
                    Hide(item);
                    GemFeedback?.Invoke(item.AirborneNote);
                    if (session.Multiplier > previousMultiplier) StreakLevelUp?.Invoke();
                }
                else if (item.Kind == ItemKind.Obstacle
#if UNITY_EDITOR
                    && !previewInvulnerable
#endif
                )
                {
                    item.ObstacleHit = true;
                    // Vehicles stay clear of the camera after contact. Small obstacles tumble aside.
                    if (!item.Definition.Jumpable)
                        Hide(item);
                    if (session.IsProtected) continue;
                    item.ImpactTime = session.Elapsed;
                    bool duringBoost = session.BoostActive;
                    int previousGemsLost = session.GemsLost;
                    session.Hit(item.Definition.Id);
                    int gemsLost = session.GemsLost - previousGemsLost;
                    if (duringBoost)
                    {
                        turboAura?.Clear();
                        var position = worldCamera.transform.localPosition;
                        position.z = cameraOrigin.z;
                        worldCamera.transform.localPosition = position;
                        cameraVelocity.z = 0f;
                        SetFieldOfView(Mathf.Max(60f, referenceFieldOfView - settings.BoostCameraFieldOfView * boostCameraBlend));
                        boostCameraBlend = 0f;
                        ScatterGems(gemsLost);
                    }
                    ImpactFeedback?.Invoke(duringBoost);
                }
            }
            foreach (TrackItem item in items)
            {
                float behind = item.Definition != null ? -item.Definition.Length * .5f - 9 : -8;
                if (item.Active && item.View.localPosition.z < behind) Hide(item);
            }
            // Speed changes the reveal horizon while the route positions stay fixed.
            float lookAhead = Mathf.Max(settings.MinimumPatternLookAhead, session.Speed * settings.PreviewTime);
            if (settings.PreparePatternsBeyondVisibility)
                lookAhead = Mathf.Max(lookAhead, roadStream.VisibilityDistance + settings.PatternVisibilityMargin);
            float spawnHorizon = session.Distance + lookAhead;
            AdvanceLetter(travel, oldX, x, session, dt);
            float clearance = Mathf.Max(0f, letterClearance);
            while (true)
            {
                float letterDistance = Mathf.Max(nextLetterDistance, lastPatternEnd + clearance);
                if (nextLetterDistance <= nextPatternDistance && CanPlaceLetter(letterDistance, clearance))
                {
                    if (letterDistance > spawnHorizon) break;
                    if (TryPlaceLetter(letterDistance, session.Distance))
                        nextPatternDistance = Mathf.Max(nextPatternDistance, letterDistance + clearance);
                }
                if (nextPatternDistance > spawnHorizon) break;

                if (session.Telemetry != null && session.OfferDistance == 0f &&
                    nextPatternDistance >= session.NextDecisionDistance)
                {
                    session.ScheduleSpeedOffer(nextPatternDistance + 4f);
                    nextPatternDistance += 16f;
                    continue;
                }
                lastPatternEnd = SpawnPattern(nextPatternDistance, session.Distance);
                nextPatternDistance = Mathf.Max(nextPatternDistance + settings.PatternSpacingAt(nextPatternDistance),
                    lastPatternEnd + settings.PatternClearance);
            }
            session.ShowSpeedOffer();
            float protection = session.ProtectionProgress;
            // Keep the pulses brief and under three per second.
            float blinkPhase = protection * 3f;
            SetRiderImpactWhite(!reducedMotion && session.IsProtected && Mathf.Repeat(blinkPhase, 1f) < .14f);
            float boostTarget = session.BoostActive && !reducedMotion ? 1f : 0f;
            boostCameraBlend = reducedMotion || !boostCamera ? 0f :
                Mathf.Lerp(boostCameraBlend, boostTarget, 1f - Mathf.Exp(-settings.BoostCameraBlendRate * dt));
            FollowRider(x, reducedMotion, dt);
            if (session.IsProtected && !reducedMotion) return;
            float pace = session.Speed - (session.BoostActive ? RunSession.BoostSpeed : 0f);
            float intensity = Mathf.InverseLerp(settings.InitialSpeed, settings.MaximumSpeed, pace);
            float targetFieldOfView = reducedMotion ? 60f : Mathf.Lerp(60f, 68f, intensity) +
                settings.BoostCameraFieldOfView * boostCameraBlend;
            SetFieldOfView(Mathf.Lerp(referenceFieldOfView, targetFieldOfView, 1f - Mathf.Exp(-10f * dt)));
        }

        private void FollowRider(float x, bool reducedMotion, float dt)
        {
            Vector3 cameraTarget = cameraOrigin + (reducedMotion ? Vector3.zero : Vector3.right * (x * settings.CameraFollowStrength));
            cameraTarget += Vector3.back * (settings.BoostCameraPullback * boostCameraBlend);
            worldCamera.transform.localPosition = Vector3.SmoothDamp(worldCamera.transform.localPosition, cameraTarget, ref cameraVelocity, settings.CameraFollowTime, Mathf.Infinity, dt);
        }

        public void ClearImpactBlink() => SetRiderImpactWhite(false);

        private void SetRiderImpactWhite(bool white)
        {
            if (riderImpactMaterial == null || riderImpactWhite == white) return;
            riderImpactWhite = white;
            Material[][] materials = white ? riderWhiteMaterials : riderMaterials;
            for (int i = 0; i < riderRenderers.Length; i++)
                riderRenderers[i].sharedMaterials = materials[i];
        }

        private void OnDestroy()
        {
            if (riderImpactMaterial != null) Destroy(riderImpactMaterial);
        }
        private float SpawnPattern(float distance, float riderDistance)
        {
            safeLane = Mathf.Clamp(safeLane + random.Next(-1, 2), 0, 2);
            float z = distance - riderDistance;
            float spacing = settings.GemSpacingAt(distance);
            float patternEnd = distance + spacing * 3;
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
                        PlaceObstacle(obstacleSet, true, otherLane, z + obstacleOffset, maximumLength));
                }
            }
            // A jump row blocks at most two lanes; every other row keeps the gem lane clear.
            wave++;
            return Mathf.Max(patternEnd, obstacleEnd);
        }

        private void ResetLetters()
        {
            if (letterPickup != null) letterPickup.gameObject.SetActive(false);
            if (letterBeam != null) letterBeam.enabled = false;
            activeLetter = -1;
            activeWord = null;
            pendingLetters.Clear();
            nextLetterDistance = 100f;
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
            !roadStream.Route.NeedsGentlePatterns(distance - clearance, distance + clearance);

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
            nextLetterDistance = distance + Mathf.Max(20f, letterInterval);
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
        private void AdvanceLetter(float travel, float oldX, float x, RunSession session, float dt)
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
            float height = jump.HeightAt(session.Elapsed - dt + dt * fraction);
            if (!wordCollectionEnabled || Mathf.Abs(letterLaneX - Mathf.Lerp(oldX, x, fraction)) >= .83f ||
                !JumpArc.CanCollect(height, LetterPickupHeight))
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
            items.Add(new TrackItem { View = instance.transform, Kind = kind, Definition = definition });
        }
        private void Place(ItemKind kind, int lane, float z, float height, ObstacleDefinition definition = null, int airborneNote = -1)
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
            item.LaneX = (lane - 1) * LaneSpacing;
            item.View.SetLocalPositionAndRotation(new Vector3(item.LaneX, kind == ItemKind.Gem ? height : 0, z), Quaternion.identity);
            bool visible = true;
#if UNITY_EDITOR
            visible = kind == ItemKind.Gem ? previewGems : previewObstacles;
#endif
            item.View.gameObject.SetActive(visible);
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
            Place(ItemKind.Obstacle, lane, z + definition.Length * .5f + .6f, 0, definition);
            // Contact starts at z and ends after the full padded collider passes.
            return definition.Length + 1.2f;
        }
        private bool TryObstacleContact(TrackItem item, float previousZ, float travel, float oldX, float newX, float elapsed, float dt, out float fraction)
        {
            fraction = 0;
            ObstacleDefinition obstacle = item.Definition;
            float front = obstacle.Length * .5f + .6f, rear = -obstacle.Length * .5f - .6f;
            float currentZ = item.View.localPosition.z;
            if (previousZ < rear || currentZ > front || travel <= 0) return false;
            float enter = Mathf.Clamp01((previousZ - front) / travel), exit = Mathf.Clamp01((previousZ - rear) / travel);
            float halfWidth = obstacle.Width * .5f + .27f;
            float minimumX = item.LaneX - halfWidth, maximumX = item.LaneX + halfWidth;
            float firstX = Mathf.Lerp(oldX, newX, enter), lastX = Mathf.Lerp(oldX, newX, exit);
            if (Mathf.Max(firstX, lastX) < minimumX || Mathf.Min(firstX, lastX) > maximumX) return false;
            fraction = enter;
            if (firstX < minimumX || firstX > maximumX)
            {
                float boundary = firstX < minimumX ? minimumX : maximumX;
                fraction = Mathf.Clamp((boundary - oldX) / (newX - oldX), enter, exit);
            }
            if (obstacle.Jumpable)
            {
                float firstHeight = jump.HeightAt(elapsed - dt + dt * fraction);
                float lastHeight = jump.HeightAt(elapsed - dt + dt * exit);
                if (Mathf.Min(firstHeight, lastHeight) >= obstacle.JumpClearance) return false;
                if (firstHeight >= obstacle.JumpClearance) fraction = exit;
            }
            // The entire vehicle body remains dangerous until its rear passes the rider.
            return true;
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

        private void LateUpdate()
        {
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
        private static void Hide(TrackItem item)
        {
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
