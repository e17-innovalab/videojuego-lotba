using UnityEngine;
using UnityEngine.Serialization;

namespace Lotba.Game
{
    /// <summary>Shared gameplay tuning values. All speeds use metres per second.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Game settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("Transiciones y recupero")]
        [SerializeField] private SectionTransitionSettings transitions = new SectionTransitionSettings();
        public SectionTransitionSettings Transitions => transitions;
        [SerializeField] private TransitionDecisionView transitionViewPrefab;
        public TransitionDecisionView TransitionViewPrefab => transitionViewPrefab;
        [SerializeField] private DecisionCountdownView decisionCountdownPrefab;
        public DecisionCountdownView DecisionCountdownPrefab => decisionCountdownPrefab;
        [SerializeField, Min(.5f), Tooltip("Segundos de cuenta regresiva después de confirmar una decisión.")]
        private float decisionCountdownSeconds = 3f;
        public float DecisionCountdownSeconds => Mathf.Max(.5f, decisionCountdownSeconds);
        [SerializeField] private RecoveryAnnouncementView recoveryAnnouncementPrefab;
        public RecoveryAnnouncementView RecoveryAnnouncementPrefab => recoveryAnnouncementPrefab;
        [SerializeField] private Transform sectionGatePrefab;
        public Transform SectionGatePrefab => sectionGatePrefab;
        [SerializeField, Min(20f), Tooltip("Distancia inicial a la que aparece el cartel de bienvenida de la primera zona.")]
        private float initialWelcomeSignDistance = 110f;
        public float InitialWelcomeSignDistance => initialWelcomeSignDistance;
        [SerializeField] private LanePatternDefinition transitionExitPattern;
        public LanePatternDefinition TransitionExitPattern => transitionExitPattern;
        [SerializeField, Min(1f)] private float initialSpeed = 18f;
        [FormerlySerializedAs("maximumSpeed")]
        [SerializeField, Min(1f), Tooltip("Objetivo de la aceleración inicial. La aceleración sostenida continúa después.")]
        private float cruiseSpeed = 30f;
        [SerializeField, Min(1f)] private float accelerationTime = 95f;
        [SerializeField, Min(0f), Tooltip("Aceleración sostenida en m/s² durante toda la partida.")]
        private float sustainedAcceleration = .02f;
        [SerializeField] private Shader riderImpactShader;
        public Shader RiderImpactShader => riderImpactShader;
        [Header("Desarrollo · frenado")]
        [SerializeField] private BrakingSettings braking = new BrakingSettings();
        public BrakingSettings Braking => braking;
        [SerializeField] private BrakeHoldEffects brakeEffectsPrefab;
        public BrakeHoldEffects BrakeEffectsPrefab => brakeEffectsPrefab;
        [Header("Turbo · reglas independientes")]
        [SerializeField, InspectorName("Turbo manual")] private TurboProfile manualTurbo = new TurboProfile();
        [SerializeField, InspectorName("Turbo de oferta gratis")] private TurboProfile speedOfferTurbo = new TurboProfile();
        [SerializeField, InspectorName("Turbo de recupero"), Tooltip("El recupero termina por distancia; la duración de este perfil no determina su final.")] private TurboProfile recoveryTurbo = new TurboProfile();
        public TurboProfile ManualTurbo => manualTurbo;
        public TurboProfile SpeedOfferTurbo => speedOfferTurbo;
        public TurboProfile RecoveryTurbo => recoveryTurbo;
        public float MaximumTurboSpeedBonus => Mathf.Max(manualTurbo.Capture().SpeedBonus,
            Mathf.Max(speedOfferTurbo.Capture().SpeedBonus, recoveryTurbo.Capture().SpeedBonus));
        [Header("Boost effects")]
        [SerializeField] private Shader turboAuraShader;
        [SerializeField] private Texture2D turboAuraTexture;
        [SerializeField, Range(0f, 1f)] private float turboAuraIntensity = .72f;
        [SerializeField, Min(.01f)] private float turboAuraFadeRate = 4f;
        [SerializeField, Min(0f)] private float boostCameraPullback = .65f;
        [SerializeField, Range(0f, 20f)] private float boostCameraFieldOfView = 5f;
        [SerializeField, Min(.01f)] private float boostCameraBlendRate = 5f;
        public Shader TurboAuraShader => turboAuraShader;
        public Texture2D TurboAuraTexture => turboAuraTexture;
        public float TurboAuraIntensity => turboAuraIntensity;
        public float TurboAuraFadeRate => turboAuraFadeRate;
        public float BoostCameraPullback => boostCameraPullback;
        public float BoostCameraFieldOfView => boostCameraFieldOfView;
        public float BoostCameraBlendRate => boostCameraBlendRate;
        [Header("Recovery · fork")]
        [SerializeField] private Transform recoveryPortalContinuePrefab;
        public Transform RecoveryPortalContinuePrefab => recoveryPortalContinuePrefab;
        [SerializeField] private Transform recoveryPortalRecoverPrefab;
        public Transform RecoveryPortalRecoverPrefab => recoveryPortalRecoverPrefab;
        [SerializeField] private Transform recoveryBarrierPrefab;
        public Transform RecoveryBarrierPrefab => recoveryBarrierPrefab;
        [Header("Recovery · front curtain")]
        [SerializeField] private bool enableRecoveryFog = false;
        public bool EnableRecoveryFog { get => enableRecoveryFog; set => enableRecoveryFog = value; }
        [SerializeField] private RecoveryCurtain recoveryCurtainPrefab;
        public RecoveryCurtain RecoveryCurtainPrefab => recoveryCurtainPrefab;
        [SerializeField] private RecoveryGemView recoveryGemPrefab;
        public RecoveryGemView RecoveryGemPrefab => recoveryGemPrefab;
        [SerializeField] private RecoveryCelebrationView recoveryCelebrationPrefab;
        public RecoveryCelebrationView RecoveryCelebrationPrefab => recoveryCelebrationPrefab;
        [SerializeField] private RecoveryMissView recoveryMissPrefab;
        public RecoveryMissView RecoveryMissPrefab => recoveryMissPrefab;
        [SerializeField, InspectorName("Dificultad de niebla")] private RecoveryFogDifficulty recoveryFogDifficulty = new RecoveryFogDifficulty();
        public RecoveryFogDifficulty RecoveryFogDifficulty => recoveryFogDifficulty;
        [SerializeField, Min(0), InspectorName("Módulos a limpiar por delante"), Tooltip("Además del módulo actual, que siempre se limpia.")]
        private int recoveryClearModulesAhead = 2;
        public int RecoveryClearModuleCount => 1 + Mathf.Max(0, recoveryClearModulesAhead);
        [SerializeField, Range(.07f, .25f)] private float laneChangeTime = .11f;
        [Header("Track patterns (metres)")]
        [SerializeField, Min(1f)] private float initialPatternSpacing = 28f;
        [SerializeField, Min(1f)] private float finalPatternSpacing = 26f;
        [SerializeField, Min(1f)] private float patternRampDistance = 1100f;
        [SerializeField, Min(.1f)] private float initialGemSpacing = 2.25f;
        [SerializeField, Min(.1f)] private float finalGemSpacing = 3.5f;
        [SerializeField, Min(0f)] private float patternClearance = 8f;
        [SerializeField, Min(2f)] private float previewTime = 3.3f;
        [Header("Pattern visibility")]
        [SerializeField, Min(0f)] private float minimumPatternLookAhead = 58f;
        [Tooltip("Prepare patterns beyond the linear fog or camera clipping distance.")]
        [SerializeField] private bool preparePatternsBeyondVisibility = true;
        [SerializeField, Min(0f)] private float patternVisibilityMargin = 20f;
        [SerializeField] private int seed = 1001;
        [Header("World bend by distance")]
        [SerializeField] private bool depthBendEnabled = true;
        [SerializeField] private Shader depthBendShader;
        [SerializeField, Min(0f)] private float depthBendStart = 25f;
        [SerializeField, Range(-.006f, .006f)] private float depthBendStrength = .004f;
        [Tooltip("Metres before a zone transition at which the visual turn starts.")]
        [SerializeField, Min(0f)] private float transitionBendLeadDistance = 80f;
        public bool DepthBendAvailable => depthBendEnabled && depthBendShader != null && depthBendShader.isSupported;
        public Shader DepthBendShader => depthBendShader;
        public float DepthBendStart => depthBendStart;
        public float DepthBendStrength => depthBendStrength;
        public float TransitionBendLeadDistance => Mathf.Max(0f, transitionBendLeadDistance);
        [Header("Pickup flashes")]
        [SerializeField] private PickupFlashSettings gemFlash = new PickupFlashSettings();
        [SerializeField] private PickupFlashSettings letterFlash = new PickupFlashSettings();
        public PickupFlashSettings GemFlash => gemFlash;
        public PickupFlashSettings LetterFlash => letterFlash;
        [Header("Letter beam")]
        [SerializeField] private Sprite letterBeamSprite;
        [SerializeField] private Sprite letterMoteSprite;
        [SerializeField] private Material letterBeamMaterial;
        [SerializeField] private Color letterBeamTopTint = new Color(1f, .91f, .65f);
        [UnityEngine.Serialization.FormerlySerializedAs("letterBeamTint")]
        [SerializeField] private Color letterBeamBottomTint = new Color(1f, .8f, .4f);
        [SerializeField] private Color letterMoteTint = new Color(1f, .91f, .65f);
        [SerializeField, Min(.01f)] private float letterMoteScaleMultiplier = 1f;
        [SerializeField] private bool letterBeamInFront;
        [SerializeField] private Vector3 letterBeamBaseOffset = new Vector3(0f, .04f, .18f);
        [SerializeField, Min(.1f)] private float letterBeamBaseScale = 5f;
        [SerializeField, Min(0f)] private float letterBeamHeightPulse = .25f;
        [SerializeField, Range(0f, 1f)] private float letterBeamMinOpacity = .85f;
        [SerializeField, Range(0f, 1f)] private float letterBeamMaxOpacity = 1f;
        [SerializeField, Min(.1f)] private float letterBeamPulseDuration = 2.2f;
        [SerializeField] private AnimationCurve letterBeamHeightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve letterBeamOpacityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public Sprite LetterBeamSprite => letterBeamSprite;
        public Sprite LetterMoteSprite => letterMoteSprite;
        public Material LetterBeamMaterial => letterBeamMaterial;
        public Color LetterBeamTopTint => letterBeamTopTint;
        public Color LetterBeamBottomTint => letterBeamBottomTint;
        public Color LetterMoteTint => letterMoteTint;
        public float LetterMoteScaleMultiplier => letterMoteScaleMultiplier;
        public bool LetterBeamInFront => letterBeamInFront;
        public Vector3 LetterBeamBaseOffset => letterBeamBaseOffset;
        public float LetterBeamBaseScale => letterBeamBaseScale;
        public float LetterBeamHeightPulse => letterBeamHeightPulse;
        public float LetterBeamMinOpacity => letterBeamMinOpacity;
        public float LetterBeamMaxOpacity => letterBeamMaxOpacity;
        public float LetterBeamPulseDuration => Mathf.Max(.1f, letterBeamPulseDuration);
        public AnimationCurve LetterBeamHeightCurve => letterBeamHeightCurve;
        public AnimationCurve LetterBeamOpacityCurve => letterBeamOpacityCurve;

