using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Generador de gemas coordinado.
    /// Puede generar:
    /// 1. Hileras sobre el piso siguiendo el carril seguro (breadcrumb trail).
    /// 2. Hileras sobre techos y rampas (TraversableSurfaces).
    /// 3. Hileras parabólicas de salto en el aire (JumpArcTrajectory).
    /// 4. Gemas rosas periódicas y encuentros de esquive.
    /// Respeta estrictamente las ZoneRules activas.
    /// </summary>
    public sealed class GemSpawner : IContentSpawner
    {
        public string Name => "GemSpawner";
        public float NextRequestedDistance => nextGemDistance;

        private float nextGemDistance;
        private int gemRowsSincePink;
        private readonly HashSet<float> populatedArcs = new HashSet<float>();
        private readonly HashSet<float> populatedSurfaces = new HashSet<float>();
        private System.Random random;

        public void Reset(int baseSeed)
        {
            random = new System.Random(baseSeed ^ 0x71F3);
            nextGemDistance = 20f;
            gemRowsSincePink = 0;
            populatedArcs.Clear();
            populatedSurfaces.Clear();
        }

        public void Populate(in GenerationContext context, TrackScheduler scheduler)
        {
            // Si la zona activa prohíbe gemas comunes (ej. zona de recupero), no generar nada y asegurar que el cursor salte más allá del final de recupero
            if (context.RecoveryActive || context.ActiveZone == ZoneType.Recovery || !context.Rules.AllowCommonGems)
            {
                float safeResumeDistance = Mathf.Max(context.SpawnHorizon, context.RecoveryZoneEnd + 6f);
                nextGemDistance = Mathf.Max(nextGemDistance, safeResumeDistance);
                return;
            }

            float spacing = context.Profile != null ? context.Profile.GemSpacing : 2.5f;
            float gemInterval = context.Profile != null ? context.Profile.GemInterval : 24f;
            float gemIntensity = Mathf.Max(0.25f, context.Rules.GemIntensity * context.GemIntensity);

            // 1. Asegurar que cada arco de salto activo tenga sus gemas parabólicas en el aire
            var activeArcs = scheduler.ActiveJumpArcs;
            for (int i = 0; i < activeArcs.Count; i++)
            {
                var arc = activeArcs[i];
                if (arc.EndZ < context.Distance - 10f || arc.StartZ > context.SpawnHorizon) continue;
                if (!populatedArcs.Contains(arc.StartZ))
                {
                    populatedArcs.Add(arc.StartZ);
                    SpawnParabolicArc(arc, scheduler);
                }
            }

            // 2. Asegurar que cada superficie transitable activa (rampa/techo) tenga sus gemas alineadas
            var activeSurfaces = scheduler.ActiveSurfaces;
            for (int i = 0; i < activeSurfaces.Count; i++)
            {
                var surf = activeSurfaces[i];
                if (surf.EndZ < context.Distance - 10f || surf.StartZ > context.SpawnHorizon) continue;
                if (!populatedSurfaces.Contains(surf.StartZ))
                {
                    populatedSurfaces.Add(surf.StartZ);
                    SpawnSurfaceTrail(surf, spacing, scheduler);
                }
            }

            // 3. Generación continua en piso guiando por el camino seguro (breadcrumbs)
            while (nextGemDistance < context.SpawnHorizon)
            {
                if (scheduler.IsGemForbiddenAt(nextGemDistance))
                {
                    nextGemDistance += spacing * 4f;
                    continue;
                }

                int count = context.Profile != null ? context.Profile.GemsPerThread : 4;
                int baseLane = scheduler.GetSafeLaneAt(nextGemDistance, -1);

                gemRowsSincePink++;
                bool pinkAllowed = context.PinkGemsUnlocked && context.Rules.AllowPinkGems;
                bool isPink = pinkAllowed && context.Profile != null && context.Profile.PinkGemEveryRows > 0 &&
                             gemRowsSincePink >= context.Profile.PinkGemEveryRows;

                if (isPink)
                {
                    gemRowsSincePink = 0;
                }

                int emittedInTrail = 0;
                for (int i = 0; i < count; i++)
                {
                    float z = nextGemDistance + i * spacing;

                    if (scheduler.IsGemForbiddenAt(z) || scheduler.IsLetterAt(z)) continue;

                    int gemLane = scheduler.GetSafeLaneAt(z, baseLane);

                    // NUNCA generar una gema si un obstáculo ocupa ese carril a esa altura
                    if (scheduler.HasObstacleAt(gemLane, z, 1.1f, 0.45f)) continue;

                    // NUNCA generar una gema de piso sobre una rampa o debajo de un colectivo
                    if (scheduler.TryGetSurfaceAt(z, out TraversableSurface surf) && surf.Lane == gemLane) continue;

                    // NUNCA generar una gema de piso debajo de un arco de salto (el arco tiene sus propias gemas aéreas)
                    if (scheduler.TryGetJumpArcAt(z, out JumpArcTrajectory arc) && (arc.FromLane == gemLane || arc.ToLane == gemLane)) continue;

                    bool itemPink = isPink && (i == count - 1);
                    scheduler.EmitItem(TrackSpawnItem.CreateGem(gemLane, z, 1.1f, pink: itemPink));
                    emittedInTrail++;
                }

                float jitter = 1f + (float)(random.NextDouble() * 0.2 - 0.1);
                float step = Mathf.Max(spacing * count + 4f, (gemInterval * jitter) / gemIntensity);
                nextGemDistance += step;
            }
        }

        private void SpawnParabolicArc(JumpArcTrajectory arc, TrackScheduler scheduler)
        {
            // Coloca 5 o 6 gemas describiendo la parábola física en el aire
            int gemCount = Mathf.Clamp(Mathf.RoundToInt(arc.Length / 2.8f), 3, 6);
            for (int i = 0; i < gemCount; i++)
            {
                float t = (i + 1f) / (gemCount + 1f);
                float z = arc.StartZ + t * arc.Length;
                float height = arc.EvaluateHeight(z);
                int lane = t < 0.5f ? arc.FromLane : arc.ToLane;

                scheduler.EmitItem(TrackSpawnItem.CreateGem(lane, z, height, airborneNote: i));
            }
        }

        private void SpawnSurfaceTrail(TraversableSurface surface, float spacing, TrackScheduler scheduler)
        {
            float start = surface.StartZ + 1.5f;
            float end = surface.EndZ - 1.5f;
            if (end <= start) return;

            int count = Mathf.FloorToInt((end - start) / spacing);
            for (int i = 0; i < count; i++)
            {
                float z = start + i * spacing;
                float height = surface.HeightAt(z) + 1.1f;
                scheduler.EmitItem(TrackSpawnItem.CreateGem(surface.Lane, z, height));
            }
        }
    }
}
