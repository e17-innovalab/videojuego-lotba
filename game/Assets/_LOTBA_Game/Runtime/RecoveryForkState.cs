using System;

namespace Lotba.Game
{
    /// <summary>
    /// Tracks the recovery fork without knowing about scenes, sessions, UI or input.
    /// The caller schedules a fork ahead of the rider, checks whether it is active,
    /// and resolves it by lane when the rider passes through.
    /// </summary>
    public sealed class RecoveryForkState
    {
        public bool Active { get; private set; }
        public float Distance { get; private set; }
        public int RecoveryLane { get; private set; }
        public int ContinueLane => RecoveryLane == 0 ? 2 : 0;
        public long OfferedPoints { get; private set; }
        public int OfferedGems { get; private set; }

        public void Begin(float distance, int recoveryLane, long offeredPoints, int offeredGems)
        {
            Active = true;
            Distance = distance;
            RecoveryLane = recoveryLane;
            OfferedPoints = offeredPoints;
            OfferedGems = offeredGems;
        }

        /// <summary>True if the rider is at or past the fork distance and should be resolved.</summary>
        public bool ShouldResolve(float riderDistance) => Active && riderDistance >= Distance;

        /// <summary>
        /// Resolves the fork by lane. Returns true if the rider chose recovery.
        /// Clears the fork state regardless of the outcome.
        /// </summary>
        public bool Resolve(int playerLane)
        {
            if (!Active) return false;
            bool choseRecovery = playerLane == RecoveryLane;
            Active = false;
            return choseRecovery;
        }

        public void Stop() => Active = false;
    }
}
