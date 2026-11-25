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
        public int PinkGemActivationCount => Math.Max(0, SpeedUpgrades);
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
            MovementSpeed = 0f;
            DecisionsShown++;
            CreateTransitionRecoveryOpportunity();
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
                DeclineTransitionRecoveryOpportunity("continue");
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
                DeclineTransitionRecoveryOpportunity("speed_upgrade");
                Record("speed_upgrade_purchased", gemDelta: -UpgradeCost, decisionNumber: DecisionsShown);
            }
            else
            {
                DeclineTransitionRecoveryOpportunity("boost_offer");
                ActivateBoost(null, DecisionsShown);
            }
            LeaveTransition();
        }

        public void ChooseTransitionRecovery()
        {
            if (!transitionDecisionPending || State != RunState.Running) return;
            if (!HasRecoveryLoss || (PendingRecoveryPoints <= 0 && PendingRecoveryGems <= 0)) return;

            // The opportunity was recorded when the recovery option became visible.
            // Keep it distinct from any earlier fork opportunity tied to the same losses.
            if (recoveryOpportunity == null || recoveryOpportunity.section != RecoverySourceSection ||
                recoveryOpportunity.outcome != "offered" || recoveryOpportunity.accepted)
                CreateTransitionRecoveryOpportunity();
            if (recoveryOpportunity == null || recoveryOpportunity.outcome != "offered") return;

            var loss = lossLedger.Consume(recoveryOpportunity.section, out int[] lossSequences);
            RecoveryLoss = loss.Gems;
            RecoveryPointLoss = loss.Points;
            RecoveryAmount = loss.Gems;
            RecoveryPointAmount = loss.Points;
            recoveryOpportunity.eligiblePoints = loss.Points;
            recoveryOpportunity.eligibleGems = loss.Gems;
            recoveryOpportunity.eligibleLossSequences = lossSequences;
            recoveryOpportunity.offeredPoints = RecoveryPointAmount;
            recoveryOpportunity.offeredGems = RecoveryAmount;

            RecoveryActive = true;
            RecoveryAttempts++;
            RecoveryFogZ = 0f;
            float challengeDistance = recoveryOpportunity != null
                ? recoveryOpportunity.challengeMetres
                : transitionSettings.RecoveryDistance(RecoveryAmount, RecoveryPointAmount,
                    finishDistance > 0f ? Distance / finishDistance : 0f);
            recoveryChallengeEnd = Distance + challengeDistance;
            recoveryZoneEnd = recoveryChallengeEnd + Math.Max(0f, transitionSettings.recoveryExitBreathMetres);
            recoveryOpportunity.attemptId = "attempt-" + RecoveryAttempts;
            recoveryOpportunity.accepted = true;
            recoveryOpportunity.startDistanceMetres = Distance;
            recoveryOpportunity.startTimeSeconds = Elapsed;
            recoveryOpportunity.outcome = "active";
            RecoveryOutcome = null;
            Record("recovery_entered", "transition_choice", decisionNumber: DecisionsShown);

            LeaveTransition();
        }

        private void InitializeTransitions() => transitions = SectionTransitionPlan.Build(telemetryRoute, transitionSettings);

        private int RecoverySourceSection
        {
            get
            {
                if (telemetryRoute == null || CurrentTransition == null) return telemetrySection;
                int section = CurrentTransition.Section;
                if (section < 0 || section >= telemetryRoute.Sections.Length) return telemetrySection;
                while (section >= 0 && telemetryRoute.Sections[section].Transition) section--;
                return section >= 0 ? section : CurrentTransition.Section;
            }
        }

        private void CreateTransitionRecoveryOpportunity()
        {
            if (!HasRecoveryLoss || (PendingRecoveryPoints <= 0 && PendingRecoveryGems <= 0)) return;

            int sourceSection = RecoverySourceSection;
            int[] lossSequences = lossLedger.SnapshotLossSequences(sourceSection);
            long eligiblePoints = PendingRecoveryPoints;
            int eligibleGems = PendingRecoveryGems;
            float challengeDistance = transitionSettings.RecoveryDistance(eligibleGems, eligiblePoints,
                finishDistance > 0f ? Distance / finishDistance : 0f);

            recoveryOpportunity = new RunRecoveryOpportunity
            {
                offerId = "trans-offer-" + DecisionsShown,
                section = sourceSection,
                decisionNumber = DecisionsShown,
                eligiblePoints = eligiblePoints,
                eligibleGems = eligibleGems,
                eligibleLossSequences = lossSequences,
                offeredPoints = eligiblePoints,
                offeredGems = eligibleGems,
                challengeMetres = challengeDistance,
                offerDistanceMetres = Distance,
                outcome = "offered"
            };
            Telemetry.recoveryOpportunities.Add(recoveryOpportunity);
            RecoveryOffers++;
        }

        private void DeclineTransitionRecoveryOpportunity(string reason)
        {
            if (recoveryOpportunity == null || recoveryOpportunity.section != RecoverySourceSection ||
                recoveryOpportunity.outcome != "offered" || recoveryOpportunity.accepted) return;

            recoveryOpportunity.outcome = "declined";
            recoveryOpportunity.cause = reason;
            recoveryOpportunity.endDistanceMetres = Distance;
            recoveryOpportunity.endTimeSeconds = Elapsed;
            lossLedger.Consume(recoveryOpportunity.section, out _);
            RecoveryDeclined++;
            Record("recovery_declined", reason, decisionNumber: DecisionsShown);
        }

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
