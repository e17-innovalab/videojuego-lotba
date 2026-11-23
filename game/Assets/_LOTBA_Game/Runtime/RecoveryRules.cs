using System;
using System.Collections.Generic;

namespace Lotba.Game
{
    /// <summary>Resource accounting, with no knowledge of scenes, sessions or UI.</summary>
    public readonly struct ResourceAmount
    {
        public readonly long Points;
        public readonly int Gems;
        public bool Any => Points > 0 || Gems > 0;
        public ResourceAmount(long points, int gems) { Points = Math.Max(0, points); Gems = Math.Max(0, gems); }
    }

    public sealed class SectionLossLedger
    {
        private long points;
        private int gems;
        private readonly List<int> lossSequences = new List<int>();
        public bool HasLoss => points > 0 || gems > 0;
        public long PendingPoints => points;
        public int PendingGems => gems;
        public void Add(long lostPoints, int lostGems, int sequence)
        {
            points += Math.Max(0, lostPoints);
            gems += Math.Max(0, lostGems);
            if ((lostPoints > 0 || lostGems > 0) && sequence > 0) lossSequences.Add(sequence);
        }
        /// <summary>Each loss belongs to one opportunity only, including declined opportunities.</summary>
        public ResourceAmount Consume(out int[] sequences)
        {
            var result = new ResourceAmount(points, gems);
            sequences = lossSequences.ToArray(); lossSequences.Clear();
            points = 0; gems = 0;
            return result;
        }
    }

    /// <summary>Distance exposure only. The caller resolves contacts before checking completion.</summary>
    public sealed class DistanceChallenge
    {
        public float Start { get; private set; }
        public float Length { get; private set; }
        public bool Active { get; private set; }
        public void Begin(float start, float length) { Start = start; Length = Math.Max(1f, length); Active = true; }
        public float Progress(float distance) => Math.Max(0f, Math.Min(1f, (distance - Start) / Math.Max(1f, Length)));
        public bool Complete(float distance) => Active && distance >= Start + Length;
        public void Stop() => Active = false;
    }

    [Serializable]
    public sealed class RecoveryCollisionPolicy
    {
        [UnityEngine.Range(0f, 1f)] public float pointFraction = .25f;
        [UnityEngine.Min(0)] public int minimumPointLoss = 50;
        [UnityEngine.Range(0f, 1f)] public float gemFraction = .5f;
        public ResourceAmount Loss(long points, int gems) => new ResourceAmount(
            Math.Min(points, Math.Max(minimumPointLoss, (long)(points * pointFraction))),
            Math.Min(gems, (int)Math.Ceiling(gems * gemFraction)));
    }
}
