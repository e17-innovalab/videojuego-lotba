using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Preserves HUD bindings; the child UI Image owns the shader effect.</summary>
    public sealed class BoostSpeedLines : UnityEngine.UI.Graphic
    {
        [SerializeField] private SpeedLinesController effect;

        public void SetScreenOrigin(Vector2 position) => effect?.SetScreenOrigin(position);

        public void Render(GameSettings configuration, RunSession session, bool showBoostLines)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (effect == null) return;
            bool allowed = showBoostLines && session.State == RunState.Running && !session.OfferPending;
            if (!allowed) effect.HideImmediately();
            else effect.UpdateSpeed(session.Speed, session.BoostActive);
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh) => mesh.Clear();
    }
}
