using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lotba.Game.Editor
{
    /// <summary>Creates the game entry scene. Existing scenes are opened rather than overwritten.</summary>
    public static class GameSceneBuilder
    {
        public const string ScenePath = PrototypeArt.Root + "/Scenes/Game.unity";
        private static readonly Color Ink = new Color32(17, 43, 61, 245);
        private static readonly Color White = new Color32(249, 250, 242, 255);
        private static readonly Color Gold = new Color32(255, 196, 61, 255);
        private static readonly Color Green = new Color32(49, 192, 107, 255);
        private static TMPro.TMP_FontAsset font;
        private static Sprite round, circle;

        [MenuItem("LOTBA/Open game")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before opening the game.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before opening the game.");
            if (System.IO.File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Build();
        }
        private static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var art = new PrototypeArt(); art.Create();
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .7f, .75f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.57f, .77f, .84f); RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 130;
            var camera = new GameObject("City chase camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 4.25f, -7.6f);
            camera.transform.LookAt(new Vector3(0, 1.1f, 8.8f)); camera.fieldOfView = 60;
            camera.nearClipPlane = .15f; camera.farClipPlane = 155; camera.allowHDR = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.35f, .72f, .91f);
            var sunlight = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sunlight.type = LightType.Directional; sunlight.intensity = .9f; sunlight.shadows = LightShadows.None;
            sunlight.transform.rotation = Quaternion.Euler(48, -25, 0);
            var world = new GameObject("Game world").AddComponent<RunnerWorld>();
            art.Primitive("Ground under city", PrimitiveType.Cube, world.transform, new Vector3(0, -.3f, 62), new Vector3(90, .25f, 190), art.Pavement);
            for (int i = 0; i < 7; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                art.Primitive("Distant skyline", PrimitiveType.Cube, world.transform, new Vector3(side * (11 + i * 4), 8 + i % 3 * 2, 133), new Vector3(8, 16 + i % 3 * 4, 8), art.Glass);
                art.Primitive("Cloud", PrimitiveType.Sphere, world.transform, new Vector3(-28 + i * 9, 18 + i % 3 * 3, 91 + i % 2 * 15), new Vector3(10, 2.5f, 4), art.White);
            }
            Transform rider = art.Rider(world.transform, out Transform[] wheels, out Transform[] pedals);
            Transform shadow = art.MeshObject("Cyclist footprint", world.transform, art.Footprint(), new Vector3(0, .025f, 0), new Vector3(1.1f, 1, 1.8f), art.Shadow).transform;
            var particles = new GameObject("Pickup and impact sparks", typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            particles.transform.SetParent(world.transform, false); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = false; main.duration = 1;
            main.startLifetime = .3f; main.startSpeed = 2.8f; main.startSize = .11f; main.gravityModifier = .7f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 128;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .25f;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>(); particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = art.WheelMesh; particleRenderer.sharedMaterial = art.Gold;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off; particleRenderer.receiveShadows = false;
            Bind(world, "rider", rider); Bind(world, "riderShadow", shadow); Bind(world, "worldCamera", camera);
            Bind(world, "coinPrefab", art.Coin); Bind(world, "sparks", particles);
            BindArray(world, "wheels", wheels); BindArray(world, "pedals", pedals);
            var profiles = MapAssetSetup.EnsureProfiles(art); ObstacleAssetSetup.EnsureAssets(art);
            MapAssetSetup.ConfigureWorld(world, camera, profiles.Obelisco);
            GameHud hud = BuildUi(camera);
            var events = new GameObject("Game input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
            var composition = new GameObject("Game composition", typeof(AudioSource), typeof(AudioFeedback), typeof(DeviceFeedbackAndInput), typeof(GameController));
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(PrototypeArt.Root + "/Settings/GameSettings.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<GameSettings>(); AssetDatabase.CreateAsset(settings, PrototypeArt.Root + "/Settings/GameSettings.asset"); }
            var controller = composition.GetComponent<GameController>();
            Bind(controller, "settings", settings); Bind(controller, "world", world); Bind(controller, "hud", hud); Bind(controller, "audioFeedback", composition.GetComponent<AudioFeedback>()); Bind(controller, "devices", composition.GetComponent<DeviceFeedbackAndInput>());
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Debug.Log("LOTBA: Game scene saved. No Play Mode, tests, or WebGL build executed.");
        }
        private static GameHud BuildUi(Camera camera)
        {
            font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            round = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_LOTBA_Game/Art/UI/Rounded panel.png"); circle = PrototypeArt.CircleSprite();
            if (font == null || round == null || circle == null) throw new InvalidOperationException("The UI font or sprites are missing.");
            var root = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(GameHud), typeof(PortraitViewport));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960); scaler.matchWidthOrHeight = .5f;
            var hud = root.GetComponent<GameHud>(); var safe = Rect("Portrait safe area", root.transform, 0, 0, 1, 1);
            Bind(root.GetComponent<PortraitViewport>(), "worldCamera", camera); Bind(root.GetComponent<PortraitViewport>(), "safeArea", safe);
            var lines = Rect("Peripheral speed lines", safe, 0, .17f, 1, 1).gameObject.AddComponent<SpeedLines>(); lines.raycastTarget = false; Bind(hud, "speedLines", lines);
            var race = Rect("Race HUD", safe, 0, 0, 1, 1); Bind(hud, "raceHud", race.gameObject);
            Bind(hud, "pause", Button("Pause", race, "II", .025f, .918f, .145f, .985f, new Color(.12f, .35f, .46f, .7f), 29, true));
            Bind(hud, "score", Label("Score", race, "0", .27f, .923f, .73f, .988f, 43, White, true));
            var coinBacking = Panel("Coin counter", race, .765f, .927f, .985f, .977f, new Color(.12f, .35f, .46f, .75f));
            Panel("Coin symbol", coinBacking, .66f, .13f, .89f, .87f, Gold, true);
            Bind(hud, "coins", Label("Coins", coinBacking, "0", .06f, .05f, .6f, .95f, 24, White));
            Bind(hud, "remaining", Label("Time left", race, "120 s", .79f, .89f, .98f, .923f, 16, White));
            var combo = Panel("Streak track", race, .31f, .864f, .7f, .891f, new Color(.12f, .35f, .46f, .7f));
            var fill = Panel("Streak fill", combo, .025f, .15f, .975f, .85f, Gold).GetComponent<UnityEngine.UI.Image>();
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0; Bind(hud, "streakFill", fill);
            var badge = Panel("Multiplier badge", race, .665f, .83f, .81f, .912f, new Color(1, .65f, .10f), true);
            Bind(hud, "multiplier", Label("Multiplier", badge, "×1", .02f, .02f, .98f, .98f, 32, White, true));
            Bind(hud, "streak", Label("Streak", race, "RACHA 0", .3f, .816f, .68f, .858f, 22, White, true));
            Bind(hud, "popup", Label("Pickup feedback", race, "", .18f, .53f, .82f, .65f, 45, Gold, true));
            var ready = Panel("Ready", safe, .065f, .26f, .935f, .79f, Ink); Bind(hud, "readyPanel", ready.gameObject);
            Bind(hud, "gameTitle", Label("Title", ready, GameIdentity.Title, .07f, .64f, .93f, .97f, 62, White));
            Bind(hud, "developmentLabel", Label("Edition", ready, GameIdentity.DevelopmentLabel, .06f, .57f, .94f, .64f, 17, Gold));
            Label("Instructions", ready, "Tres carriles. Seguí las monedas.\nSaltá los conos, esquivá los autos.\nFrená cuando vos quieras.", .07f, .33f, .93f, .55f, 22, White);
            Bind(hud, "start", Button("Start", ready, "EMPEZAR", .07f, .17f, .93f, .30f, Green, 27));

            var pausePanel = Panel("Pause panel", safe, .1f, .37f, .9f, .72f, Ink); Bind(hud, "pausePanel", pausePanel.gameObject);
            Label("Pause heading", pausePanel, "PAUSA", .08f, .72f, .92f, .94f, 39, White);
            Label("Pause copy", pausePanel, "Retomá a tu ritmo o frená abajo.", .06f, .61f, .94f, .73f, 18, White);
            Bind(hud, "resume", Button("Resume", pausePanel, "CONTINUAR", .08f, .35f, .92f, .57f, Green, 26));

            var result = Panel("Results", safe, .065f, .2f, .935f, .85f, Ink); Bind(hud, "resultPanel", result.gameObject);
            Bind(hud, "resultTitle", Label("Result heading", result, "", .07f, .81f, .93f, .97f, 38, White));
            Bind(hud, "resultStats", Label("Result stats", result, "", .07f, .245f, .93f, .79f, 25, White));
            Bind(hud, "retry", Button("Retry", result, "REINTENTAR", .07f, .13f, .93f, .225f, Green, 25));
            Bind(hud, "menu", Button("Return to menu", result, "VOLVER AL INICIO", .07f, .025f, .93f, .11f, new Color(.16f, .32f, .41f), 22));

            // Controls are above the pause layer. Braking never requires dismissing a modal.
            var controls = Rect("Touch controls", safe, .025f, .015f, .975f, .255f); Bind(hud, "controls", controls.gameObject);
            var dialRoot = Rect("Speed dial", controls, .3f, 0, .72f, .7f);
            var gauge = dialRoot.gameObject.AddComponent<SpeedGauge>(); gauge.raycastTarget = false; Bind(hud, "gauge", gauge);
            Bind(hud, "speed", Label("Speed number", dialRoot, "65", .18f, .12f, .82f, .48f, 41, White));
            Label("Speed units", dialRoot, "km/h", .24f, .005f, .76f, .15f, 16, White);
            Bind(hud, "brake", Button("Brake", controls, "FRENAR", .76f, .17f, .99f, .65f, new Color(.3f, .38f, .43f, .96f), 20, true));
            Bind(hud, "identityFooter", Label("Edition label", safe, GameIdentity.FooterLabel, .035f, .001f, .31f, .019f, 10, White));
            foreach (var hidden in new[] { race, controls, pausePanel, result }) hidden.gameObject.SetActive(false);
            GameUiSetup.Apply(hud, camera);
            return hud;
        }
        internal static void PrepareUiAssets()
        {
            font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            round = AssetDatabase.LoadAssetAtPath<Sprite>(PrototypeArt.Root + "/Art/UI/Rounded panel.png");
            circle = PrototypeArt.CircleSprite();
        }
        internal static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        internal static RectTransform Panel(string name, Transform parent, float x0, float y0, float x1, float y1, Color colour, bool circular = false)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = colour; image.sprite = circular ? circle : round; image.type = circular ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget = false; return rect;
        }
        internal static TMPro.TMP_Text Label(string name, Transform parent, string value, float x0, float y0, float x1, float y1, int size, Color colour, bool shadow = false)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1); var label = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = font; label.text = value; label.fontSize = size; label.fontStyle = TMPro.FontStyles.Bold;
            label.color = colour; label.alignment = TMPro.TextAlignmentOptions.Center;
            label.textWrappingMode = TMPro.TextWrappingModes.Normal; label.overflowMode = TMPro.TextOverflowModes.Ellipsis; label.raycastTarget = false;
            if (shadow) { var effect = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>(); effect.effectColor = new Color(0, .1f, .15f, .35f); effect.effectDistance = new Vector2(1, -2); }
            return label;
        }
        internal static UnityEngine.UI.Button Button(string name, Transform parent, string caption, float x0, float y0, float x1, float y1, Color colour, int size, bool circular = false)
        {
            var rect = Panel(name, parent, x0, y0, x1, y1, colour, circular); var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Label("Label", rect, caption, .025f, .04f, .975f, .96f, size, White); return button;
        }
        internal static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Unknown binding: " + target.name + "." + name);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        internal static void BindArray(UnityEngine.Object target, string name, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(name); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
