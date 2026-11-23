using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Representa una superficie por donde el ciclista puede pedalear (rampa, techo de colectivo, container).
    /// Permite que los generadores de gemas coloquen estelas sobre techos de forma coordinada.
    /// </summary>
    public readonly struct TraversableSurface
    {
        public readonly int Lane;
        public readonly float StartZ;
        public readonly float EndZ;
        public readonly float LowHeight;
        public readonly float HighHeight;
        public readonly float Width;
        public readonly bool IsRamp;

        public TraversableSurface(int lane, float startZ, float endZ, float lowHeight, float highHeight, float width = 2f, bool isRamp = false)
        {
            Lane = lane;
            StartZ = startZ;
            EndZ = endZ;
            LowHeight = lowHeight;
            HighHeight = highHeight;
            Width = width;
            IsRamp = isRamp;
        }

        public float HeightAt(float z)
        {
            if (EndZ <= StartZ) return HighHeight;
            float t = Mathf.Clamp01((z - StartZ) / (EndZ - StartZ));
            return Mathf.Lerp(LowHeight, HighHeight, t);
        }

        public bool ContainsZ(float z) => z >= StartZ && z <= EndZ;
    }

    /// <summary>
    /// Trayectoria parabólica de salto en el aire (post-container, rampa o salto entre colectivos).
    /// Permite generar arcos de gemas en el aire estilo Subway Surfers.
    /// </summary>
    public readonly struct JumpArcTrajectory
    {
        public readonly int FromLane;
        public readonly int ToLane;
        public readonly float StartZ;
        public readonly float EndZ;
        public readonly float StartHeight;
        public readonly float PeakHeight;
        public readonly float EndHeight;

        public JumpArcTrajectory(int fromLane, int toLane, float startZ, float endZ, float startHeight, float peakHeight, float endHeight)
        {
            FromLane = fromLane;
            ToLane = toLane;
            StartZ = startZ;
            EndZ = endZ;
            StartHeight = startHeight;
            PeakHeight = peakHeight;
            EndHeight = endHeight;
        }

        public float Length => Mathf.Max(0.01f, EndZ - StartZ);

        /// <summary>
        /// Evalúa la altura parabólica física exacta en un z determinado del salto.
        /// </summary>
        public float EvaluateHeight(float z)
        {
            if (EndZ <= StartZ) return StartHeight;
            float t = Mathf.Clamp01((z - StartZ) / Length);
            float baseLine = Mathf.Lerp(StartHeight, EndHeight, t);
            float arcOffset = (PeakHeight - Mathf.Max(StartHeight, EndHeight)) * 4f * t * (1f - t);
            return baseLine + Mathf.Max(0f, arcOffset);
        }

        /// <summary>
        /// Evalúa el carril o posición X lateral a lo largo de la trayectoria.
        /// </summary>
        public float EvaluateProgress(float z)
        {
            if (EndZ <= StartZ) return 0f;
            return Mathf.Clamp01((z - StartZ) / Length);
        }
    }
}
