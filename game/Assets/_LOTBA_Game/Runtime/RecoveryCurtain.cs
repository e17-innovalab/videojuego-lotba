using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Visibility only. Placement, rotation and physical size belong to the prefab Transform.</summary>
    [DisallowMultipleComponent]
    public sealed class RecoveryCurtain : MonoBehaviour
    {
        [SerializeField, Tooltip("Transform cuya Z local se ajusta. X/Y, escala y rotación quedan libres.")]
        private Transform distanceTarget;
        [Header("Visibility")]
        [SerializeField, Range(0f, 1f)] private float maximumOpacity = .9f;
        [SerializeField, Min(.01f)] private float fadeInSpeed = 4f;
        [SerializeField, Min(.01f)] private float fadeOutSpeed = 8f;
        private SpriteRenderer[] visuals;
        private Color[] originalColors;
        private bool[] originallyEnabled;
        private float opacity;
        private float authoredZ;
        private bool capturedZ;
        public float AuthoredDistanceZ => capturedZ ? authoredZ :
            (distanceTarget != null ? distanceTarget.localPosition.z : transform.localPosition.z);

        private void Awake()
        {
            if (!Application.IsPlaying(gameObject)) return;
            authoredZ = AuthoredDistanceZ;
            capturedZ = true;
            // Cache the authored children once; never create sprites, textures or renderers at runtime.
            visuals = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[visuals.Length];
            originallyEnabled = new bool[visuals.Length];
            for (int i = 0; i < visuals.Length; i++)
            {
                originalColors[i] = visuals[i].color;
                originallyEnabled[i] = visuals[i].enabled;
            }
            Clear();
        }

        public void SetDistanceZ(float localZ)
        {
            Transform target = distanceTarget != null ? distanceTarget : transform;
            Vector3 position = target.localPosition;
            position.z = localZ;
            target.localPosition = position;
        }

        public void Render(bool active, bool immediateHide)
        {
            if (visuals == null) return;
            opacity = immediateHide ? 0f : Mathf.MoveTowards(opacity,
                active ? maximumOpacity : 0f, Time.unscaledDeltaTime * (active ? fadeInSpeed : fadeOutSpeed));
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].enabled = originallyEnabled[i] && opacity > .001f;
                Color color = originalColors[i];
                color.a *= opacity;
                visuals[i].color = color;
            }
        }

        public void Clear()
        {
            opacity = 0f;
            if (visuals == null) return;
            foreach (var visual in visuals)
                if (visual != null) visual.enabled = false;
        }

        private void OnDisable() => Clear();
    }
}
