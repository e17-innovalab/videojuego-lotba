using System;

namespace Lotba.Game
{
    public sealed partial class RunSession
    {
        private readonly RecoveryForkState recoveryFork = new RecoveryForkState();
        private readonly SectionLossLedger lossLedger = new SectionLossLedger();
        private RunRecoveryOpportunity recoveryOpportunity;
        private float recoveryZoneEnd;
        private float recoveryChallengeEnd;

        public bool RecoveryForkActive => recoveryFork.Active;
        public float RecoveryForkDistance => recoveryFork.Distance;
        public int RecoveryForkLane => recoveryFork.RecoveryLane;
        public bool RecoveryForkApproaching => recoveryFork.Active && Distance >= recoveryFork.Distance - 40f;
        public float RecoveryZoneEnd => recoveryZoneEnd;
        public float RecoveryChallengeEnd => recoveryChallengeEnd;
        public bool RecoveryActive { get; private set; }
        public int RecoveryLoss { get; private set; }
        public long RecoveryPointLoss { get; private set; }
        public int RecoveryAmount { get; private set; }
        public long RecoveryPointAmount { get; private set; }
        public float RecoveryFogZ { get; private set; }
        public int GemsRecovered { get; private set; }
        public long PointsRecovered { get; private set; }
        public string RecoveryOutcome { get; private set; }
        public int RecoveryAttempts { get; private set; }
        public int RecoverySucceeded { get; private set; }
        public int RecoveryFailed { get; private set; }
        public int RecoveryDeclined { get; private set; }
        public int RecoveryInterrupted { get; private set; }
        public int RecoveryOffers { get; private set; }
        public int RecoveryUnavailable { get; private set; }
        public int RecoveryOffersClosed { get; private set; }
        public bool HasRecoveryLoss => (lossLedger != null && lossLedger.HasLoss) || RecoveryLoss > 0 || RecoveryPointLoss > 0;
        public int PendingRecoveryGems => lossLedger != null && lossLedger.HasLoss ? lossLedger.PendingGems : RecoveryAmount;
        public long PendingRecoveryPoints => lossLedger != null && lossLedger.HasLoss ? lossLedger.PendingPoints : RecoveryPointAmount;

        internal void AddLossToLedger(long lostPoints, int lostGems, int sequence)
        {
            if (RecoveryActive || recoveryFork.Active) return;
            lossLedger.Add(lostPoints, lostGems, sequence);
        }

        private void ScheduleRecoveryFork()
        {
            var loss = lossLedger.Consume(out int[] lossSequences);
            RecoveryLoss = loss.Gems; RecoveryPointLoss = loss.Points;
            RecoveryAmount = loss.Gems;
            RecoveryPointAmount = loss.Points;
            float forkDistance = Distance + 60f;
            float challengeDistance = transitionSettings.RecoveryDistance(RecoveryAmount,
                RecoveryPointAmount, finishDistance > 0f ? Distance / finishDistance : 0f);
            bool fits = finishDistance <= 0f || forkDistance + challengeDistance + 20f < finishDistance;
            bool hasReward = RecoveryAmount > 0 || RecoveryPointAmount > 0;
            if (!fits || !hasReward)
            {
                RecoveryUnavailable++;
                recoveryOpportunity = new RunRecoveryOpportunity
                {
                    offerId = "offer-" + DecisionsShown, section = telemetrySection,
                    eligiblePoints = loss.Points, eligibleGems = loss.Gems,
                    eligibleLossSequences = lossSequences,
                    offeredPoints = RecoveryPointAmount, offeredGems = RecoveryAmount,
                    offerDistanceMetres = Distance, outcome = "not_offered",
                    cause = !hasReward ? "zero_reward" : "insufficient_route"
                };
                Telemetry.recoveryOpportunities.Add(recoveryOpportunity);
                Record("recovery_not_offered", recoveryOpportunity.cause);
                return;
            }
            int recoveryLane = RecoveryOffers % 2 == 0 ? 0 : 2;
            recoveryFork.Begin(forkDistance, recoveryLane, RecoveryPointAmount, RecoveryAmount);
            recoveryChallengeEnd = forkDistance + challengeDistance;
            recoveryZoneEnd = recoveryChallengeEnd + Math.Max(0f, transitionSettings.recoveryExitBreathMetres);
            recoveryOpportunity = new RunRecoveryOpportunity
            {
                offerId = "offer-" + DecisionsShown, section = telemetrySection,
                eligiblePoints = loss.Points, eligibleGems = loss.Gems,
                eligibleLossSequences = lossSequences,
                offeredPoints = RecoveryPointAmount, offeredGems = RecoveryAmount,
                challengeMetres = challengeDistance, offerDistanceMetres = Distance,
                outcome = "offered", cause = null
            };
            Telemetry.recoveryOpportunities.Add(recoveryOpportunity);
            RecoveryOffers++;
            Record("recovery_fork_appeared");
        }

