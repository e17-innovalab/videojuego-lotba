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

            // Mayor espaciado y menor frecuencia según pedido del usuario:
            // espaciado aumentado a 3.4m (antes 2.5m) e intervalo base a 46m (antes 24m)
            float spacing = 3.4f;
            float gemInterval = 46f;
            float gemIntensity = Mathf.Max(0.25f, context.Rules.GemIntensity * context.GemIntensity);

            // Generación de un ÚNICO camino de gemas continuo (sin hileras paralelas en múltiples carriles)
            while (nextGemDistance < context.SpawnHorizon)
            {
                if (scheduler.IsGemForbiddenAt(nextGemDistance))
                {
                    nextGemDistance += spacing * 4f;
                    continue;
                }

                // 1. Si hay una superficie transitable (rampa/techo) en esta cota, el único camino de gemas sube por ella
                if (scheduler.TryGetSurfaceAt(nextGemDistance, out TraversableSurface surface))
                {
                    if (!populatedSurfaces.Contains(surface.StartZ))
                    {
                        populatedSurfaces.Add(surface.StartZ);
                        SpawnSurfaceTrail(surface, spacing, scheduler);
                    }
                    nextGemDistance = surface.EndZ + 12f;
                    continue;
                }

                // 2. Si hay un arco parabólico de salto en esta cota, el único camino de gemas describe la parábola en el aire
                if (scheduler.TryGetJumpArcAt(nextGemDistance, out JumpArcTrajectory jumpArc))
                {
                    if (!populatedArcs.Contains(jumpArc.StartZ))
                    {
                        populatedArcs.Add(jumpArc.StartZ);
                        SpawnParabolicArc(jumpArc, scheduler);
                    }
                    nextGemDistance = jumpArc.EndZ + 12f;
                    continue;
                }

                // 3. Generación en piso: exactamente un único camino de gemas siguiendo el carril seguro
                int count = 4;
                int safeLane = scheduler.GetSafeLaneAt(nextGemDistance, -1);

                gemRowsSincePink++;
                bool pinkAllowed = context.PinkGemsUnlocked && context.Rules.AllowPinkGems;
                bool isPink = pinkAllowed && context.Profile != null && context.Profile.PinkGemEveryRows > 0 &&
                             gemRowsSincePink >= context.Profile.PinkGemEveryRows;

                if (isPink)
                {
                    gemRowsSincePink = 0;
                }

                for (int i = 0; i < count; i++)
                {
                    float z = nextGemDistance + i * spacing;

                    if (scheduler.IsGemForbiddenAt(z) || scheduler.IsLetterAt(z)) continue;

                    // Si hay una superficie o arco de salto en cualquier carril a esta cota, no colocar gemas de piso para no duplicar caminos
                    if (scheduler.TryGetSurfaceAt(z, out _) || scheduler.TryGetJumpArcAt(z, out _)) break;

                    int gemLane = scheduler.GetSafeLaneAt(z, safeLane);

                    // NUNCA generar una gema si hay cualquier obstáculo (incluso cajas pequeñas o conos) en ese carril y cota
                    if (scheduler.HasObstacleAt(gemLane, z, 1.1f, 1.0f)) continue;

                    bool itemPink = isPink && (i == count - 1);
                    scheduler.EmitItem(TrackSpawnItem.CreateGem(gemLane, z, 1.1f, pink: itemPink));
                }

                float jitter = 1f + (float)(random.NextDouble() * 0.15 - 0.05);
                float step = Mathf.Max(spacing * count + 14f, (gemInterval * jitter) / gemIntensity);
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
