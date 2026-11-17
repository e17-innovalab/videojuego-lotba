using UnityEngine;

namespace Lotba.Game
{
    internal readonly struct TrackContact
    {
        public const float SurfaceTolerance = .01f;
        public readonly float StartX, EndX, StartHeight, FreeEndHeight;
        public readonly float LandingFraction, LandingHeight, EndHeight;

        public TrackContact(float startX, float endX, float startHeight, float freeEndHeight,
            float landingFraction = 1f, float landingHeight = 0f, float? endHeight = null)
        {
            StartX = startX;
            EndX = endX;
            StartHeight = startHeight;
            FreeEndHeight = freeEndHeight;
            LandingFraction = landingFraction;
            LandingHeight = landingHeight;
            EndHeight = endHeight ?? freeEndHeight;
        }

        public float XAt(float fraction) => Mathf.Lerp(StartX, EndX, fraction);

        public float HeightAt(float fraction)
        {
            if (fraction >= 1f) return EndHeight;
            if (LandingFraction >= 1f) return Mathf.Lerp(StartHeight, FreeEndHeight, fraction);
            if (fraction < LandingFraction)
                return Mathf.Lerp(StartHeight, LandingHeight, fraction / LandingFraction);
            return Mathf.Lerp(LandingHeight, EndHeight, (fraction - LandingFraction) / (1f - LandingFraction));
        }

        public bool TryHit(float from, float to, float solidHeight, out float fraction)
        {
            float limit = solidHeight - SurfaceTolerance;
            float split = Mathf.Clamp(LandingFraction, from, to);
            return TryEnterBelow(from, split, limit, out fraction) || TryEnterBelow(split, to, limit, out fraction);
        }

        private bool TryEnterBelow(float from, float to, float limit, out float fraction)
        {
            fraction = from;
            float first = HeightAt(from);
            if (first < limit) return true;
            float last = HeightAt(to);
            if (last >= limit) return false;
            fraction = Mathf.Lerp(from, to, Mathf.InverseLerp(first, last, limit));
            return true;
        }

        public static bool Clip(float first, float last, float min, float max, ref float enter, ref float exit)
        {
            float delta = last - first;
            if (Mathf.Abs(delta) < .00001f) return first >= min && first <= max && enter <= exit;
            float a = (min - first) / delta, b = (max - first) / delta;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            exit = Mathf.Min(exit, Mathf.Max(a, b));
            return enter <= exit;
        }
    }
}
