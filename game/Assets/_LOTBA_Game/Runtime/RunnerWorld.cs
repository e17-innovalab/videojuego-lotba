using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Pooled runner presentation and swept encounters. Scenery never decides run state.</summary>
    public sealed class RunnerWorld : MonoBehaviour
    {
        public const float LaneSpacing = RoadStandard.LaneSpacing;
        [SerializeField] private Transform rider;
        [SerializeField] private Transform[] wheels;
        [SerializeField] private Transform[] pedals;
        [SerializeField] private RoadStreamer roadStream;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Transform riderShadow;
        private readonly JumpArc jump = new JumpArc();
        private readonly List<TrackItem> items = new List<TrackItem>(256);
        private readonly List<Crossing> crossings = new List<Crossing>(16);
        private Quaternion[] wheelOrigins;
        private Vector3 cameraOrigin;
        private Quaternion cameraRotation;
        private Vector3 cameraVelocity;
        private ObstacleSet obstacleOverride;
        private readonly HashSet<ObstacleDefinition> registeredObstacles = new HashSet<ObstacleDefinition>();
        private GameSettings settings;
        private System.Random random;
        private float spawnTimer;
        private float laneTime;
        private float laneOrigin;
        private int visualLane = 1;
        private int safeLane = 1;
        private int wave;
        private float pedalAngle;
        public event Action<long, bool> EncounterFeedback;
        private enum ItemKind { Coin, Obstacle }
        private sealed class TrackItem
        {
            public Transform View;
            public ItemKind Kind;
            public ObstacleDefinition Definition;
            public bool ObstacleHit;
            public bool Active;
            public bool Collecting;
            public float CollectTime;
            public Vector3 Scale;
            public Vector3 PickupOrigin;
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
            cameraOrigin = worldCamera.transform.localPosition;
            cameraRotation = worldCamera.transform.localRotation;
            roadStream.Initialize();
            wheelOrigins = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) wheelOrigins[i] = wheels[i].localRotation;
            for (int i = 0; i < 72; i++) AddToPool(coinPrefab, ItemKind.Coin);
            foreach (var section in roadStream.Route.Sections) WarmSet(section.Map.Obstacles);
        }
        public void ResetTrack()
        {
            random = new System.Random(settings.Seed);
            foreach (TrackItem item in items) Hide(item);
            jump.Reset(); spawnTimer = 0; wave = 0; safeLane = visualLane = 1;
            laneOrigin = laneTime = pedalAngle = 0;
            rider.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            riderShadow.localPosition = new Vector3(0, .025f, 0);
            roadStream.ResetRoad();
            for (int i = 0; i < wheels.Length; i++) wheels[i].localRotation = wheelOrigins[i];
            foreach (Transform pedal in pedals) pedal.localRotation = Quaternion.identity;
            cameraVelocity = Vector3.zero;
            worldCamera.transform.SetLocalPositionAndRotation(cameraOrigin, cameraRotation);
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < 4; i++) Place(ItemKind.Coin, 1, 10 + i * 3, 1.1f);
            SetFieldOfView(60f);
        }
        public bool TryJump(float time) => jump.TryStart(time);
        public void Advance(RunSession session, float dt, bool reducedMotion)
        {
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
            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                SpawnPattern(session);
                spawnTimer += Mathf.Lerp(settings.InitialPatternInterval, settings.FinalPatternInterval, Mathf.Clamp01(session.Elapsed / 45f));
            }
            crossings.Clear();
            foreach (TrackItem item in items)
            {
                if (!item.Active) continue;
                if (item.Collecting)
                {
                    item.CollectTime += dt;
                    float t = Mathf.Clamp01(item.CollectTime / .32f);
                    float ease = 1 - (1 - t) * (1 - t);
                    item.View.localPosition = item.PickupOrigin + Vector3.up * (ease * 1.65f);
                    item.View.localScale = item.Scale * (1 - t * t);
                    if (t >= 1) Hide(item);
                    continue;
                }
                float previousZ = item.View.localPosition.z;
                item.View.localPosition += Vector3.back * travel;
                if (item.Kind == ItemKind.Coin) item.View.Rotate(Vector3.up, 145 * dt, Space.World);
                if (item.Kind == ItemKind.Coin)
                {
                    if (previousZ >= 0 && item.View.localPosition.z <= 0)
                        crossings.Add(new Crossing(item, travel > 0 ? Mathf.Clamp01(previousZ / travel) : 1));
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
                bool sameLane = Mathf.Abs(item.View.localPosition.x - crossingX) < .83f;
                if (sameLane && item.Kind == ItemKind.Coin && JumpArc.CanCollect(crossingHeight, item.View.localPosition.y))
                {
                    long reward = session.CollectCoin();
                    item.Collecting = true; item.CollectTime = 0; item.PickupOrigin = item.View.localPosition;
                    Emit(false); EncounterFeedback?.Invoke(reward, false);
                }
                else if (item.Kind == ItemKind.Obstacle && !session.IsProtected)
                {
                    item.ObstacleHit = true;
                    long loss = session.Hit(); Emit(true); EncounterFeedback?.Invoke(-loss, true);
                }
                if (item.Kind == ItemKind.Coin && !item.Collecting) Hide(item);
            }
            foreach (TrackItem item in items)
            {
                float behind = item.Definition != null ? -item.Definition.Length * .5f - 9 : -8;
                if (item.Active && item.View.localPosition.z < behind) Hide(item);
            }
            float intensity = Mathf.InverseLerp(settings.InitialSpeed, settings.MaximumSpeed, session.Speed);
            Vector3 cameraTarget = cameraOrigin + (reducedMotion ? Vector3.zero : Vector3.right * (x * settings.CameraFollowStrength));
            worldCamera.transform.localPosition = Vector3.SmoothDamp(worldCamera.transform.localPosition, cameraTarget, ref cameraVelocity, settings.CameraFollowTime, Mathf.Infinity, dt);
            SetFieldOfView(reducedMotion ? 60f : Mathf.Lerp(60f, 68f, intensity));
        }
        private void SpawnPattern(RunSession session)
        {
            safeLane = Mathf.Clamp(safeLane + random.Next(-1, 2), 0, 2);
            float z = Mathf.Max(58, session.Speed * settings.PreviewTime);
            float spacing = Mathf.Clamp(session.Speed * .125f, 2.2f, 3.5f);
            bool gentle = roadStream.Route.NeedsGentlePatterns(session.Distance + z - 6, session.Distance + z + spacing * 3 + 6);
            bool jumping = !gentle && session.Elapsed > 5 && wave % 4 == 2;
            for (int i = 0; i < 4; i++)
                Place(ItemKind.Coin, safeLane, z + i * spacing, jumping ? (i == 0 || i == 3 ? 2.05f : 2.7f) : 1.1f);
            ObstacleSet obstacleSet = obstacleOverride != null ? obstacleOverride : roadStream.Route.MapAtDistance(session.Distance + z).Obstacles;
            float maximumLength = gentle || session.Elapsed < 8 ? 4.5f : float.PositiveInfinity;
            if (jumping) PlaceObstacle(obstacleSet, false, safeLane, z + spacing * 1.5f, maximumLength);
            if (session.Elapsed > 2)
            {
                int blockedLane = (safeLane + 1 + random.Next(2)) % 3;
                PlaceObstacle(obstacleSet, wave % 3 == 0, blockedLane, z + spacing * 1.5f, maximumLength);
                if (!gentle && !jumping && session.Elapsed > 12 && wave % 3 == 1)
                {
                    int otherLane = 3 - safeLane - blockedLane;
                    PlaceObstacle(obstacleSet, true, otherLane, z + spacing * 1.5f, maximumLength);
                }
            }
            // A jump row blocks at most two lanes; every other row keeps the coin lane clear.
            wave++;
        }
        private void AddToPool(GameObject prefab, ItemKind kind, ObstacleDefinition definition = null)
        {
            GameObject instance = Instantiate(prefab, transform); instance.SetActive(false);
            if (definition != null) instance.GetComponent<ObstacleView>().Apply(definition);
            items.Add(new TrackItem { View = instance.transform, Kind = kind, Definition = definition, Scale = instance.transform.localScale });
        }
        private void Place(ItemKind kind, int lane, float z, float height, ObstacleDefinition definition = null)
        {
            // Rotate the free list to avoid always showing the same prefab variation.
            int start = random.Next(items.Count);
            for (int offset = 0; offset < items.Count; offset++)
            {
                TrackItem item = items[(start + offset) % items.Count];
                if (item.Active || item.Kind != kind || item.Definition != definition) continue;
                item.Active = true; item.Collecting = item.ObstacleHit = false; item.View.localScale = item.Scale;
                item.View.SetLocalPositionAndRotation(new Vector3((lane - 1) * LaneSpacing, kind == ItemKind.Coin ? height : 0, z), Quaternion.identity);
                item.View.gameObject.SetActive(true); return;
            }
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
        private void PlaceObstacle(ObstacleSet set, bool vehicle, int lane, float z, float maximumLength)
        {
            if (set == null) return;
            ObstacleDefinition definition = set.Pick(random, vehicle, maximumLength, vehicle && wave % 3 == 0 && float.IsPositiveInfinity(maximumLength));
            if (definition != null) Place(ItemKind.Obstacle, lane, z + definition.Length * .5f + .6f, 0, definition);
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
            float minimumX = item.View.localPosition.x - halfWidth, maximumX = item.View.localPosition.x + halfWidth;
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
        private void Emit(bool impact)
        {
            var emission = new ParticleSystem.EmitParams { position = rider.position + Vector3.up * 1.15f, startColor = impact ? new Color(1, .35f, .15f) : new Color(1, .86f, .25f) };
            sparks.Emit(emission, impact ? 14 : 7);
        }
        private static void Hide(TrackItem item) { item.Active = item.Collecting = false; item.View.localScale = item.Scale; item.View.gameObject.SetActive(false); }
        private void SetFieldOfView(float reference)
        {
            float scale = Mathf.Max(1, (9f / 16f) / Mathf.Max(.1f, worldCamera.aspect));
            worldCamera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(reference * Mathf.Deg2Rad * .5f) * scale) * Mathf.Rad2Deg;
        }
    }
}
