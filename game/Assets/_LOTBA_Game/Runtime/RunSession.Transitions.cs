using System;

namespace Lotba.Game
{
    public sealed partial class RunSession
    {
        private SectionTransitionPlan[] transitions;
        private int transitionIndex;
        private bool transitionAnnounced;
        public SectionTransitionPlan CurrentTransition => transitionIndex < transitions.Length ? transitions[transitionIndex] : null;
        public bool TransitionApproaching => State == RunState.Running && CurrentTransition != null && Distance >= CurrentTransition.Announce && !OfferPending;
        public int PurchasedGemMultiplier { get; private set; } = 1;
        public int PurchasedPointMultiplier { get; private set; } = 1;
        public int OfferedGemMultiplier => PurchasedGemMultiplier + Math.Max(0, transitionSettings.purchasedGemMultiplier - 1);
        public int OfferedPointMultiplier => PurchasedPointMultiplier + Math.Max(0, transitionSettings.purchasedPointMultiplier - 1);
        public float RealElapsed { get; private set; }
        public void AdvancePresentationClock(float dt)
        {
            if (State == RunState.Running || State == RunState.Paused) RealElapsed += Math.Max(0f, dt);
        }
        public void UpdateTransitionArrival()
        {
            AnnounceTransitionIfNeeded();
            if (Telemetry == null || State != RunState.Running || OfferPending || RecoveryForkActive ||
                CurrentTransition == null || Distance < CurrentTransition.Stop) return;
            transitionDecisionPending = true;
            Speed = 0f;
            DecisionsShown++;
            Record("transition_decision_shown", decisionNumber: DecisionsShown);
        }

        private void RefreshTransitionOffer()
        {
            var plan = CurrentTransition;
            OfferDistance = plan?.Stop ?? 0f;
            OfferedSpeedIncrease = plan?.ExtraSpeed ?? 0f;
            UpgradeCost = plan?.Cost ?? 0;
        }

        public void ChooseTransitionUpgrade(bool accelerate)
        {
            if (!transitionDecisionPending || State != RunState.Running) return;
            if (accelerate && UpgradeCost > 0 && !CanBuyUpgrade) return;
            if (!accelerate)
            {
                DecisionsKept++;
                Record("speed_kept", decisionNumber: DecisionsShown);
            }
            else if (UpgradeCost > 0)
            {
                GemsSpent += UpgradeCost;
                PurchasedSpeed += OfferedSpeedIncrease;
                PurchasedGemMultiplier += Math.Max(0, transitionSettings.purchasedGemMultiplier - 1);
                PurchasedPointMultiplier += Math.Max(0, transitionSettings.purchasedPointMultiplier - 1);
                PinkGemsUnlocked = true;
                SpeedUpgrades++;
                Record("speed_upgrade_purchased", gemDelta: -UpgradeCost, decisionNumber: DecisionsShown);
            }
            else
            {
                ActivateBoost(null, DecisionsShown);
            }
            LeaveTransition();
        }

        public void ChooseTransitionRecovery()
        {
            if (!transitionDecisionPending || State != RunState.Running) return;
            if (!HasRecoveryLoss) return;

            if (lossLedger.HasLoss)
            {
                var loss = lossLedger.Consume(out int[] lossSequences);
                RecoveryLoss = loss.Gems;
                RecoveryPointLoss = loss.Points;
                RecoveryAmount = Math.Min(transitionSettings.maximumRecoveryGems, loss.Gems);
                RecoveryPointAmount = Math.Min(transitionSettings.maximumRecoveryPoints, loss.Points);
                recoveryOpportunity = new RunRecoveryOpportunity
                {
                    offerId = "trans-offer-" + DecisionsShown,
                    section = telemetrySection,
                    eligiblePoints = loss.Points,
                    eligibleGems = loss.Gems,
                    eligibleLossSequences = lossSequences,
                    offeredPoints = RecoveryPointAmount,
                    offeredGems = RecoveryAmount,
                    challengeMetres = transitionSettings.recoveryDistanceMetres,
                    offerDistanceMetres = Distance,
                    outcome = "offered"
                };
                if (Telemetry != null) Telemetry.recoveryOpportunities.Add(recoveryOpportunity);
                RecoveryOffers++;
            }

            RecoveryActive = true;
            RecoveryAttempts++;
            RecoveryFogZ = 0f;
            recoveryZoneEnd = Distance + transitionSettings.recoveryDistanceMetres;
            RecoveryZoneCollisions = 0;
            RecoveryZonePointsLost = 0;
            RecoveryZoneGemsLost = 0;
            if (recoveryOpportunity != null)
            {
                recoveryOpportunity.attemptId = "attempt-" + RecoveryAttempts;
                recoveryOpportunity.accepted = true;
                recoveryOpportunity.startDistanceMetres = Distance;
                recoveryOpportunity.startTimeSeconds = Elapsed;
                recoveryOpportunity.outcome = "active";
            }
            RecoveryOutcome = null;
            Record("recovery_entered", "transition_choice");

            LeaveTransition();
        }

        private void InitializeTransitions() => transitions = SectionTransitionPlan.Build(telemetryRoute, transitionSettings);
        private void AnnounceTransitionIfNeeded()
        {
            if (!TransitionApproaching || transitionAnnounced) return;
            transitionAnnounced = true; Record("transition_announced");
        }
        private void LeaveTransition()
        {
            transitionDecisionPending = false;
            previousOfferDistance = Distance;
            PurchasedSpeed += 1.2f;
            Record("transition_departed");
            transitionIndex++;
            transitionAnnounced = false;
            RefreshTransitionOffer();
        }
    }
}
