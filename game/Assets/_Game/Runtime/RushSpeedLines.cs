using UnityEngine;

namespace Lotba.CityRush
{
    /// <summary>Subtle peripheral motion cues; disabled by the reduced effects preference.</summary>
    public sealed class RushSpeedLines : UnityEngine.UI.MaskableGraphic
    {
        public float Intensity { get; set; }
        private void Update() { if (Intensity > 0) SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); if (Intensity <= 0) return;
            Rect rect = rectTransform.rect;
            Vector2 focus = new Vector2(rect.center.x, rect.yMin + rect.height * .63f);
            for (int i = 0; i < 12; i++)
            {
                float phase = Mathf.Repeat(Time.unscaledTime * .8f + i * .173f, 1);
                float y = rect.yMin + rect.height * ((i / 2) / 6f);
                Vector2 edge = new Vector2(i % 2 == 0 ? rect.xMin : rect.xMax, y);
                Vector2 direction = (edge - focus).normalized;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x) * 1.5f;
                Vector2 tip = Vector2.Lerp(focus, edge, .70f + phase * .3f);
                Vector2 tail = tip - direction * rect.height * (.045f + Intensity * .04f);
                Color tint = new Color(1, 1, 1, Intensity * .30f * Mathf.Sin(phase * Mathf.PI));
                RushGauge.Quad(mesh, tail + perpendicular, tip + perpendicular, tip - perpendicular, tail - perpendicular, tint);
            }
        }
        public void ClearEffect() { Intensity = 0; SetVerticesDirty(); }
    }
}
