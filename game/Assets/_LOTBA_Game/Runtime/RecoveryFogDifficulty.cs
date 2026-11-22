using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Pure distance policy; independent of scene objects, run state and rendering.</summary>
    [Serializable]
    public sealed class RecoveryFogDifficulty
    {
        [SerializeField, InspectorName("Z más cercana"), Tooltip("Extremo difícil en Z local de Fog Visual. Usar un valor menor que la Z del prefab. No se corrige automáticamente.")]
        private float closestZ = 20f;
        [SerializeField, Min(2), InspectorName("Gemas de referencia")]
        private int referenceGems = 20;
        [SerializeField, Min(1f), InspectorName("Metros de referencia")]
        private float referenceMetres = 5000f;

        public float Evaluate(float authoredZ, int offeredGems, float distanceMetres)
        {
            float amount = Mathf.InverseLerp(1f, Mathf.Max(2, referenceGems), offeredGems);
            float progress = Mathf.Clamp01(distanceMetres / Mathf.Max(1f, referenceMetres));
            return Mathf.Lerp(authoredZ, closestZ, (amount + progress) * .5f);
        }
    }
}
