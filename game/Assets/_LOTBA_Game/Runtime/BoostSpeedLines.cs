using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Draws the boost streaks as one HUD graphic, with an open centre.</summary>
    public sealed class BoostSpeedLines : UnityEngine.UI.Graphic
    {
        private GameSettings settings;
        private float elapsed;
        private float opacity;

        public void Render(GameSettings configuration, RunSession session)
        {
            settings = configuration;
            bool visible = session.State == RunState.Running && session.BoostActive &&
                settings.BoostLineCount > 0 && settings.BoostLineColor.a > 0f;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) return;

            elapsed = session.Elapsed;
            float remaining = session.BoostProgress * RunSession.BoostSeconds;
            opacity = Mathf.Clamp01(Mathf.Min(remaining, RunSession.BoostSeconds - remaining) / .22f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            if (settings == null) return;

            Rect rect = rectTransform.rect;
            var halfSize = rect.size * .5f;
            for (int i = 0; i < settings.BoostLineCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float y = Mathf.Repeat(i * .618034f, 1f) * 1.65f - .8f;
                var ray = new Vector2(side * halfSize.x, y * halfSize.y);
                float travel = Mathf.Repeat(elapsed * settings.BoostLineSpeed + i * .381966f, 1f);
                float tipRadius = Mathf.Lerp(.77f, 1f, travel);
                float tailRadius = Mathf.Max(.65f, tipRadius - settings.BoostLineLength);
                Vector2 tip = rect.center + ray * tipRadius;
                Vector2 tail = rect.center + ray * tailRadius;
                Vector2 width = new Vector2(-ray.y, ray.x).normalized * settings.BoostLineWidth * .5f;
                Color tint = settings.BoostLineColor;
                tint.a *= opacity * Mathf.Sin(travel * Mathf.PI);
                Color fade = new Color(tint.r, tint.g, tint.b, 0f);
                int vertex = mesh.currentVertCount;
                mesh.AddVert(tail - width * .2f, fade, Vector2.zero);
                mesh.AddVert(tail + width * .2f, fade, Vector2.zero);
                mesh.AddVert(tip + width, tint, Vector2.zero);
                mesh.AddVert(tip - width, tint, Vector2.zero);
                mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
                mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
            }
        }
    }
}
