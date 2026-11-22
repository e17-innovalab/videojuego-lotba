using System;
using System.Collections.Generic;

namespace Lotba.Game
{
    /// <summary>Half-open interval in absolute route metres, independent of scene objects and game mechanics.</summary>
    public readonly struct TrackSpan
    {
        public readonly float Start, End;
        public TrackSpan(float start, float end)
        {
            if (end <= start) throw new ArgumentOutOfRangeException(nameof(end));
            Start = start; End = end;
        }
        public bool Contains(float point) => point >= Start && point < End;
        public bool Intersects(float start, float end) => end > Start && start < End;
        public static TrackSpan ModulesAt(float distance, float moduleLength, int count)
        {
            if (moduleLength <= 0f || count <= 0) throw new ArgumentOutOfRangeException(nameof(moduleLength));
            float start = (float)Math.Floor(Math.Max(0f, distance) / moduleLength) * moduleLength;
            return new TrackSpan(start, start + moduleLength * count);
        }
    }

    /// <summary>Generic no-spawn reservations. Consumers check occupancy before placing route content.</summary>
    public sealed class TrackReservations
    {
        private readonly List<TrackSpan> spans = new List<TrackSpan>();
        public void Clear() => spans.Clear();
        public void Reserve(TrackSpan span)
        {
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                var existing = spans[i];
                if (existing.End < span.Start || existing.Start > span.End) continue;
                span = new TrackSpan(Math.Min(existing.Start, span.Start), Math.Max(existing.End, span.End));
                spans.RemoveAt(i);
            }
            spans.Add(span);
        }
        public bool Blocks(float center, float halfLength = 0f)
        {
            foreach (var span in spans)
                if (halfLength > 0f ? span.Intersects(center - halfLength, center + halfLength) : span.Contains(center)) return true;
            return false;
        }
        public float ExitOverlapping(float start, float end)
        {
            float exit = float.NegativeInfinity;
            foreach (var span in spans)
                if (span.Intersects(start, end)) exit = Math.Max(exit, span.End);
            return exit;
        }
        public float ExitContaining(float point)
        {
            float exit = point;
            foreach (var span in spans) if (span.Contains(point)) exit = Math.Max(exit, span.End);
            return exit;
        }
    }

    public readonly struct TrackClearanceResult
    {
        public readonly TrackSpan Span;
        public readonly int Obstacles, Gems, Letters, Platforms, SurfaceViews;
        public TrackClearanceResult(TrackSpan span, int obstacles, int gems, int letters, int platforms, int surfaceViews)
        {
            Span = span; Obstacles = obstacles; Gems = gems; Letters = letters;
            Platforms = platforms; SurfaceViews = surfaceViews;
        }
    }
}
