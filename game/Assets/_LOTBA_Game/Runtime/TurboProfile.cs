using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Independent gameplay tuning; no charge, collision, recovery or presentation rules.</summary>
    [Serializable]
    public sealed class TurboProfile
    {
        [SerializeField, Min(.05f), InspectorName("Duración (s)")] private float durationSeconds = 6f;
        [SerializeField, Min(0f), InspectorName("Velocidad extra (m/s)")] private float speedBonus = 6f;
        [SerializeField, Min(1), InspectorName("Multiplicador de gemas")] private int gemMultiplier = 1;
        [SerializeField, Min(1), InspectorName("Multiplicador de puntos"), Tooltip("Independiente de las gemas. Se combina con la racha de rosas.")]
        private int pointMultiplier = 1;

        public TurboParameters Capture() => new TurboParameters(Mathf.Max(.05f, durationSeconds),
            Mathf.Max(0f, speedBonus), Mathf.Max(1, gemMultiplier), Mathf.Max(1, pointMultiplier));
    }

    /// <summary>Immutable parameters captured by the caller, usable without any scene or settings asset.</summary>
    public readonly struct TurboParameters
    {
        public readonly float DurationSeconds, SpeedBonus;
        public readonly int GemMultiplier, PointMultiplier;

        public TurboParameters(float durationSeconds, float speedBonus, int gemMultiplier, int pointMultiplier)
        {
            DurationSeconds = durationSeconds;
            SpeedBonus = speedBonus;
            GemMultiplier = gemMultiplier;
            PointMultiplier = pointMultiplier;
        }
    }
}
