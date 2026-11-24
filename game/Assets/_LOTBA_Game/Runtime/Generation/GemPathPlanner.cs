using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Rules for planning collectible trails across connected elevated surfaces and jumps.
    /// Keeps reachability decisions out of the obstacle and gem placement code.
    /// </summary>
    public static class GemPathPlanner
    {
        public static float ElevatedSpacing(float speed, EncounterProfile profile)
        {
            float baseSpacing = profile != null ? profile.GemSpacing : TrackScheduler.GridStep;
            float collectionSeconds = profile != null ? profile.AerialGemCollectionIntervalSeconds : .15f;
            return Mathf.Max(TrackScheduler.GridStep, baseSpacing, Mathf.Max(0f, speed) * collectionSeconds);
        }

        public static float FirstSurfaceGem(TraversableSurface surface,
            IReadOnlyList<JumpArcTrajectory> arcs, float spacing, float landingBuffer)
        {
            float firstGem = Mathf.Ceil((surface.StartZ + 1.5f) / spacing) * spacing;
            if (arcs == null) return firstGem;

            for (int i = 0; i < arcs.Count; i++)
            {
                JumpArcTrajectory arc = arcs[i];
                if (arc.ToLane != surface.Lane || arc.EndZ < surface.StartZ || arc.EndZ > surface.EndZ)
                    continue;

                float roofHeightAtLanding = surface.HeightAt(arc.EndZ) + 1.1f;
                if (Mathf.Abs(arc.EndHeight - roofHeightAtLanding) > .5f) continue;
                firstGem = Mathf.Max(firstGem, arc.EndZ + Mathf.Max(0f, landingBuffer));
            }

            return firstGem;
        }
    }
}
