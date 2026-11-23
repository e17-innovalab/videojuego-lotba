using System;
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
        private System.Random random;

        public void Reset(int baseSeed)
        {
            random = new System.Random(baseSeed ^ 0x71F3);
            nextGemDistance = 20f;
            gemRowsSincePink = 0;
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

            while (nextGemDistance < context.SpawnHorizon)
            {
                // Verificar si esta distancia cae dentro de una zona que prohíbe gemas (ej. zona de recupero)
                if (scheduler.IsGemForbiddenAt(nextGemDistance))
                {
                    nextGemDistance += spacing * 4f;
                    continue;
                }

                // 1. ¿Hay un arco parabólico de salto en esta distancia?
                if (scheduler.TryGetJumpArcAt(nextGemDistance, out JumpArcTrajectory jumpArc))
                {
                    SpawnParabolicArc(jumpArc, scheduler);
                    nextGemDistance = jumpArc.EndZ + spacing * 3f;
                    continue;
                }

                // 2. ¿Hay una superficie transitable (techo/rampa) en esta distancia?
                if (scheduler.TryGetSurfaceAt(nextGemDistance, out TraversableSurface surface))
                {
                    SpawnSurfaceTrail(surface, spacing, scheduler);
                    nextGemDistance = surface.EndZ + spacing * 2f;
                    continue;
                }

                // 3. Generación normal en piso siguiendo el camino seguro
                int count = context.Profile != null ? context.Profile.GemsPerThread : 4;
                int lane = scheduler.GetSafeLaneAt(nextGemDistance, -1);

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
                    if (scheduler.IsGemForbiddenAt(z)) continue;

                    bool itemPink = isPink && (i == count - 1);
                    scheduler.EmitItem(TrackSpawnItem.CreateGem(lane, z, 1.1f, pink: itemPink));
                }

                float jitter = 1f + (float)(random.NextDouble() * 0.2 - 0.1);
                nextGemDistance += Mathf.Max(spacing * count + 6f, (gemInterval * jitter) / gemIntensity);
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
