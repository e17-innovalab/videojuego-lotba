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

                if (scheduler.IsObstacleForbiddenAt(nextObstacleDistance, isRecovery))
                {
                    nextObstacleDistance += 8f;
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
            float patternEndZ = nextObstacleDistance;

            // Selección de patrón según dificultad y reglas de juego
            bool isHurdleWave = !isRecovery && wave % 4 == 2;

            if (isHurdleWave)
            {
                // Patrón: Salto de valla sobre obstáculo bajo en el carril seguro
                patternEndZ = SpawnHurdleJump(catalog, safeLane, nextObstacleDistance, scheduler);
            }
            else
            {
                // Determinación del tipo de patrón según dificultad
                double roll = random.NextDouble();

                if (difficulty < 0.35f)
                {
                    // Dificultad Baja (0 - 400m): 80% bloqueador simple (2 carriles libres), 20% puerta
                    if (roll < 0.80)
                        patternEndZ = SpawnSingleBlocker(catalog, safeLane, nextObstacleDistance, scheduler);
                    else
                        patternEndZ = SpawnCorridorGate(catalog, safeLane, nextObstacleDistance, scheduler);
                }
                else if (difficulty < 0.70f)
                {
                    // Dificultad Media (400 - 1000m): 45% simple, 35% puerta de carril, 20% slalom (zigzag)
                    if (roll < 0.45)
                        patternEndZ = SpawnSingleBlocker(catalog, safeLane, nextObstacleDistance, scheduler);
                    else if (roll < 0.80)
                        patternEndZ = SpawnCorridorGate(catalog, safeLane, nextObstacleDistance, scheduler);
                    else
                        patternEndZ = SpawnSlalom(catalog, safeLane, nextObstacleDistance, scheduler);
                }
                else
                {
                    // Dificultad Alta (1000m+ o Recuperación): 55% puerta, 30% slalom, 15% simple
                    if (roll < 0.55)
                        patternEndZ = SpawnCorridorGate(catalog, safeLane, nextObstacleDistance, scheduler);
                    else if (roll < 0.85)
                        patternEndZ = SpawnSlalom(catalog, safeLane, nextObstacleDistance, scheduler);
                    else
                        patternEndZ = SpawnSingleBlocker(catalog, safeLane, nextObstacleDistance, scheduler);
                }
            }

            // Distancia de reacción garantizada: se estrecha suavemente al avanzar pero siempre asegura tiempo humano de maniobra
            float minGap = Mathf.Lerp(16f, 13f, difficulty);
            float maxGap = Mathf.Lerp(26f, 17f, difficulty);
            float reactionGap = isRecovery
                ? Mathf.Clamp(15f / intensity, 11f, 16f)
                : Mathf.Clamp(Mathf.Lerp(maxGap, minGap, difficulty) / intensity, minGap, maxGap);

            nextObstacleDistance = patternEndZ + reactionGap;
        }

        private float SpawnSingleBlocker(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler)
        {
            int blockedLane = (safeLane + 1 + random.Next(2)) % 3;
            ObstacleDefinition obstacle = catalog.Pick(random, vehicle: random.NextDouble() < 0.40, maximumLength: 7f);
            if (obstacle == null) return z + 8f;

            float halfLen = obstacle.Length * 0.5f;
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(blockedLane, z, obstacle));
            scheduler.RegisterObstacleBlock(blockedLane, z - halfLen, z + halfLen + 1f, 0f, obstacle.Height);
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
                scheduler.RegisterObstacleBlock(laneA, z - halfA, z + halfA + 1f, 0f, obsA.Height);
                maxEndZ = Mathf.Max(maxEndZ, z + halfA);
            }

            // Segundo obstáculo ligeramente desfasado para dar apariencia de tránsito orgánico
            float zB = z + 2.5f;
            ObstacleDefinition obsB = catalog.Pick(random, vehicle: false, maximumLength: 4.5f);
            if (obsB != null)
            {
                float halfB = obsB.Length * 0.5f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(laneB, zB, obsB));
                scheduler.RegisterObstacleBlock(laneB, zB - halfB, zB + halfB + 1f, 0f, obsB.Height);
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
                scheduler.RegisterObstacleBlock(firstBlocked, z - half1, z + half1 + 1f, 0f, obs1.Height);
                maxEndZ = Mathf.Max(maxEndZ, z + half1);
            }

            // Segundo obstáculo 15m adelante en el carril inicial, forzando un zigzag hacia firstBlocked
            float z2 = z + 15f;
            ObstacleDefinition obs2 = catalog.Pick(random, vehicle: true, maximumLength: 6f);
            if (obs2 != null)
            {
                float half2 = obs2.Length * 0.5f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(safeLane, z2, obs2));
                scheduler.RegisterObstacleBlock(safeLane, z2 - half2, z2 + half2 + 1f, 0f, obs2.Height);
                maxEndZ = Mathf.Max(maxEndZ, z2 + half2);
            }

            // El nuevo carril seguro pasa a ser el que quedó abierto
            currentSafeLane = firstBlocked;
            return maxEndZ;
        }

        private float SpawnHurdleJump(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler)
        {
            ObstacleDefinition lowObstacle = catalog.Pick(random, vehicle: false, maximumLength: 3.5f);
            if (lowObstacle == null) return z + 8f;

            float obsHalf = lowObstacle.Length * 0.5f;
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(safeLane, z, lowObstacle));
            scheduler.RegisterObstacleBlock(safeLane, z - obsHalf, z + obsHalf, 0f, lowObstacle.Height);

            // Arco parabólico físico que coincide exactamente con el salto de la bici: 1.1m -> 2.65m -> 1.1m
            scheduler.RegisterJumpArc(new JumpArcTrajectory(
                fromLane: safeLane,
                toLane: safeLane,
                startZ: z - 4.5f,
                endZ: z + 4.5f,
                startHeight: 1.1f,
                peakHeight: 2.65f,
                endHeight: 1.1f
            ));

            float maxEndZ = z + 4.5f;

            // Ocasionalmente bloquea un carril vecino para enmarcar el salto
            if (random.NextDouble() < 0.45)
            {
                int neighbor = (safeLane + 1 + random.Next(2)) % 3;
                ObstacleDefinition sideObstacle = catalog.Pick(random, vehicle: false, maximumLength: 4.5f);
                if (sideObstacle != null)
                {
                    float sideHalf = sideObstacle.Length * 0.5f;
                    scheduler.EmitItem(TrackSpawnItem.CreateObstacle(neighbor, z, sideObstacle));
                    scheduler.RegisterObstacleBlock(neighbor, z - sideHalf, z + sideHalf + 1f, 0f, sideObstacle.Height);
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

            float startZ = nextObstacleDistance;
            float rampEndZ = startZ + rampLength;

            // Fórmulas físicas adaptadas a la velocidad para que las gemas de descenso se puedan recolectar con precisión
            float fallTime = Mathf.Sqrt(2f * busHeight / 34f);
            float speed = Mathf.Max(26f, context.Speed);
            float arcLength = speed * fallTime * 1.15f;

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
                scheduler.RegisterSurface(new TraversableSurface(lane, rampEndZ, bus1EndZ, busHeight, busHeight));
                scheduler.RegisterObstacleBlock(lane, startZ, bus1EndZ + 2f, 0f, busHeight);

                // 3. Colectivo 2 en 'neighborLane' (desfasado 2.5 metros adelante)
                float bus2StartZ = rampEndZ + 2.5f;
                float bus2EndZ = bus2StartZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(neighborLane, bus2StartZ + busLength * 0.5f, bus));
                scheduler.RegisterSurface(new TraversableSurface(neighborLane, bus2StartZ, bus2EndZ, busHeight, busHeight));
                scheduler.RegisterObstacleBlock(neighborLane, bus2StartZ, bus2EndZ + 2f, 0f, busHeight);

                // 4. Plataforma de cruce lateral entre ambos techos
                float bridgeStartZ = bus2StartZ;
                float bridgeEndZ = bus1EndZ;
                scheduler.CreateRoofBridge(lane, neighborLane, bridgeStartZ, bridgeEndZ, busHeight, bus);
                scheduler.RegisterSurface(new TraversableSurface(neighborLane, bridgeStartZ, bridgeEndZ, busHeight, busHeight, RunnerWorld.LaneSpacing));

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
            float gap = Mathf.Clamp(22f / intensity, 16f, 26f);
            nextObstacleDistance = patternEndZ + gap;
        }
    }
}
