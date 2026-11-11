using System;

namespace Lotba.Game
{
    public enum RunState { Ready, Running, Paused, Finished }
    public enum RunEnding { None, Brake, TimeLimit }

    /// <summary>Run state only: no cards, menus, physics, or scene object ownership.</summary>
    public sealed class RunSession
    {
        private readonly GameSettings settings;
        private readonly RouteDefinition telemetryRoute;
        private int telemetrySection;
        public RunTelemetry Telemetry { get; private set; }
        private const float ImpactRecoverySeconds = 2.5f;
        private const float ImpactSpeedLoss = 9f;
        private const float ImpactProtectionSeconds = 1.1f;
        public const int BoostImpactGemLoss = 5;
        private float impactTime = float.NegativeInfinity;
        private float protectedUntil;
        private int multiplierStartGems;
        private float decisionStartDistance;
        public const float BoostSeconds = 6f;
        public const float BoostSpeed = 6f;
        private float boostUntil;
        public bool OfferPending { get; private set; }
        public bool BoostActive => Elapsed < boostUntil;
        public float BoostProgress => BoostActive ? (boostUntil - Elapsed) / BoostSeconds : 0f;
        public int VoluntaryBoosts { get; private set; }
        public int BonusGems { get; private set; }
        public float OfferDistance { get; private set; }
        public int DecisionsShown { get; private set; }
        public int DecisionsKept { get; private set; }
        public float NextDecisionDistance => decisionStartDistance + settings.FirstDecisionMetres +
            DecisionsShown * settings.ExtraMetresPerDecision;
        public float OfferProgress => OfferPending ? 1f : Math.Min(1f,
            (Distance - decisionStartDistance) / ((OfferDistance > 0f ? OfferDistance : NextDecisionDistance) - decisionStartDistance));
        public RunState State { get; private set; }
        public RunEnding Ending { get; private set; }
        public float Elapsed { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float PeakSpeed { get; private set; }
        public long Score { get; private set; }
        public int Gems { get; private set; }
        public int GemsLost { get; private set; }
        public int GemBalance => Gems - GemsLost;
        public int BoostCollisions { get; private set; }
        public int Collisions { get; private set; }
        public int Lane { get; private set; } = 1;
        public int Multiplier { get; private set; } = 1;
        public int GemsForMultiplier => settings.FirstMultiplierGems + (Multiplier - 1) * settings.ExtraGemsPerMultiplier;
        public float MultiplierProgress => (Gems - multiplierStartGems) / (float)GemsForMultiplier;
        public float Remaining => Math.Max(0f, settings.Duration - Elapsed);
        public bool IsProtected => Elapsed < protectedUntil;
        public float ProtectionProgress => IsProtected
            ? Math.Max(0f, 1f - (protectedUntil - Elapsed) / ImpactProtectionSeconds)
            : 1f;

        public RunSession(GameSettings settings, RouteDefinition telemetryRoute = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.telemetryRoute = telemetryRoute;
            Speed = PeakSpeed = settings.InitialSpeed;
        }
        public void Start()
        {
            if (State != RunState.Ready) return;
            State = RunState.Running;
            if (telemetryRoute == null) return;
            Telemetry = new RunTelemetry { route = telemetryRoute.name, seed = settings.Seed, durationSeconds = settings.Duration };
            Record("run_started");
        }
#if UNITY_EDITOR
        /// <summary>Artist preview advances without a time limit or progression curve.</summary>
        public void TickPreview(float dt, float speed)
        {
            if (State != RunState.Running) return;
            Elapsed += dt;
            Speed = PeakSpeed = speed;
            Distance += speed * dt;
        }
#endif
        public void Tick(float dt)
        {
            if (State != RunState.Running || OfferPending) return;
            float step = Math.Min(dt, Remaining);
            Elapsed += step;
            float progression = 1f - (float)Math.Exp(-Elapsed / settings.AccelerationTime);
            float baseSpeed = settings.InitialSpeed + (settings.MaximumSpeed - settings.InitialSpeed) * progression;
            float sinceImpact = Elapsed - impactTime;
            float recovery = Math.Min(1f, sinceImpact / ImpactRecoverySeconds);
            Speed = Math.Max(0f, baseSpeed + (BoostActive ? BoostSpeed : 0f) - ImpactSpeedLoss * (1f - recovery));
            PeakSpeed = Math.Max(PeakSpeed, Speed);
            Distance += Speed * step;
            if (Telemetry != null)
            {
                int section = telemetryRoute.SectionAt(Distance, out _);
                if (section != telemetrySection)
                {
                    telemetrySection = section;
                    Record("zone_entered");
                }
            }
            if (Remaining <= 0f) Finish(RunEnding.TimeLimit);
        }
        public void ScheduleSpeedOffer(float distance)
        {
            if (Telemetry != null && OfferDistance == 0f && distance >= NextDecisionDistance) OfferDistance = distance;
        }

        public void ShowSpeedOffer()
        {
            if (OfferDistance <= 0f || Distance < OfferDistance || State != RunState.Running || OfferPending) return;
            OfferPending = true;
            DecisionsShown++;
            Record("speed_offer_shown", decisionNumber: DecisionsShown);
        }

        public void ChooseSpeed(bool accelerate)
        {
            if (!OfferPending || State != RunState.Running) return;
            OfferPending = false;
            decisionStartDistance = Distance;
            OfferDistance = 0f;
            if (!accelerate)
            {
                DecisionsKept++;
                Record("speed_kept", decisionNumber: DecisionsShown);
                return;
            }

            boostUntil = Elapsed + BoostSeconds;
            VoluntaryBoosts++;
            Record("boost_activated", decisionNumber: DecisionsShown);
        }

        public bool Move(int direction)
        {
            if (State != RunState.Running) return false;
            int lane = Math.Max(0, Math.Min(2, Lane + Math.Sign(direction)));
            if (Lane == lane) return false;
            Lane = lane;
            Record("lane_changed");
            return true;
        }
        public long CollectGem(int airborneNote = -1)
        {
            if (State != RunState.Running) return 0;
            int previousMultiplier = Multiplier;
            int gemCount = BoostActive ? 2 : 1;
            long reward = 10L * Multiplier * gemCount;
            Score += reward; Gems += gemCount; BonusGems += gemCount - 1;
            if (Gems - multiplierStartGems >= GemsForMultiplier)
            {
                multiplierStartGems += GemsForMultiplier;
                Multiplier++;
            }
            Record("gem_collected", airborneNote >= 0 ? "air" : "ground", reward,
                previousMultiplier: previousMultiplier, gemDelta: gemCount);
            if (gemCount > 1) Record("boost_bonus_gem");
            if (Multiplier > previousMultiplier)
                Record("multiplier_level_up", previousMultiplier: previousMultiplier);
            return reward;
        }
        public long Hit(string obstacleId = null)
        {
            if (State != RunState.Running || IsProtected) return 0;
            bool duringBoost = BoostActive;
            int gemLoss = duringBoost ? Math.Min(GemBalance, BoostImpactGemLoss) : 0;
            long loss = duringBoost ? 0 : Math.Min(Score, Math.Max(50L, Score / 4));
            GemsLost += gemLoss;
            Score -= loss; Collisions++;
            if (duringBoost)
            {
                BoostCollisions++;
                boostUntil = Elapsed;
            }
            impactTime = Elapsed;
            protectedUntil = impactTime + ImpactProtectionSeconds;
            Record("obstacle_hit", obstacleId, -loss, gemDelta: -gemLoss, duringBoost: duringBoost);
            return loss;
        }
        public void Pause()
        {
            if (State != RunState.Running) return;
            State = RunState.Paused;
            Record("run_paused");
        }
        public void Resume()
        {
            if (State != RunState.Paused) return;
            State = RunState.Running;
            Record("run_resumed");
        }
        public void Brake()
        {
            if (State == RunState.Running || State == RunState.Paused) Finish(RunEnding.Brake);
        }
        private void Finish(RunEnding ending)
        {
            Ending = ending;
            State = RunState.Finished;
            OfferPending = false;
            if (Telemetry != null)
            {
                string reason = ending == RunEnding.Brake ? "brake" : "time_limit";
                Record("run_finished", reason);
                Telemetry.summary = new RunTelemetrySummary
                {
                    ending = reason, timeSeconds = Elapsed, distanceMetres = Distance, peakSpeedKmh = PeakSpeed * 3.6f,
                    score = Score, gems = Gems, collisions = Collisions, bestMultiplier = Multiplier,
                    decisionsShown = DecisionsShown, decisionsKept = DecisionsKept,
                    voluntaryBoosts = VoluntaryBoosts, bonusGems = BonusGems,
                    gemsLost = GemsLost, gemBalance = GemBalance, boostCollisions = BoostCollisions
                };
            }
            Speed = 0f;
        }

        public void RecordJump() => Record("jump");

        public void RecordLetter(string word, int index, bool complete)
        {
            if (Telemetry == null) return;
            Record("letter_collected", $"{word[index]} · {word} · {index + 1}");
            if (complete) Record("word_completed", word);
        }

        private void Record(string type, string detail = null, long delta = 0,
            int? previousMultiplier = null, int gemDelta = 0, bool duringBoost = false, int decisionNumber = 0)
        {
            if (Telemetry == null) return;
            Telemetry.events.Add(new RunTelemetryEvent
            {
                sequence = Telemetry.events.Count + 1, type = type, detail = detail,
                zone = telemetryRoute.Sections[telemetrySection].Map.DisplayName,
                timeSeconds = Elapsed, distanceMetres = Distance, speedKmh = Speed * 3.6f,
                lane = Lane, score = Score, scoreDelta = delta, multiplier = Multiplier,
                gemsCollected = Gems, gemBalance = GemBalance, gemDelta = gemDelta, duringBoost = duringBoost,
                multiplierBefore = previousMultiplier ?? Multiplier, decisionNumber = decisionNumber,
                multiplierProgress = MultiplierProgress
            });
        }
    }
}
