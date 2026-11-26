using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Prefab bindings only. Receives visibility/progress without knowing gameplay or input.</summary>
    public sealed class BrakeProgressView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image fill;
        [SerializeField] private Material angularFillMaterial;
        [SerializeField] private RectTransform indicator;
        [SerializeField, Min(0f)] private float indicatorRadius = 82f;
        private const float ArcStartDegrees = -135f;
        private const float ArcDegrees = 200f;
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private Material runtimeFillMaterial;

        private void Awake()
        {
            if (fill == null) return;
            var source = angularFillMaterial != null ? angularFillMaterial : fill.material;
            if (source == null || !source.HasProperty(ProgressId)) return;
            runtimeFillMaterial = new Material(source) { name = $"{source.name} (Runtime)" };
            fill.material = runtimeFillMaterial;
        }

        public void Render(bool visible, float progress)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);

            var normalized = Mathf.Clamp01(progress);
            if (runtimeFillMaterial != null) runtimeFillMaterial.SetFloat(ProgressId, normalized);
            if (indicator != null)
            {
                var angle = (ArcStartDegrees - normalized * ArcDegrees) * Mathf.Deg2Rad;
                indicator.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * indicatorRadius;
                indicator.localRotation = Quaternion.Euler(0f, 0f, ArcStartDegrees + 90f - normalized * ArcDegrees);
            }
        }

        private void OnDestroy()
        {
            if (runtimeFillMaterial == null) return;
            if (Application.isPlaying) Destroy(runtimeFillMaterial);
            else DestroyImmediate(runtimeFillMaterial);
        }
    }
}
