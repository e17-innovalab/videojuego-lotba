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
            nextGemDistance = 12f;
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

            // 1. Población prioritaria de superficies elevadas registradas (techos/rampas)
            for (int i = 0; i < scheduler.ActiveSurfaces.Count; i++)
            {
                var surface = scheduler.ActiveSurfaces[i];
                if (surface.StartZ < context.SpawnHorizon && populatedSurfaces.Add(surface.StartZ))
                {
                    SpawnSurfaceTrail(surface, TrackScheduler.GridStep, scheduler);
                }
            }

            // 2. Población prioritaria de arcos parabólicos de salto registrados (vallas/cajas bajas y descensos)
            for (int i = 0; i < scheduler.ActiveJumpArcs.Count; i++)
            {
                var arc = scheduler.ActiveJumpArcs[i];
                if (arc.StartZ < context.SpawnHorizon && populatedArcs.Add(arc.StartZ))
                {
                    SpawnParabolicArc(arc, scheduler);
                }
            }

            // 3. Generación en piso de un ÚNICO camino de gemas continuo, espaciado y alineado a la grilla de 3m
            float gemInterval = 48f;
            float gemIntensity = Mathf.Max(0.25f, context.Rules.GemIntensity * context.GemIntensity);
            nextGemDistance = Mathf.Round(nextGemDistance / TrackScheduler.GridStep) * TrackScheduler.GridStep;

            while (nextGemDistance < context.SpawnHorizon)
            {
                if (scheduler.IsGemForbiddenAt(nextGemDistance))
                {
                    nextGemDistance += TrackScheduler.GridStep * 4f;
                    continue;
                }

                // Si hay una superficie transitable o arco de salto activo en esta cota, el camino ya fue cubierto arriba; saltar más allá
                if (scheduler.TryGetSurfaceAt(nextGemDistance, out TraversableSurface surface))
                {
                    nextGemDistance = Mathf.Ceil((surface.EndZ + 6.0f) / TrackScheduler.GridStep) * TrackScheduler.GridStep;
                    continue;
                }

                if (scheduler.TryGetJumpArcAt(nextGemDistance, out JumpArcTrajectory jumpArc))
                {
                    nextGemDistance = Mathf.Ceil((jumpArc.EndZ + 6.0f) / TrackScheduler.GridStep) * TrackScheduler.GridStep;
                    continue;
                }

                // Generación de hilera de suelo en un ÚNICO carril consistente y 100% libre de obstáculos
                int count = 4;
                float clusterStartZ = nextGemDistance;
                float clusterEndZ = clusterStartZ + (count - 1) * TrackScheduler.GridStep;

                // Seleccionar un ÚNICO carril seguro para TODO el cluster, libre de obstáculos con 6m de despeje
                // El primer cluster de la partida (antes de 30m) siempre arranca en el carril 1 (centro donde empieza el jugador)
                int preferredLane = clusterStartZ < 30f ? 1 : -1;
                int clusterLane = scheduler.GetSafeLaneForSpan(clusterStartZ, clusterEndZ, preferredLane: preferredLane, clearance: 6.0f);

                // Si ningún carril tiene despeje continuo durante este tramo, avanzar a la siguiente celda
                if (!scheduler.IsLaneClearOfObstacles(clusterLane, clusterStartZ - 6.0f, clusterEndZ + 6.0f))
                {
                    nextGemDistance += TrackScheduler.GridStep * 2f;
                    continue;
                }

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
                    float z = clusterStartZ + i * TrackScheduler.GridStep;

                    if (scheduler.IsGemForbiddenAt(z) || scheduler.IsLetterAt(z, 6f)) continue;

                    // Si se aproxima a una rampa o un arco de salto, cortar la hilera de suelo
                    if (scheduler.TryGetSurfaceAt(z, out _, 6.0f) || scheduler.TryGetJumpArcAt(z, out _, 6.0f)) break;

                    bool itemPink = isPink && (i == count - 1);
                    scheduler.EmitItem(TrackSpawnItem.CreateGem(clusterLane, z, 1.1f, pink: itemPink));
                }

                float jitter = 1f + (float)(random.NextDouble() * 0.10 - 0.05);
                float rawStep = Mathf.Max(TrackScheduler.GridStep * count + 18f, (gemInterval * jitter) / gemIntensity);
                float step = Mathf.Round(rawStep / TrackScheduler.GridStep) * TrackScheduler.GridStep;
                nextGemDistance += step;
            }
        }

        private void SpawnParabolicArc(JumpArcTrajectory arc, TrackScheduler scheduler)
        {
            // Coloca gemas alineadas estrictamente a la grilla modular describiendo la trayectoria parabólica en el aire
            int steps = Mathf.Max(2, Mathf.RoundToInt(arc.Length / TrackScheduler.GridStep));
            bool isHurdleJump = Mathf.Abs(arc.StartHeight - arc.EndHeight) < 0.2f && arc.PeakHeight > arc.StartHeight + 0.5f;

            if (isHurdleJump)
            {
                // Salto de valla estándar de 18m (6 pasos de grilla):
                // Inicia en arc.StartZ (z - 9m) y termina en arc.EndZ (z + 9m).
                // A los extremos (i = 0 y i = steps) el ciclista está en suelo (1.1m).
                // Colocamos las 5 gemas aéreas (i = 1 a steps - 1) que coinciden exactamente con la altura del ciclista en el aire:
                for (int i = 1; i < steps; i++)
                {
                    float z = arc.StartZ + i * TrackScheduler.GridStep;
                    float height = arc.EvaluateHeight(z);
                    int lane = arc.FromLane;
                    scheduler.EmitItem(TrackSpawnItem.CreateGem(lane, z, height, airborneNote: i));
                }
            }
            else
            {
                // Descenso de techo / rampa hacia el piso
                for (int i = 1; i <= steps; i++)
                {
                    float z = arc.StartZ + i * TrackScheduler.GridStep;
                    float height = arc.EvaluateHeight(z);
                    int lane = arc.EvaluateProgress(z) < 0.5f ? arc.FromLane : arc.ToLane;
                    scheduler.EmitItem(TrackSpawnItem.CreateGem(lane, z, height, airborneNote: i));
                }
            }
        }

        private void SpawnSurfaceTrail(TraversableSurface surface, float spacing, TrackScheduler scheduler)
        {
            float start = Mathf.Ceil((surface.StartZ + 1.5f) / spacing) * spacing;
            float end = surface.EndZ - 1.5f;
            if (end <= start) return;

            for (float z = start; z <= end; z += spacing)
            {
                float height = surface.HeightAt(z) + 1.1f;
                scheduler.EmitItem(TrackSpawnItem.CreateGem(surface.Lane, z, height));
            }
        }
    }
}