        [Header("Camera follow")]
        [SerializeField, Range(.05f, .3f)] private float cameraFollowTime = .13f;
        [SerializeField, Range(.5f, 1f)] private float cameraFollowStrength = .95f;
        public float CameraFollowTime => cameraFollowTime;
        public float CameraFollowStrength => cameraFollowStrength;
        [Header("Progression")]
        [SerializeField, Min(1)] private int turboChargeGems = 40;
        [SerializeField, Min(.25f), Tooltip("Multiplicador de frecuencia de obstáculos en la carrera.")]
        private float baseObstacleIntensity = 1f;
        public int TurboChargeGems => Mathf.Max(1, turboChargeGems);
        public float BaseObstacleIntensity => Mathf.Max(.25f, baseObstacleIntensity);
        public float InitialSpeed => initialSpeed;
        public float CruiseSpeed => Mathf.Max(initialSpeed, cruiseSpeed);
        public float AccelerationTime => accelerationTime;
        public float SustainedAcceleration => Mathf.Max(0f, sustainedAcceleration);
        public float LaneChangeTime => laneChangeTime;
        public float PatternSpacingAt(float distance) =>
            Mathf.Max(1f, Mathf.Lerp(initialPatternSpacing, finalPatternSpacing, distance / Mathf.Max(1f, patternRampDistance)));

