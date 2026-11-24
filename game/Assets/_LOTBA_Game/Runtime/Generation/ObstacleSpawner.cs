using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Generador de obstáculos, vehículos y superficies elevadas.
    /// Define la geometría transitable del nivel y registra superficies y arcos de salto en el scheduler.
    /// </summary>
    public sealed class ObstacleSpawner : IContentSpawner
    {
        public string Name => "ObstacleSpawner";
        public float NextRequestedDistance => nextObstacleDistance;

        private float nextObstacleDistance;
        private int wave;
        private int currentSafeLane = 1;
        private int previousVehicleLane = -1;
        private System.Random random;

        public void Reset(int baseSeed)
        {
            random = new System.Random(baseSeed ^ 0x3A5B);
            nextObstacleDistance = 35f;
            wave = 0;
            currentSafeLane = 1;
            previousVehicleLane = -1;
        }

        public void Populate(in GenerationContext context, TrackScheduler scheduler)
        {
            if (!context.Rules.AllowProfileObstacles)
            {
                nextObstacleDistance = Mathf.Max(nextObstacleDistance, context.SpawnHorizon);
                return;
            }

            ObstacleSet catalog = context.Route != null ? context.Route.MapAtDistance(nextObstacleDistance).Obstacles : null;
            if (catalog == null) return;

            float minSpacing = context.Rules.MinimumObstacleSpacing;
            float intensity = Mathf.Max(0.5f, context.Rules.ObstacleIntensity * context.ObstacleIntensity);

            while (nextObstacleDistance < context.SpawnHorizon)
            {
                bool isRecovery = (context.ActiveZone == ZoneType.Recovery || context.RecoveryActive) &&
                                  (context.RecoveryZoneEnd <= 0f || nextObstacleDistance < context.RecoveryZoneEnd);

                if (scheduler.IsObstacleForbiddenAt(nextObstacleDistance, isRecovery) ||
                    scheduler.IsObstacleSpanForbidden(nextObstacleDistance - 3f, nextObstacleDistance + 21f, isRecovery))
                {
                    nextObstacleDistance += TrackScheduler.GridStep * 2f;
                    continue;
                }

                if (isRecovery && context.RecoveryZoneEnd > 0f &&
                    nextObstacleDistance >= context.RecoveryZoneEnd - 25f &&
                    nextObstacleDistance < context.RecoveryZoneEnd + 36f)
                {
                    // Mantener los últimos 25m de la zona de recupero libres para la aproximación y recolección de la gema dorada
                    nextObstacleDistance = context.RecoveryZoneEnd + 36f;
                    continue;
                }

                bool canSpawnElevated = context.Rules.AllowElevatedEncounters && catalog.ElevatedBus != null && !isRecovery && wave % 5 == 3;

                if (canSpawnElevated)
                {
                    SpawnElevatedEncounter(catalog, context, scheduler);
                }
                else
                {
                    SpawnGroundObstacles(catalog, context, isRecovery, intensity, scheduler);
                }

                wave++;
            }
        }

        private void SpawnGroundObstacles(ObstacleSet catalog, in GenerationContext context, bool isRecovery, float intensity, TrackScheduler scheduler)
        {
            // Progreso dinámico de dificultad a lo largo del recorrido (0 = inicio/fácil, 1 = avanzado/difícil)
            float routeProgress = Mathf.Clamp01(nextObstacleDistance / 1800f);
            float sectionProgress = 0f;
            if (context.Route != null && context.Route.Sections != null && context.Route.Sections.Length > 0)
            {
                sectionProgress = (float)context.Route.SectionAt(nextObstacleDistance, out _) / Mathf.Max(1, context.Route.Sections.Length - 1);
            }
            float difficulty = Mathf.Clamp01(Mathf.Max(routeProgress, sectionProgress) * 0.75f + (context.ObstacleIntensity - 1f) * 0.25f);
            if (isRecovery) difficulty = Mathf.Max(difficulty, 0.75f); // Modo recupero siempre desafiante

            // Transición suave de carril seguro cada pocas olas
            if (random.NextDouble() < 0.35)
            {
                currentSafeLane = Mathf.Clamp(currentSafeLane + (random.Next(2) == 0 ? -1 : 1), 0, 2);
            }

            int safeLane = currentSafeLane;
            // Alineación estricta a la grilla modular de 3.0m (4 celdas por módulo de calle de 12m)
            float patternZ = Mathf.Round(nextObstacleDistance / TrackScheduler.GridStep) * TrackScheduler.GridStep;
            float patternEndZ = patternZ;

            // Selección de patrón según dificultad y reglas de juego
            bool isHurdleWave = !isRecovery && wave % 4 == 2;

            if (isHurdleWave)
            {
                // Patrón: Salto de valla sobre obstáculo bajo en el carril seguro (con arco parabólico de 5 gemas sobre la grilla)
                patternEndZ = SpawnHurdleJump(catalog, safeLane, patternZ, scheduler);
            }
            else
            {
                // Determinación del tipo de patrón según dificultad
                double roll = random.NextDouble();

                if (difficulty < 0.35f)
                {
                    // Dificultad Baja (0 - 400m): 80% bloqueador simple (2 carriles libres), 20% puerta
                    if (roll < 0.80)
                        patternEndZ = SpawnSingleBlocker(catalog, safeLane, patternZ, scheduler);
                    else
                        patternEndZ = SpawnCorridorGate(catalog, safeLane, patternZ, scheduler);
                }
                else if (difficulty < 0.70f)
                {
                    // Dificultad Media (400 - 1000m): 45% simple, 35% puerta de carril, 20% slalom (zigzag)
                    if (roll < 0.45)
                        patternEndZ = SpawnSingleBlocker(catalog, safeLane, patternZ, scheduler);
                    else if (roll < 0.80)
                        patternEndZ = SpawnCorridorGate(catalog, safeLane, patternZ, scheduler);
                    else
                        patternEndZ = SpawnSlalom(catalog, safeLane, patternZ, scheduler);
                }
                else
                {
                    // Dificultad Alta (1000m+ o Recuperación): 55% puerta, 30% slalom, 15% simple
                    if (roll < 0.55)
                        patternEndZ = SpawnCorridorGate(catalog, safeLane, patternZ, scheduler);
                    else if (roll < 0.85)
                        patternEndZ = SpawnSlalom(catalog, safeLane, patternZ, scheduler);
                    else
                        patternEndZ = SpawnSingleBlocker(catalog, safeLane, patternZ, scheduler);
                }
            }

            // Distancia de reacción garantizada alineada a la grilla de 3m
            float minGap = Mathf.Lerp(15f, 12f, difficulty);
            float maxGap = Mathf.Lerp(27f, 18f, difficulty);
            float rawGap = isRecovery
                ? Mathf.Clamp(15f / intensity, 12f, 18f)
                : Mathf.Clamp(Mathf.Lerp(maxGap, minGap, difficulty) / intensity, minGap, maxGap);
            float reactionGap = Mathf.Round(rawGap / TrackScheduler.GridStep) * TrackScheduler.GridStep;

            nextObstacleDistance = patternEndZ + reactionGap;
        }

        private float SpawnSingleBlocker(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler)
        {
            int blockedLane = (safeLane + 1 + random.Next(2)) % 3;
            ObstacleDefinition obstacle = catalog.Pick(random, vehicle: random.NextDouble() < 0.40, maximumLength: 7f);
            if (obstacle == null) return z + 9f;

            float halfLen = obstacle.Length * 0.5f;
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(blockedLane, z, obstacle));
            scheduler.RegisterObstacleBlock(blockedLane, z - halfLen, z + halfLen, 0f, obstacle.Height);
            return z + halfLen;
        }

        private float SpawnCorridorGate(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler)
        {
            int laneA = (safeLane + 1) % 3;
            int laneB = (safeLane + 2) % 3;
            float maxEndZ = z;

            ObstacleDefinition obsA = catalog.Pick(random, vehicle: random.NextDouble() < 0.35, maximumLength: 6f);
            if (obsA != null)
            {
                float halfA = obsA.Length * 0.5f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(laneA, z, obsA));
                scheduler.RegisterObstacleBlock(laneA, z - halfA, z + halfA, 0f, obsA.Height);
                maxEndZ = Mathf.Max(maxEndZ, z + halfA);
            }

            // Segundo obstáculo desfasado exactamente 1 paso de grilla (3.0m)
            float zB = z + TrackScheduler.GridStep;
            ObstacleDefinition obsB = catalog.Pick(random, vehicle: false, maximumLength: 4.5f);
            if (obsB != null)
            {
                float halfB = obsB.Length * 0.5f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(laneB, zB, obsB));
                scheduler.RegisterObstacleBlock(laneB, zB - halfB, zB + halfB, 0f, obsB.Height);
                maxEndZ = Mathf.Max(maxEndZ, zB + halfB);
            }

            return maxEndZ;
        }

        private float SpawnSlalom(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler)
        {
            int firstBlocked = (safeLane + 1 + random.Next(2)) % 3;
            float maxEndZ = z;

            ObstacleDefinition obs1 = catalog.Pick(random, vehicle: false, maximumLength: 4.5f);
            if (obs1 != null)
            {
                float half1 = obs1.Length * 0.5f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(firstBlocked, z, obs1));
                scheduler.RegisterObstacleBlock(firstBlocked, z - half1, z + half1, 0f, obs1.Height);
                maxEndZ = Mathf.Max(maxEndZ, z + half1);
            }

            // Segundo obstáculo 15m (5 pasos de grilla) adelante en el carril inicial, forzando un zigzag hacia firstBlocked
            float z2 = z + 15f;
            ObstacleDefinition obs2 = catalog.Pick(random, vehicle: true, maximumLength: 6f);
            if (obs2 != null)
            {
                float half2 = obs2.Length * 0.5f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(safeLane, z2, obs2));
                scheduler.RegisterObstacleBlock(safeLane, z2 - half2, z2 + half2, 0f, obs2.Height);
                maxEndZ = Mathf.Max(maxEndZ, z2 + half2);
            }

            // El nuevo carril seguro pasa a ser el que quedó abierto
            currentSafeLane = firstBlocked;
            return maxEndZ;
        }

        private float SpawnHurdleJump(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler)
        {
            ObstacleDefinition lowObstacle = catalog.Pick(random, vehicle: false, maximumLength: 3.5f);
            if (lowObstacle == null) return z + 9f;

            float obsHalf = lowObstacle.Length * 0.5f;
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(safeLane, z, lowObstacle));
            scheduler.RegisterObstacleBlock(safeLane, z - obsHalf, z + obsHalf, 0f, lowObstacle.Height);

            // Arco parabólico físico que coincide exactamente con el salto de la bici:
            // La bici salta durante 0.72s a ~25-26 m/s = 18 metros de longitud total de salto.
            // Inicia en z - 9.0m, alcanza el ápice (2.85m) en z (sobre el obstáculo), y aterriza en z + 9.0m.
            scheduler.RegisterJumpArc(new JumpArcTrajectory(
                fromLane: safeLane,
                toLane: safeLane,
                startZ: z - 9.0f,
                endZ: z + 9.0f,
                startHeight: 1.1f,
                peakHeight: 2.85f,
                endHeight: 1.1f
            ));

            float maxEndZ = z + 9.0f;

            // Ocasionalmente bloquea un carril vecino para enmarcar el salto
            if (random.NextDouble() < 0.45)
            {
                int neighbor = (safeLane + 1 + random.Next(2)) % 3;
                ObstacleDefinition sideObstacle = catalog.Pick(random, vehicle: false, maximumLength: 4.5f);
                if (sideObstacle != null)
                {
                    float sideHalf = sideObstacle.Length * 0.5f;
                    scheduler.EmitItem(TrackSpawnItem.CreateObstacle(neighbor, z, sideObstacle));
                    scheduler.RegisterObstacleBlock(neighbor, z - sideHalf, z + sideHalf, 0f, sideObstacle.Height);
                    maxEndZ = Mathf.Max(maxEndZ, z + sideHalf);
                }
            }

            return maxEndZ;
        }

        private void SpawnElevatedEncounter(ObstacleSet catalog, in GenerationContext context, TrackScheduler scheduler)
        {
            ObstacleDefinition bus = catalog.PickPatternVehicle(random);
            if (bus == null)
            {
                nextObstacleDistance += 20f;
                return;
            }

            int lane = previousVehicleLane < 0 ? currentSafeLane : (previousVehicleLane + 1 + random.Next(2)) % 3;
            previousVehicleLane = lane;
            currentSafeLane = lane;
            int neighborLane = lane == 1 ? (random.Next(2) == 0 ? 0 : 2) : 1;

            float busLength = bus.Length;
            float busHeight = bus.Height;
            float rampLength = 12f;

            float startZ = Mathf.Round(nextObstacleDistance / TrackScheduler.GridStep) * TrackScheduler.GridStep;
            float rampEndZ = startZ + rampLength;

            // Fórmulas físicas adaptadas a la velocidad para que las gemas de descenso se puedan recolectar con precisión
            float fallTime = Mathf.Sqrt(2f * busHeight / 34f);
            float speed = Mathf.Max(26f, context.Speed);
            float arcLength = Mathf.Max(12f, Mathf.Round((speed * fallTime * 1.15f) / TrackScheduler.GridStep) * TrackScheduler.GridStep);

            float patternEndZ;
            bool isRoofBridge = (wave % 2 == 1);

            if (isRoofBridge)
            {
                // Patrón RoofBridge: Dos colectivos contiguos unidos por una plataforma lateral
                // 1. Rampa de acceso en 'lane'
                scheduler.CreateRamp(lane, startZ, rampEndZ, 0f, busHeight, bus);
                scheduler.RegisterSurface(new TraversableSurface(lane, startZ, rampEndZ, 0f, busHeight, isRamp: true));

                // 2. Colectivo 1 en 'lane'
                float bus1EndZ = rampEndZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, rampEndZ + busLength * 0.5f, bus));
                scheduler.RegisterObstacleBlock(lane, startZ, bus1EndZ + 2f, 0f, busHeight);

                // 3. Colectivo 2 en 'neighborLane' (desfasado 1 celda de grilla adelante)
                float bridgeStartZ = rampEndZ + TrackScheduler.GridStep;
                float bus2StartZ = bridgeStartZ;
                float bus2EndZ = bus2StartZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(neighborLane, bus2StartZ + busLength * 0.5f, bus));
                scheduler.RegisterObstacleBlock(neighborLane, bus2StartZ, bus2EndZ + 2f, 0f, busHeight);

                // 4. Plataforma de cruce lateral entre ambos techos
                float bridgeEndZ = bus1EndZ;
                scheduler.CreateRoofBridge(lane, neighborLane, bridgeStartZ, bridgeEndZ, busHeight, bus);

                // ÚNICO camino de gemas continuo que guía al ciclista sin duplicación de carriles:
                // Colectivo 1 en 'lane' hasta llegar al puente:
                scheduler.RegisterSurface(new TraversableSurface(lane, rampEndZ, bridgeStartZ, busHeight, busHeight));
                // Colectivo 2 en 'neighborLane' desde el puente hasta la bajada:
                scheduler.RegisterSurface(new TraversableSurface(neighborLane, bridgeStartZ, bus2EndZ, busHeight, busHeight));

                // 5. Descenso balístico al final del segundo colectivo en 'neighborLane'
                float dropStartZ = bus2EndZ;
                float dropEndZ = dropStartZ + arcLength;
                scheduler.RegisterJumpArc(new JumpArcTrajectory(
                    fromLane: neighborLane,
                    toLane: neighborLane,
                    startZ: dropStartZ,
                    endZ: dropEndZ,
                    startHeight: busHeight + 1.1f,
                    peakHeight: busHeight + 1.1f,
                    endHeight: 1.1f
                ));

                currentSafeLane = neighborLane;
                patternEndZ = dropEndZ;
            }
            else
            {
                // Patrón Convoy: Cadena de colectivos en el mismo carril
                // 1. Rampa de subida
                scheduler.CreateRamp(lane, startZ, rampEndZ, 0f, busHeight, bus);
                scheduler.RegisterSurface(new TraversableSurface(lane, startZ, rampEndZ, 0f, busHeight, isRamp: true));

                // 2. Colectivo 1
                float bus1EndZ = rampEndZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, rampEndZ + busLength * 0.5f, bus));
                scheduler.RegisterSurface(new TraversableSurface(lane, rampEndZ, bus1EndZ, busHeight, busHeight));
                scheduler.RegisterObstacleBlock(lane, startZ, bus1EndZ + 2f, 0f, busHeight);

                // 3. Colectivo 2
                float bus2EndZ = bus1EndZ + busLength + 1.4f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, bus1EndZ + 1.4f + busLength * 0.5f, bus));
                scheduler.RegisterSurface(new TraversableSurface(lane, bus1EndZ + 1.4f, bus2EndZ, busHeight, busHeight));
                scheduler.RegisterObstacleBlock(lane, bus1EndZ, bus2EndZ + 2f, 0f, busHeight);

                // 4. Descenso balístico al final del segundo colectivo hacia el piso
                float jumpStartZ = bus2EndZ;
                float jumpEndZ = jumpStartZ + arcLength;
                scheduler.RegisterJumpArc(new JumpArcTrajectory(
                    fromLane: lane,
                    toLane: lane,
                    startZ: jumpStartZ,
                    endZ: jumpEndZ,
                    startHeight: busHeight + 1.1f,
                    peakHeight: busHeight + 1.1f,
                    endHeight: 1.1f
                ));

                currentSafeLane = lane;
                patternEndZ = jumpEndZ;
            }

            float intensity = Mathf.Max(0.5f, context.Rules.ObstacleIntensity * context.ObstacleIntensity);
            float gap = Mathf.Round(Mathf.Clamp(22f / intensity, 15f, 27f) / TrackScheduler.GridStep) * TrackScheduler.GridStep;
            nextObstacleDistance = patternEndZ + gap;
        }
    }
}
