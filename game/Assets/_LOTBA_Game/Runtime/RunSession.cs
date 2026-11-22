using System;

namespace Lotba.Game
{
    public enum RunState { Ready, Running, Paused, Finished }
    public enum RunEnding { None, Brake, RouteComplete = 3, Overload }

    /// <summary>Run state only: no cards, menus, physics, or scene object ownership.</summary>
    public sealed partial class RunSession
    {
        private readonly GameSettings settings;
        private readonly RouteDefinition telemetryRoute;
        private int telemetrySection;
        public RunTelemetry Telemetry { get; private set; }
        private const float ImpactRecoverySeconds = 2.5f;
        private const float ImpactSpeedLoss = 9f;
        private const float ImpactProtectionSeconds = 1.1f;
        private float impactTime = float.NegativeInfinity;
        private float protectedUntil;
        private int nextDecisionSection;
        private float decisionSectionStart;
        private float previousOfferDistance;
        public TurboParameters ManualTurbo { get; }
        public TurboParameters SpeedOfferTurbo { get; }
        public TurboParameters RecoveryTurbo { get; }
        private TurboParameters activeTurbo;
        public float BoostSpeedBonus => BoostActive ? activeTurbo.SpeedBonus : 0f;
        public float BoostTimeLeft => turbo.TimeLeft(Elapsed);
        public int BoostGemMultiplier => BoostActive ? activeTurbo.GemMultiplier : 1;
        public int BoostPointMultiplier => BoostActive ? activeTurbo.PointMultiplier : 1;
        public const float RiskSpeedForFullIntensity = 5f;
        private readonly TurboState turbo = new TurboState();
        public float TurboChargeProgress => turbo.ChargeProgress(settings.TurboChargeGems);
        public bool CanActivateTurbo => State == RunState.Running && !OfferPending &&
            !BoostActive && !RecoveryActive && !RecoveryPreparing && !RecoveryRejoining && TurboChargeProgress >= 1f;
        public int ManualBoosts { get; private set; }
        public int RecoveryBoosts { get; private set; }
        public int RecoveryAttempts { get; private set; }
        private bool speedOfferPending;
        public bool OfferPending => speedOfferPending || RecoveryOfferPending || RecoveryImpactPending;
        public bool BoostActive => turbo.IsActive(Elapsed);
        public float BoostProgress => turbo.Progress(Elapsed);
        public int VoluntaryBoosts { get; private set; }
        public int SpeedUpgrades { get; private set; }
        public int GemsSpent { get; private set; }
        public float PurchasedSpeed { get; private set; }
        public int UpgradeCost { get; private set; }
        public float OfferedSpeedIncrease { get; private set; }
        public bool CanBuySpeed => GemBalance >= UpgradeCost && OfferedSpeedIncrease > 0f;
        private float DecisionBoostSpeed => RiskSpeedForFullIntensity * Math.Min(1f, (VoluntaryBoosts - ManualBoosts) / 3f);
        public float AccumulatedRisk => Math.Min(1f, (PurchasedSpeed + DecisionBoostSpeed) / RiskSpeedForFullIntensity);
        public int BonusGems { get; private set; }
        public float OfferDistance { get; private set; }
        public int DecisionsShown { get; private set; }
        public int DecisionsKept { get; private set; }
        public bool HasDecisionProgress => OfferDistance > previousOfferDistance;
        public float OfferProgress => HasDecisionProgress
            ? Math.Min(1f, (Distance - previousOfferDistance) / (OfferDistance - previousOfferDistance))
            : 0f;
        public RunState State { get; private set; }
        public RunEnding Ending { get; private set; }
        public float Elapsed { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float PeakSpeed { get; private set; }
        public long Score { get; private set; }
        public int Gems { get; private set; }
        public int GemsLost { get; private set; }
        public int GemBalance => Gems - GemsLost - GemsSpent + GemsRecovered;
        public int BoostCollisions { get; private set; }
        public int Collisions { get; private set; }
        public int Lane { get; private set; } = 1;
        private readonly float finishDistance;
        public bool IsProtected => Elapsed < protectedUntil;
        public float ProtectionProgress => IsProtected
            ? Math.Max(0f, 1f - (protectedUntil - Elapsed) / ImpactProtectionSeconds)
            : 1f;

        public RunSession(GameSettings settings, RouteDefinition telemetryRoute = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.telemetryRoute = telemetryRoute;
            BrakingConfiguration = settings.Braking.Capture();
            braking = new BrakingState(BrakingConfiguration);
            ManualTurbo = settings.ManualTurbo.Capture();
            SpeedOfferTurbo = settings.SpeedOfferTurbo.Capture();
            RecoveryTurbo = settings.RecoveryTurbo.Capture();
            finishDistance = telemetryRoute?.FinishDistance ?? 0f;
            Speed = PeakSpeed = settings.InitialSpeed;
        }
        public void Start()
        {
            if (State != RunState.Ready) return;
            BrakingConfiguration = settings.Braking.Capture();
            braking = new BrakingState(BrakingConfiguration);
            State = RunState.Running;
            if (telemetryRoute == null) return;
            Telemetry = new RunTelemetry
            {
                gameVersion = UnityEngine.Application.version,
                route = telemetryRoute.name,
                seed = settings.Seed,
                routeDistanceMetres = finishDistance,
                braking = RunBrakingConfiguration.From(BrakingConfiguration),
                startedAtUtc = DateTime.UtcNow.ToString("O")
            };
            ScheduleNextSpeedOffer();
            Record("run_started");
        }
#if UNITY_EDITOR
        /// <summary>Held turbo for artist playback only; never changes a tracked run.</summary>
        public void SetPreviewTurbo(bool active)
        {
            if (Telemetry != null) return;
            if (active)
            {
                activeTurbo = ManualTurbo;
                turbo.Activate(Elapsed, activeTurbo.DurationSeconds);
            }
            else turbo.Stop();
        }

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
            float step = dt;
            Elapsed += step;
            BeginRecoveryChallengeIfReady();
            float desiredSpeed = CalculateUnbrakedSpeed();
            Speed = braking.IsRestricting ? Math.Min(desiredSpeed, braking.SpeedLimit) : desiredSpeed;
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
            if (finishDistance > 0f && Distance >= finishDistance)
            {
                Distance = finishDistance;
                Finish(RunEnding.RouteComplete);
            }
        }
        public void ShowSpeedOffer()
        {
            if (Telemetry == null || State != RunState.Running || OfferPending || RecoveryActive || RecoveryPreparing || RecoveryRejoining ||
                !HasDecisionProgress || Distance < OfferDistance) return;
            speedOfferPending = true;
            DecisionsShown++;
            Record("speed_offer_shown", decisionNumber: DecisionsShown);
        }

