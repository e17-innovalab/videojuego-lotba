using System;

namespace Lotba.Game
{
    public enum RunState { Ready, Running, Paused, Finished }
    public enum RunEnding { None, Brake, TimeLimit }

    /// <summary>Run state only: no cards, menus, physics, or scene object ownership.</summary>
    public sealed class RunSession
    {
        private readonly GameSettings settings;
        private float impactDrag;
        private float protectedUntil;
        private float streakEnergy;
        public RunState State { get; private set; }
        public RunEnding Ending { get; private set; }
        public float Elapsed { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float PeakSpeed { get; private set; }
        public long Score { get; private set; }
        public long CoinValue => Coins * 10L;
        public int Coins { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int Collisions { get; private set; }
        public int Lane { get; private set; } = 1;
        public int Multiplier { get; private set; } = 1;
        public int BestMultiplier { get; private set; } = 1;
        public float StreakProgress => streakEnergy;
        public float Remaining => Math.Max(0f, settings.Duration - Elapsed);
        public bool IsProtected => Elapsed < protectedUntil;

        public RunSession(GameSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Speed = PeakSpeed = settings.InitialSpeed;
        }
        public void Start() { if (State == RunState.Ready) State = RunState.Running; }
        public void Tick(float dt)
        {
            if (State != RunState.Running) return;
            float step = Math.Min(dt, Remaining);
            Elapsed += step;
            impactDrag = Math.Max(0f, impactDrag - step * 3f);
            float progression = 1f - (float)Math.Exp(-Elapsed / settings.AccelerationTime);
            Speed = settings.InitialSpeed + (settings.MaximumSpeed - settings.InitialSpeed) * progression - impactDrag;
            PeakSpeed = Math.Max(PeakSpeed, Speed);
            Distance += Speed * step;
            if (Streak > 0)
            {
                streakEnergy = Math.Max(0, streakEnergy - settings.StreakDrainPerSecond * step);
                if (streakEnergy <= 0) ResetStreak();
            }
            if (Remaining <= 0f) Finish(RunEnding.TimeLimit);
        }
        public bool Move(int direction)
        {
            if (State != RunState.Running) return false;
            int lane = Math.Max(0, Math.Min(2, Lane + Math.Sign(direction)));
            if (Lane == lane) return false;
            Lane = lane; return true;
        }
        public long CollectCoin()
        {
            if (State != RunState.Running) return 0;
            long reward = 10L * Multiplier;
            Score += reward; Coins++; Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
            float coinsForLevel = settings.FirstLevelCoins + settings.ExtraCoinsPerLevel * (Multiplier - 1f);
            streakEnergy += 1f / Math.Max(1, coinsForLevel);
            if (streakEnergy >= 1)
            {
                Multiplier++;
                BestMultiplier = Math.Max(BestMultiplier, Multiplier);
                // Leave a reserve after levelling up; an empty bar must always mean a lost streak.
                streakEnergy = Math.Min(1, settings.LevelUpReserve + streakEnergy - 1);
            }
            return reward;
        }
        public long Hit()
        {
            if (State != RunState.Running || IsProtected) return 0;
            long loss = Math.Min(Score, Math.Max(50L, Score / 4));
            Score -= loss; ResetStreak(); Collisions++;
            impactDrag = 3f; protectedUntil = Elapsed + 1.1f;
            return loss;
        }
        private void ResetStreak() { Streak = 0; streakEnergy = 0; Multiplier = 1; }
        public void Pause() { if (State == RunState.Running) State = RunState.Paused; }
        public void Resume() { if (State == RunState.Paused) State = RunState.Running; }
        public void Brake()
        {
            if (State == RunState.Running || State == RunState.Paused) Finish(RunEnding.Brake);
        }
        private void Finish(RunEnding ending) { Ending = ending; State = RunState.Finished; Speed = 0f; }
    }
}
