using System;

namespace Lotba.Game
{
    public sealed partial class RunSession
    {
        public float RecoverySeconds => RecoveryTurbo.DurationSeconds;
        public float RecoverySpeed => RecoveryTurbo.SpeedBonus;
        public const float RecoveryResumeSeconds = 1.25f;
        public const float RecoveryImpactSeconds = .85f;
        public bool RecoveryImpactPending { get; private set; }
        public bool RecoveryOfferPending { get; private set; }
        public bool RecoveryActive { get; private set; }
        public bool RecoveryPreparing { get; private set; }
        public bool RecoveryRejoining => Elapsed < recoveryResumeUntil;
        public bool RecoveryOffered { get; private set; }
        public int RecoveryLoss { get; private set; }
        public int RecoveryAmount { get; private set; }
        public float RecoveryFogZ { get; private set; }
        private float recoveryFogSelectedAtMetres;
        public int GemsRecovered { get; private set; }
        public string RecoveryOutcome { get; private set; }
        public float RecoveryTimeLeft => RecoveryActive ? Math.Max(0f, recoveryUntil - Elapsed) : 0f;
        private float recoveryUntil, recoveryResumeUntil;

        private void OnRecoveryImpact(int gemLoss)
        {
            if (RecoveryActive)
            {
                RecoveryActive = false;
                RecoveryOutcome = "failed";
                Record("recovery_failed", "collision");
            }
            if (Telemetry == null || RecoveryOffered || RecoveryImpactPending || gemLoss <= 0) return;
            float requiredDistance = (Speed + RecoverySpeed + 5f) * (RecoverySeconds + RecoveryResumeSeconds + 2f);
            if (finishDistance > 0f && finishDistance - Distance < requiredDistance) return;
            RecoveryLoss = gemLoss;
            RecoveryAmount = Math.Min(20, gemLoss);
            RecoveryImpactPending = true;
            Record("recovery_impact_started");
        }

        public void ShowRecoveryOffer()
        {
            if (!RecoveryImpactPending || State != RunState.Running) return;
            RecoveryImpactPending = false;
            RecoveryOffered = RecoveryOfferPending = true;
            Record("recovery_offer_shown", $"lost {RecoveryLoss} gems; recover {RecoveryAmount} gems; {RecoverySeconds:0} s; +{RecoverySpeed * 3.6f:0.#} km/h");
        }

        /// <summary>Called after world contacts so a final-step collision precedes success.</summary>
        public void UpdateRecovery()
        {
            if (State != RunState.Running || Telemetry == null) return;
            if (!RecoveryActive || Elapsed < recoveryUntil) return;
            RecoveryActive = false;
            turbo.Stop();
            GemsRecovered += RecoveryAmount;
            RecoveryOutcome = "succeeded";
            Record("recovery_succeeded", gemDelta: RecoveryAmount);
        }

        private void BeginRecoveryChallengeIfReady()
        {
            if (!RecoveryPreparing || RecoveryRejoining) return;
            RecoveryPreparing = false;
            RecoveryActive = true;
            recoveryUntil = Elapsed + RecoverySeconds;
            var curtain = settings.RecoveryCurtainPrefab;
            Record("recovery_challenge_started", curtain != null
                ? $"{RecoveryAmount} gems; curtain prefab {curtain.name}; localZ {RecoveryFogZ:0.###}; selected at {recoveryFogSelectedAtMetres:0.###} m"
                : $"{RecoveryAmount} gems; curtain not configured");
            ActivateBoost("recovery_auto", recovery: true);
        }

        public void ChooseRecovery(bool accept, int clearedObstacles = 0, float fogZ = 0f)
        {
            if (!RecoveryOfferPending || State != RunState.Running) return;
            RecoveryOfferPending = false;
            if (!accept)
            {
                RecoveryOutcome = "declined";
                Record("recovery_declined");
            }
            else
            {
                RecoveryFogZ = fogZ;
                recoveryFogSelectedAtMetres = Distance;
                RecoveryAttempts++;
                RecoveryPreparing = true;
                Record("recovery_started", $"{RecoveryAmount} gems");
            }
            // Same rejoin window for either choice; the challenge clock starts afterwards.
            recoveryResumeUntil = Elapsed + RecoveryResumeSeconds;
            protectedUntil = Math.Max(protectedUntil, recoveryResumeUntil);
            Record("recovery_resume_started", $"{RecoveryResumeSeconds:0.##} s; cleared {clearedObstacles} obstacles");
        }

        private void InterruptRecovery(RunEnding ending)
        {
            string reason = ending == RunEnding.Brake ? "brake" : ending == RunEnding.Overload ? "overload" : "route_complete";
            if (RecoveryActive || RecoveryPreparing)
            {
                RecoveryActive = RecoveryPreparing = false;
                turbo.Stop();
                RecoveryOutcome = "interrupted";
                Record("recovery_interrupted", reason);
            }
            if (RecoveryOfferPending) Record("recovery_offer_closed", reason);
            RecoveryOfferPending = RecoveryImpactPending = false;
            recoveryResumeUntil = 0f;
        }
    }
}
