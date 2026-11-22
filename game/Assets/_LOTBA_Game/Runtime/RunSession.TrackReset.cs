using System;

namespace Lotba.Game
{
    public sealed partial class RunSession
    {
        public int TrackResets { get; private set; }
        public int ClearedObstacles { get; private set; }
        public int ClearedGems { get; private set; }
        public int ClearedLetters { get; private set; }
        public int ClearedPlatforms { get; private set; }

        /// <summary>Record only after the world has confirmed clearing and repositioning. Not a player lane change.</summary>
        public void RecordTrackReset(TrackClearanceResult result, int targetLane, string reason)
        {
            if (State != RunState.Running && State != RunState.Paused) return;
            int previous = Lane;
            Lane = Math.Max(0, Math.Min(2, targetLane));
            TrackResets++;
            ClearedObstacles += result.Obstacles; ClearedGems += result.Gems;
            ClearedLetters += result.Letters; ClearedPlatforms += result.Platforms;
            Record("track_reset", reason, trackReset: RunTrackReset.From(result, previous, Lane));
        }
    }
}
