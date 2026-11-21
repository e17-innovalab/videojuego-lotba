using System;

namespace Lotba.Game
{
    public sealed partial class RunSession
    {
        // MVP prototype: one voluntary opportunity following an actual loss.
        public const float RecoverySeconds = 8f;
        public const float RecoverySpeed = 6f;
        public bool RecoveryOfferPending { get; private set; }
        public bool RecoveryActive { get; private set; }
        public bool RecoveryOffered { get; private set; }
        public int RecoveryAmount { get; private set; }
        public int GemsRecovered { get; private set; }
        public string RecoveryOutcome { get; private set; }
        public float RecoveryTimeLeft => RecoveryActive ? Math.Max(0f, recoveryUntil - Elapsed) : 0f;
        private float recoveryUntil, recoveryEligibleAt;
        private int recoveryLoss;

        private void OnRecoveryImpact(int gemLoss)
        {
            if (RecoveryActive)
            {
                RecoveryActive = false;
                RecoveryOutcome = "failed";
                Record("recovery_failed", "collision");
            }
            if (Telemetry == null || RecoveryOffered || gemLoss <= 0) return;
            recoveryLoss = gemLoss;
            recoveryEligibleAt = Elapsed + ImpactRecoverySeconds + .5f;
        }

        /// <summary>Called after world contacts: a collision on the final step still fails the attempt.</summary>
        public void UpdateRecovery(bool safeToOffer)
        {
            if (State != RunState.Running || Telemetry == null) return;
            if (RecoveryActive && Elapsed >= recoveryUntil)
            {
                RecoveryActive = false;
                GemsRecovered += RecoveryAmount;
                RecoveryOutcome = "succeeded";
                Record("recovery_succeeded", gemDelta: RecoveryAmount);
            }
            if (!safeToOffer || RecoveryOffered || recoveryLoss <= 0 || OfferPending ||
                BoostActive || Elapsed < recoveryEligibleAt) return;
            // Avoid offering an attempt that cannot finish before the route ends.
            float requiredDistance = (Speed + RecoverySpeed + 5f) * (RecoverySeconds + 2f);
            if (finishDistance > 0f && finishDistance - Distance < requiredDistance) return;
            RecoveryAmount = Math.Min(20, recoveryLoss);
            RecoveryOffered = RecoveryOfferPending = true;
            Record("recovery_offer_shown", $"{RecoveryAmount} gems; {RecoverySeconds:0} s; +{RecoverySpeed * 3.6f:0.#} km/h");
        }

        public void ChooseRecovery(bool accept)
        {
            if (!RecoveryOfferPending || State != RunState.Running) return;
            RecoveryOfferPending = false;
            if (!accept)
            {
                RecoveryOutcome = "declined";
                Record("recovery_declined");
                return;
            }
            RecoveryAttempts++;
            RecoveryActive = true;
            recoveryUntil = Elapsed + RecoverySeconds;
            Record("recovery_started", $"{RecoveryAmount} gems");
        }

        private void InterruptRecovery(RunEnding ending)
        {
            string reason = ending == RunEnding.Brake ? "brake" : ending == RunEnding.Overload ? "overload" : "route_complete";
            if (RecoveryActive)
            {
                RecoveryActive = false;
                RecoveryOutcome = "interrupted";
                Record("recovery_interrupted", reason);
            }
            if (RecoveryOfferPending) Record("recovery_offer_closed", reason);
            RecoveryOfferPending = false;
        }
    }
}
