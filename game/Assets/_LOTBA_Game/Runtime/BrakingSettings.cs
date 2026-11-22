using System;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class BrakingSettings
    {
        [SerializeField, InspectorName("Frenado progresivo experimental"), Tooltip("Feature flag. Desactivado conserva el freno inmediato. Se fija por partida.")]
        private bool progressiveEnabled;
        [SerializeField, Min(.1f), InspectorName("Desaceleración (m/s²)")] private float deceleration = 24f;
        [SerializeField, Min(.1f), InspectorName("Aceleración al soltar (m/s²)")] private float releaseAcceleration = 12f;

        public BrakingParameters Capture() => new BrakingParameters(
            progressiveEnabled ? BrakingMode.HoldToStop : BrakingMode.Immediate, deceleration, releaseAcceleration);
    }
}
