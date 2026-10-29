using System;

namespace Lotba.CityRush
{
    /// <summary>Deterministic jump, sampled with the session clock so decisions and pauses freeze it.</summary>
    public sealed class JumpArc
    {
        public const float Duration = .72f;
        public const float ApexHeight = 1.75f;
        private float startedAt = float.NegativeInfinity;

        public bool TryStart(float time)
        {
            if (time < startedAt + Duration) return false;
            startedAt = time;
            return true;
        }

        public float HeightAt(float time)
        {
            float phase = (time - startedAt) / Duration;
            return phase <= 0f || phase >= 1f ? 0f : 4f * ApexHeight * phase * (1f - phase);
        }

        public void Reset() => startedAt = float.NegativeInfinity;

        // A ground pickup is at handlebar height. Elevated pickups require the jump arc.
        public static bool CanCollect(float jumpHeight, float gemHeight) => Math.Abs(jumpHeight + 1.1f - gemHeight) < .7f;
        public static bool ClearsObstacle(float jumpHeight) => jumpHeight >= 1.05f;
    }
}
