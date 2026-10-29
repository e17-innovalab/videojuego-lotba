using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Procedural dial; no raster dependency and no per-frame hierarchy creation.</summary>
    public sealed class SpeedGauge : UnityEngine.UI.MaskableGraphic
    {
        private float value;
        public float Value { set { float next = Mathf.Clamp01(value); if (Mathf.Abs(this.value - next) < .001f) return; this.value = next; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); Rect rect = rectTransform.rect;
            Vector2 centre = new Vector2(rect.center.x, rect.yMin + rect.height * .08f);
            float radius = Mathf.Min(rect.width * .49f, rect.height * .88f);
            for (int i = 0; i < 24; i++)
            {
                float start = Mathf.PI - i * Mathf.PI / 24, end = start - Mathf.PI / 24 + .012f;
                Color tint = Color.Lerp(new Color(.3f, .85f, .38f), new Color(1, .3f, .22f), i / 23f);
                if (i / 24f > value) tint *= new Color(.4f, .4f, .4f, 1);
                Quad(mesh, centre + Direction(start) * radius, centre + Direction(end) * radius,
                    centre + Direction(end) * radius * .83f, centre + Direction(start) * radius * .83f, tint);
            }
            Color backing = new Color(.08f, .16f, .22f, .94f);
            for (int i = 0; i < 32; i++)
            {
                int index = mesh.currentVertCount;
                mesh.AddVert(centre, backing, Vector2.zero);
                mesh.AddVert(centre + Direction(Mathf.PI - i * Mathf.PI / 32) * radius * .79f, backing, Vector2.zero);
                mesh.AddVert(centre + Direction(Mathf.PI - (i + 1) * Mathf.PI / 32) * radius * .79f, backing, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
            }
            float angle = Mathf.Lerp(Mathf.PI, 0, value);
            Vector2 direction = Direction(angle), perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 pivot = centre + Vector2.up * radius * .12f;
            // Short needle stays outside the speed label's central reading area.
            Quad(mesh, pivot + direction * radius * .52f + perpendicular * 3,
                pivot + direction * radius * .91f + perpendicular * 2,
                pivot + direction * radius * .91f - perpendicular * 2,
                pivot + direction * radius * .52f - perpendicular * 3, new Color(1, .49f, .12f));
        }
        private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        internal static void Quad(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