        private void ScheduleNextSpeedOffer()
        {
            RouteSection[] sections = telemetryRoute.Sections;
            while (OfferDistance == 0f && nextDecisionSection < sections.Length)
            {
                int index = nextDecisionSection++;
                RouteSection section = sections[index];
                float length = section.ModuleCount * RoadStandard.ModuleLength;
                if (telemetryRoute.HasDecision(index))
                {
                    OfferDistance = decisionSectionStart + length * .5f;
                    RouteSection upgrade = telemetryRoute.SpeedOfferAt(index);
                    OfferedSpeedIncrease = upgrade.SpeedUpgradeMetresPerSecond;
                    UpgradeCost = upgrade.SpeedUpgradeCost;
                }
                decisionSectionStart += length;
            }
        }

        public void ChooseSpeed(bool accelerate)
        {
            if (!speedOfferPending || State != RunState.Running) return;
            if (accelerate && UpgradeCost > 0 && !CanBuySpeed) return;
            speedOfferPending = false;
            previousOfferDistance = Distance;
            OfferDistance = 0f;
            if (!accelerate)
            {
                DecisionsKept++;
                Record("speed_kept", decisionNumber: DecisionsShown);
            }
            else if (UpgradeCost > 0)
            {
                GemsSpent += UpgradeCost;
                PurchasedSpeed += OfferedSpeedIncrease;
                SpeedUpgrades++;
                Record("speed_upgrade_purchased", gemDelta: -UpgradeCost, decisionNumber: DecisionsShown);
            }
            else
            {
                ActivateBoost(null, DecisionsShown);
            }
            ScheduleNextSpeedOffer();
        }

