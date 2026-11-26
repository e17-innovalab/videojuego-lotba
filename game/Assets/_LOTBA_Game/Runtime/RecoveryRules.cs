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
        private sealed class SectionLoss
        {
            public long points;
            public int gems;
            public readonly List<int> sequences = new List<int>();
        }

        private readonly Dictionary<int, SectionLoss> sections = new Dictionary<int, SectionLoss>();
        public bool HasLossIn(int section)
        {
            return sections.TryGetValue(section, out SectionLoss loss) && (loss.points > 0 || loss.gems > 0);
        }
        public long PendingPointsIn(int section) => sections.TryGetValue(section, out SectionLoss loss) ? loss.points : 0;
        public int PendingGemsIn(int section) => sections.TryGetValue(section, out SectionLoss loss) ? loss.gems : 0;

        public int[] SnapshotLossSequences(int section) => sections.TryGetValue(section, out SectionLoss loss)
            ? loss.sequences.ToArray()
            : Array.Empty<int>();

        public void Add(long lostPoints, int lostGems, int sequence, int section)
        {
            if (!sections.TryGetValue(section, out SectionLoss loss))
                sections.Add(section, loss = new SectionLoss());
            loss.points += Math.Max(0, lostPoints);
            loss.gems += Math.Max(0, lostGems);
            if ((lostPoints > 0 || lostGems > 0) && sequence > 0) loss.sequences.Add(sequence);
        }

        public ResourceAmount Consume(int section, out int[] sequences)
        {
            if (!sections.TryGetValue(section, out SectionLoss loss))
            {
                sequences = Array.Empty<int>();
                return new ResourceAmount(0, 0);
            }
            sections.Remove(section);
            sequences = loss.sequences.ToArray();
            return new ResourceAmount(loss.points, loss.gems);
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
        [UnityEngine.Range(0f, 1f)] public float pointFraction = .5f;
        [UnityEngine.Min(0)] public int minimumPointLoss = 50;
        [UnityEngine.Range(0f, 1f)] public float gemFraction = .5f;
        public ResourceAmount Loss(long points, int gems) => new ResourceAmount(
            Math.Min(points, Math.Max(minimumPointLoss, (long)(points * pointFraction))),
            Math.Min(gems, (int)Math.Ceiling(gems * gemFraction)));
    }
}