        public float GemSpacingAt(float distance) =>
            Mathf.Max(.1f, Mathf.Lerp(initialGemSpacing, finalGemSpacing, distance / Mathf.Max(1f, patternRampDistance)));

        public float PatternClearance => Mathf.Max(0f, patternClearance);
        public float PreviewTime => previewTime;
        public float MinimumPatternLookAhead => Mathf.Max(0f, minimumPatternLookAhead);
        public bool PreparePatternsBeyondVisibility => preparePatternsBeyondVisibility;
        public float PatternVisibilityMargin => Mathf.Max(0f, patternVisibilityMargin);
        public int Seed => seed;

        [Header("Identidad visual LOTBA")]
        [SerializeField] private LotbaBrandSettings brand = new LotbaBrandSettings();
        public LotbaBrandSettings Brand => brand;

        [Header("Cierre de partida")]
        [SerializeField, Min(0f), Tooltip("Segundos de fundido a negro después del derrape.")]
        private float endingBrakeFadeSeconds = 1.45f;
        [SerializeField, Min(0f)] private float endingArtworkFadeSeconds = .45f;
        [SerializeField, Min(1f)] private float endingCharactersPerSecond = 25f;
        [SerializeField, Min(0f), Tooltip("Segundos de lectura después del último carácter y antes de mostrar Continuar.")]
        private float endingReadingHoldSeconds = 5f;
        [SerializeField, Min(0f)] private float endingContinueFadeSeconds = .25f;
        [SerializeField, Min(0f)] private float endingResultsFadeSeconds = .75f;
        public float EndingBrakeFadeSeconds => Mathf.Max(0f, endingBrakeFadeSeconds);
        public float EndingArtworkFadeSeconds => Mathf.Max(0f, endingArtworkFadeSeconds);
        public float EndingCharactersPerSecond => Mathf.Max(1f, endingCharactersPerSecond);
        public float EndingReadingHoldSeconds => Mathf.Max(0f, endingReadingHoldSeconds);
        public float EndingContinueFadeSeconds => Mathf.Max(0f, endingContinueFadeSeconds);
        public float EndingResultsFadeSeconds => Mathf.Max(0f, endingResultsFadeSeconds);

