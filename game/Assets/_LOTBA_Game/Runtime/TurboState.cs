using System;

namespace Lotba.Game
{
    /// <summary>Reusable timed turbo. The caller supplies its simulation clock and owns game rules.</summary>
    public sealed class TurboState
    {
        private float startedAt;
        private float duration;
        private bool activated;
        private bool requiresExplicitStop;
        private int charge;

        public float ChargeProgress(int requiredCharge) => Math.Min(1f, (float)charge / Math.Max(1, requiredCharge));

        public void AddCharge(int requiredCharge)
        {
            if (charge < Math.Max(1, requiredCharge)) charge++;
        }

        public void ClearCharge() => charge = 0;

        /// <param name="requiresExplicitStop">Keep active until Stop, allowing the caller to resolve final-step contacts first.</param>
        public void Activate(float now, float seconds, bool requiresExplicitStop = false)
        {
            startedAt = now;
            duration = Math.Max(0f, seconds);
            activated = requiresExplicitStop || duration > 0f;
            this.requiresExplicitStop = requiresExplicitStop;
        }

        public bool IsActive(float now) => activated && (requiresExplicitStop || now < startedAt + duration);

        public float Progress(float now) => IsActive(now)
            ? Math.Max(0f, Math.Min(1f, (startedAt + duration - now) / duration)) : 0f;

        public float TimeLeft(float now) => IsActive(now) ? Math.Max(0f, startedAt + duration - now) : 0f;

        public void Stop() => activated = false;
    }
}
