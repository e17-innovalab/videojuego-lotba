using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Shared gameplay tuning values. All speeds use metres per second.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Game settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [SerializeField, Min(30f)] private float duration = 480f;
        [SerializeField, Min(1f)] private float initialSpeed = 18f;
        [SerializeField, Min(1f)] private float maximumSpeed = 30f;
        [SerializeField, Min(1f)] private float accelerationTime = 95f;
        [SerializeField] private Shader riderImpactShader;
        public Shader RiderImpactShader => riderImpactShader;
        [Header("Boost effects")]
        [SerializeField] private Shader turboAuraShader;
        [SerializeField] private Texture2D turboAuraTexture;
        [SerializeField, Range(0f, 1f)] private float turboAuraIntensity = .72f;
        [SerializeField, Min(.01f)] private float turboAuraFadeRate = 4f;
        [SerializeField, Min(0f)] private float boostCameraPullback = .65f;
        [SerializeField, Range(0f, 20f)] private float boostCameraFieldOfView = 5f;
        [SerializeField, Min(.01f)] private float boostCameraBlendRate = 5f;
        [SerializeField] private Color boostLineColor = new Color(.68f, .88f, 1f, .55f);
        [SerializeField, Range(0, 32)] private int boostLineCount = 14;
        [SerializeField, Min(.01f)] private float boostLineSpeed = 2.4f;
        [SerializeField, Range(.01f, .3f)] private float boostLineLength = .15f;
        [SerializeField, Min(.1f)] private float boostLineWidth = 2f;
        public Shader TurboAuraShader => turboAuraShader;
        public Texture2D TurboAuraTexture => turboAuraTexture;
        public float TurboAuraIntensity => turboAuraIntensity;
        public float TurboAuraFadeRate => turboAuraFadeRate;
        public float BoostCameraPullback => boostCameraPullback;
        public float BoostCameraFieldOfView => boostCameraFieldOfView;
        public float BoostCameraBlendRate => boostCameraBlendRate;
        public Color BoostLineColor => boostLineColor;
        public int BoostLineCount => boostLineCount;
        public float BoostLineSpeed => boostLineSpeed;
        public float BoostLineLength => boostLineLength;
        public float BoostLineWidth => boostLineWidth;
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
        [SerializeField] private int seed = 2710;
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
        [SerializeField, Min(2)] private int firstMultiplierGems = 200;
        [SerializeField, Min(1)] private int extraGemsPerMultiplier = 100;
        [SerializeField, Min(200f)] private float firstDecisionMetres = 600f;
        [SerializeField, Min(1f)] private float extraMetresPerDecision = 300f;
        public int FirstMultiplierGems => firstMultiplierGems;
        public int ExtraGemsPerMultiplier => extraGemsPerMultiplier;
        public float FirstDecisionMetres => firstDecisionMetres;
        public float ExtraMetresPerDecision => extraMetresPerDecision;
        public float Duration => duration;
        public float InitialSpeed => initialSpeed;
        public float MaximumSpeed => Mathf.Max(initialSpeed, maximumSpeed);
        public float AccelerationTime => accelerationTime;
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