        public bool TryActivateTurbo()
        {
            if (!CanActivateTurbo) return false;
            ActivateBoost("manual_button");
            return true;
        }

        private void ActivateBoost(string source, int decisionNumber = 0, bool recovery = false)
        {
            turbo.ClearCharge();
            activeTurbo = recovery ? RecoveryTurbo : source == "manual_button" ? ManualTurbo : SpeedOfferTurbo;
            turbo.Activate(Elapsed, activeTurbo.DurationSeconds, requiresExplicitStop: recovery);
            if (recovery) RecoveryBoosts++;
            else VoluntaryBoosts++;
            if (source == "manual_button") ManualBoosts++;
            Record("boost_activated", source, decisionNumber: decisionNumber);
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
        public int PinkGemStreak { get; private set; }
        public int ScoreMultiplier => Math.Max(1, PinkGemStreak);
        public int PinkGemsCollected { get; private set; }
        public int PinkGemsMissed { get; private set; }
        public int BestPinkGemStreak { get; private set; }

        public int PinkGemNearMisses { get; private set; }
        public int PinkDodgeCleared { get; private set; }
        public int PinkDodgeCollisions { get; private set; }

        public bool MissPinkGem(bool nearMiss, int pinkDodgeEncounter = 0)
        {
            if (State != RunState.Running) return false;
            PinkGemsMissed++;
            if (nearMiss) PinkGemNearMisses++;
            int lostStreak = PinkGemStreak;
            PinkGemStreak = 0;
            Record("pink_gem_missed", streakBefore: lostStreak, pinkGemNearMiss: nearMiss,
                pinkDodgeEncounter: pinkDodgeEncounter);
            return lostStreak > 0;
        }

        public long CollectGem(int airborneNote = -1, bool pink = false, int pinkDodgeEncounter = 0)
        {
            if (State != RunState.Running) return 0;
            int gemCount = BoostGemMultiplier;
            if (!BoostActive) turbo.AddCharge(settings.TurboChargeGems);
            if (pink)
            {
                PinkGemsCollected++;
                PinkGemStreak++;
                BestPinkGemStreak = Math.Max(BestPinkGemStreak, PinkGemStreak);
            }
            long reward = 10L * BoostPointMultiplier * ScoreMultiplier;
            Score += reward; Gems += gemCount; BonusGems += gemCount - 1;
            Record("gem_collected", airborneNote >= 0 ? "air" : "ground", reward,
                gemDelta: gemCount);
            if (gemCount > 1) Record("boost_bonus_gem");
            if (pink) Record("pink_gem_collected", pinkDodgeEncounter: pinkDodgeEncounter);
            return reward;
        }
        public void ClearPinkDodge(int encounter)
        {
            if (State != RunState.Running) return;
            PinkDodgeCleared++;
            Record("pink_dodge_cleared", pinkDodgeEncounter: encounter);
        }

        public long Hit(string obstacleId = null, int pinkDodgeEncounter = 0)
        {
            if (State != RunState.Running || IsProtected) return 0;
            int lostStreak = PinkGemStreak;
            PinkGemStreak = 0;
            turbo.ClearCharge();
            bool duringBoost = BoostActive;
            int divisor = duringBoost ? 2 : 4;
            int gemLoss = GemBalance / divisor + (GemBalance % divisor == 0 ? 0 : 1);
            long loss = duringBoost ? 0 : Math.Min(Score, Math.Max(50L, Score / 4));
            GemsLost += gemLoss;
            Score -= loss; Collisions++;
            if (duringBoost)
            {
                BoostCollisions++;
                turbo.Stop();
            }
            impactTime = Elapsed;
            protectedUntil = impactTime + ImpactProtectionSeconds;
            if (pinkDodgeEncounter > 0) PinkDodgeCollisions++;
            Record("obstacle_hit", obstacleId, -loss, gemDelta: -gemLoss, duringBoost: duringBoost,
                pinkDodgeEncounter: pinkDodgeEncounter, streakBefore: lostStreak);
            OnRecoveryImpact(gemLoss);
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

        public void EndForOverload()
        {
            if (State == RunState.Running) Finish(RunEnding.Overload);
        }

        private void Finish(RunEnding ending)
        {
            InterruptRecovery(ending);
            Ending = ending;
            State = RunState.Finished;
            speedOfferPending = false;
            if (Telemetry != null)
            {
                Telemetry.finishedAtUtc = DateTime.UtcNow.ToString("O");
                string reason = ending switch
                {
                    RunEnding.Brake => "brake",
                    RunEnding.Overload => "overload",
                    _ => "route_complete"
                };
                Record("run_finished", reason);
                Telemetry.summary = new RunTelemetrySummary
                {
                    ending = reason, timeSeconds = Elapsed, distanceMetres = Distance, peakSpeedKmh = PeakSpeed * 3.6f,
                    score = Score, gems = Gems, collisions = Collisions,
                    decisionsShown = DecisionsShown, decisionsKept = DecisionsKept,
                    voluntaryBoosts = VoluntaryBoosts, manualBoosts = ManualBoosts, recoveryBoosts = RecoveryBoosts, bonusGems = BonusGems,
                    recoveryAttempts = RecoveryAttempts,
                    brakePresses = BrakePresses, brakeReleases = BrakeReleases, brakeHeldSeconds = BrakeHeldSeconds,
                    gemsLost = GemsLost, gemsRecovered = GemsRecovered, gemBalance = GemBalance, boostCollisions = BoostCollisions,
                    pinkGemsCollected = PinkGemsCollected, pinkGemsMissed = PinkGemsMissed,
                    pinkGemNearMisses = PinkGemNearMisses,
                    pinkDodgeCleared = PinkDodgeCleared, pinkDodgeCollisions = PinkDodgeCollisions,
                    bestPinkGemStreak = BestPinkGemStreak,
                    gemsSpent = GemsSpent, speedUpgrades = SpeedUpgrades, purchasedSpeedKmh = PurchasedSpeed * 3.6f
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
            int gemDelta = 0, bool duringBoost = false, int decisionNumber = 0, int streakBefore = 0,
            bool pinkGemNearMiss = false, int pinkDodgeEncounter = 0)
        {
            if (Telemetry == null) return;
            Telemetry.events.Add(new RunTelemetryEvent
            {
                sequence = Telemetry.events.Count + 1, type = type, detail = detail,
                zone = telemetryRoute.Sections[telemetrySection].Map.DisplayName,
                timeSeconds = Elapsed, distanceMetres = Distance, speedKmh = Speed * 3.6f,
                lane = Lane, score = Score, scoreDelta = delta,
                gemsCollected = Gems, gemBalance = GemBalance, gemDelta = gemDelta, duringBoost = duringBoost,
                decisionNumber = decisionNumber,
                pinkGemStreak = PinkGemStreak, scoreMultiplier = ScoreMultiplier, pinkGemStreakBefore = streakBefore,
                pinkGemNearMiss = pinkGemNearMiss,
                pinkDodgeEncounter = pinkDodgeEncounter,
                purchasedSpeedKmh = PurchasedSpeed * 3.6f,
                turboConfiguration = type == "boost_activated" ? RunTurboConfiguration.From(activeTurbo) : null
            });
        }
    }
}
