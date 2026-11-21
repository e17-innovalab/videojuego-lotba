using UnityEngine;

namespace Lotba.Game
{
    /// <summary>UI-only feedback, driven externally with speeds in metres/second.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Image))]
    public sealed class SpeedLinesController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minSpeedThreshold = 18f;
        [SerializeField, Min(.01f)] private float maxSprintSpeed = 36f;
        [SerializeField, Min(.01f)] private float fadeInSpeed = 4f;
        [SerializeField, Min(.01f)] private float fadeOutSpeed = 40f;
        [SerializeField, Range(0f,1f)] private float maximumOpacity = 1f;
        private const float Interval = .05f;
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int AspectId = Shader.PropertyToID("_AspectRatio");
        private UnityEngine.UI.Image image;
        private RectTransform rect;
        private Canvas ownerCanvas;
        private Material runtimeMaterial, originalMaterial;
        private float speed, opacity, nextTick, previousTick;
        private bool boosting, hasScreenOrigin;
        private Vector2 screenOrigin;

        private void Awake()
        {
            image = GetComponent<UnityEngine.UI.Image>();
            rect = (RectTransform)transform;
            ownerCanvas = image.canvas;
            originalMaterial = image.material;
            if (originalMaterial == null || !originalMaterial.HasProperty(OpacityId))
            {
                image.enabled = false;
                enabled = false;
                Debug.LogError("Assign the LOTBA/UI/Radial Speed Lines material to the Image.",this);
                return;
            }
            // Exactly one instance, retained across activations. No per-frame allocations.
            runtimeMaterial = Instantiate(originalMaterial);
            image.material = runtimeMaterial;
            image.raycastTarget = false;
            HideImmediately();
        }

        public void UpdateSpeed(float currentSpeed, bool isBoosting)
        {
            speed = Mathf.Max(0f,currentSpeed);
            boosting = isBoosting;
        }

        public void SetScreenOrigin(Vector2 position)
        {
            screenOrigin = position;
            hasScreenOrigin = true;
        }

        public void HideImmediately()
        {
            opacity = 0f;
            boosting = false;
            if (runtimeMaterial != null) runtimeMaterial.SetFloat(OpacityId,0f);
            if (image != null) image.enabled = false;
        }

        private void OnEnable()
        {
            previousTick = Time.unscaledTime;
            nextTick = previousTick;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < nextTick || runtimeMaterial == null) return;
            float dt = Mathf.Min(now - previousTick,.1f);
            previousTick = now;
            nextTick = now + Interval;
            float target = boosting ? maximumOpacity * Mathf.InverseLerp(minSpeedThreshold,
                Mathf.Max(minSpeedThreshold + .01f,maxSprintSpeed),speed) : 0f;
            if (target == 0f && opacity == 0f) return;
            opacity = Mathf.MoveTowards(opacity,target,(target > opacity ? fadeInSpeed : fadeOutSpeed) * dt);
            if (opacity <= .001f) opacity = 0f;
            runtimeMaterial.SetFloat(OpacityId,opacity);
            image.enabled = opacity > .001f; // No fullscreen draw/overdraw when inactive.
            if (!image.enabled) return;
            UpdateOrigin();
            // A UI Mask can provide a stencil-material copy; synchronize dynamic values.
            Material rendered = image.materialForRendering;
            if (rendered != runtimeMaterial)
            {
                rendered.SetFloat(OpacityId,opacity);
                rendered.SetVector(CenterId,runtimeMaterial.GetVector(CenterId));
                rendered.SetFloat(AspectId,runtimeMaterial.GetFloat(AspectId));
            }
        }

        private void UpdateOrigin()
        {
            Rect bounds = rect.rect;
            runtimeMaterial.SetFloat(AspectId,bounds.width / Mathf.Max(1f,bounds.height));
            if (!hasScreenOrigin) return;
            if (ownerCanvas == null) ownerCanvas = image.canvas;
            Camera camera = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? ownerCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screenOrigin,camera,out Vector2 local))
                runtimeMaterial.SetVector(CenterId,new Vector4(
                    Mathf.InverseLerp(bounds.xMin,bounds.xMax,local.x),
                    Mathf.InverseLerp(bounds.yMin,bounds.yMax,local.y),0f,0f));
        }

        private void OnDisable() => HideImmediately();

        private void OnDestroy()
        {
            if (runtimeMaterial == null) return;
            if (image != null) image.material = originalMaterial;
            Destroy(runtimeMaterial);
        }
    }
}