        [Header("Debug")]
        [SerializeField, Tooltip("Escribe en la Console los choques con penalización de Game y Art Preview.")]
        private bool logObstacleCollisions;
        public bool LogObstacleCollisions => logObstacleCollisions;
    }

    [System.Serializable]
    public sealed class LotbaBrandSettings
    {
        [SerializeField, Tooltip("PPNeueCorp NarrowUltrabold para titulares de impacto.")]
        private TMPro.TMP_FontAsset impactFont;
        [SerializeField, Tooltip("Archivo Regular para texto de lectura, descripciones e información.")]
        private TMPro.TMP_FontAsset bodyFont;
        [SerializeField, Tooltip("Archivo Bold para subtítulos, énfasis y botones.")]
        private TMPro.TMP_FontAsset bodyBoldFont;
        [SerializeField] private Color black = new Color32(20, 20, 20, 255);
        [SerializeField] private Color red = new Color32(252, 61, 36, 255);
        [SerializeField] private Color yellow = new Color32(255, 212, 0, 255);
        [SerializeField] private Color green = new Color32(142, 239, 41, 255);
        [SerializeField] private Color pink = new Color32(255, 115, 205, 255);
        [SerializeField] private Color white = new Color32(254, 255, 243, 255);

        public TMPro.TMP_FontAsset ImpactFont => impactFont;
        public TMPro.TMP_FontAsset BodyFont => bodyFont;
        public TMPro.TMP_FontAsset BodyBoldFont => bodyBoldFont;
        public Color Black => black;
        public Color Red => red;
        public Color Yellow => yellow;
        public Color Green => green;
        public Color Pink => pink;
        public Color White => white;
    }

    [System.Serializable]
    public sealed class PickupFlashSettings
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private Material material;
        [Tooltip("In radial mode, Top Tint is the centre and Bottom Tint is the edge.")]
        [SerializeField] private FlashGradientMode gradientMode;
        [SerializeField] private Color topTint = Color.white;
        [SerializeField] private Color bottomTint = Color.white;
        [Tooltip("Maximum random rotation in either direction, in degrees.")]
        [SerializeField, Range(0f, 180f)] private float randomRotation = 180f;
        [SerializeField, Min(.1f)] private float size = 3f;
        [Tooltip("Offset from the pickup in track coordinates. Positive Z is ahead.")]
        [SerializeField] private Vector3 offset = Vector3.forward;
        [SerializeField, Min(.01f)] private float duration = .24f;
        [SerializeField, Range(0f, 1f)] private float opacity = .6f;
        [Tooltip("Initial scale as a fraction of the maximum size.")]
        [SerializeField, Range(0f, 1f)] private float startScale = .15f;
        [Tooltip("Fraction of the duration spent growing before the flash fades.")]
        [SerializeField, Range(.05f, .95f)] private float growthFraction = .6f;
        public Sprite Sprite => sprite;
        public Material Material => material;
        public Color TopTint => topTint;
        public Color BottomTint => bottomTint;
        public FlashGradientMode GradientMode => gradientMode;
        public float RandomRotation => randomRotation;
        public float Size => size;
        public Vector3 Offset => offset;
        public float Duration => Mathf.Max(.01f, duration);
        public float Opacity => opacity;
        public float StartScale => startScale;
        public float GrowthFraction => Mathf.Clamp(growthFraction, .05f, .95f);
    }

    public enum FlashGradientMode { Linear, Radial }
}