        internal void UpdateRecoveryFork()
        {
            if (!recoveryFork.ShouldResolve(Distance)) return;
            bool choseRecovery = recoveryFork.Resolve(Lane);
            if (choseRecovery)
            {
                RecoveryActive = true; RecoveryAttempts++;
                RecoveryFogZ = 0f;
                recoveryOpportunity.attemptId = "attempt-" + RecoveryAttempts;
                recoveryOpportunity.accepted = true;
                recoveryOpportunity.startDistanceMetres = Distance;
                recoveryOpportunity.startTimeSeconds = Elapsed;
                recoveryOpportunity.outcome = "active";
                RecoveryOutcome = null;
                Record("recovery_entered");
            }
            else
            {
                RecoveryDeclined++; RecoveryOutcome = "declined";
                recoveryOpportunity.outcome = "skipped";
                recoveryOpportunity.cause = "continue";
                Record("recovery_skipped", "continue");
            }
        }

        public void CollectRecoveryGem()
        {
            if (!RecoveryActive) return;
            GemsRecovered += RecoveryAmount;
            PointsRecovered += RecoveryPointAmount;
            Score += RecoveryPointAmount;
            Gems += RecoveryAmount;
            if (recoveryOpportunity != null)
            {
                recoveryOpportunity.returnedPoints = RecoveryPointAmount;
                recoveryOpportunity.returnedGems = RecoveryAmount;
            }
            ResolveRecovery("succeeded", "gem_collected", RecoveryPointAmount, RecoveryAmount);
        }

        public void MissRecoveryGem()
        {
            if (!RecoveryActive) return;
            if (recoveryOpportunity != null)
            {
                recoveryOpportunity.returnedPoints = 0;
                recoveryOpportunity.returnedGems = 0;
            }
            ResolveRecovery("failed", "gem_missed");
        }

        internal void UpdateRecoveryZone()
        {
            if (!RecoveryActive || State != RunState.Running) return;
            if (Distance >= recoveryChallengeEnd)
            {
                MissRecoveryGem();
            }
        }

        public int RecoveryZoneCollisions { get; private set; }
        public long RecoveryZonePointsLost { get; private set; }
        public int RecoveryZoneGemsLost { get; private set; }

        internal void OnRecoveryImpact(long loss, int gemLoss)
        {
            if (!RecoveryActive) return;
            RecoveryZoneCollisions++;
            RecoveryZonePointsLost += loss;
            RecoveryZoneGemsLost += gemLoss;
        }

        private void ResolveRecovery(string outcome, string cause, long points = 0, int gems = 0)
        {
            RecoveryActive = false; RecoveryOutcome = outcome;
            recoveryOpportunity.outcome = outcome; recoveryOpportunity.cause = cause;
            recoveryOpportunity.endDistanceMetres = Distance; recoveryOpportunity.endTimeSeconds = Elapsed;
            if (outcome == "succeeded") RecoverySucceeded++;
            else if (outcome == "failed") RecoveryFailed++;
            else RecoveryInterrupted++;
            Record("recovery_" + outcome, cause, points, gems);
        }

        private void InterruptRecovery(RunEnding ending)
        {
            string reason = ending == RunEnding.Brake ? "brake" : ending == RunEnding.Overload ? "overload" : "route_complete";
            if (RecoveryActive) ResolveRecovery("interrupted", reason);
            if (recoveryFork.Active)
            {
                recoveryFork.Stop();
                RecoveryOffersClosed++;
                recoveryOpportunity.outcome = "closed"; recoveryOpportunity.cause = reason;
                Record("recovery_fork_closed", reason);
            }
        }

        private RunRecoveryOpportunity RecoverySnapshot() => recoveryOpportunity?.Copy();
    }
}
