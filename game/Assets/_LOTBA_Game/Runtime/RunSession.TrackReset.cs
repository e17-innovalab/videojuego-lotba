using System;

namespace Lotba.Game
{
    public sealed partial class RunSession
    {
        /// <summary>Record only after the world has confirmed clearing and repositioning. Not a player lane change.</summary>
        public void RecordTrackReset(TrackClearanceResult result, int targetLane, string reason)
        {
            if (State != RunState.Running && State != RunState.Paused) return;
            Lane = Math.Max(0, Math.Min(2, targetLane));
        }
    }
}
