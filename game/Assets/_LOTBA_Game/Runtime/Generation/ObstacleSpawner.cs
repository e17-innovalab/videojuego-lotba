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
        private int previousVehicleLane = -1;
        private System.Random random;

        public void Reset(int baseSeed)
        {
            random = new System.Random(baseSeed ^ 0x3A5B);
            nextObstacleDistance = 35f;
            wave = 0;
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
                bool isRecovery = context.ActiveZone == ZoneType.Recovery || context.RecoveryActive;

                if (scheduler.IsObstacleForbiddenAt(nextObstacleDistance, isRecovery))
                {
                    nextObstacleDistance += 8f;
                    continue;
                }

                if (isRecovery && context.RecoveryZoneEnd > 0f && nextObstacleDistance >= context.RecoveryZoneEnd - 25f)
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
            int safeLane = scheduler.GetCorridorLaneAt(nextObstacleDistance);
            bool hurdleJump = !isRecovery && wave % 4 == 2;

            if (hurdleJump)
            {
                // Encuentro de salto sobre obstáculo bajo en el carril transitable
                ObstacleDefinition lowObstacle = catalog.Pick(random, vehicle: false, maximumLength: 3.5f);
                if (lowObstacle != null)
                {
                    scheduler.EmitItem(TrackSpawnItem.CreateObstacle(safeLane, nextObstacleDistance, lowObstacle));
                    scheduler.RegisterObstacleBlock(safeLane, nextObstacleDistance - lowObstacle.Length * 0.5f, nextObstacleDistance + lowObstacle.Length * 0.5f);

                    // Arco parabólico físico que coincide exactamente con el salto de la bici: 1.1m -> 2.65m -> 1.1m
                    scheduler.RegisterJumpArc(new JumpArcTrajectory(
                        fromLane: safeLane,
                        toLane: safeLane,
                        startZ: nextObstacleDistance - 4.5f,
                        endZ: nextObstacleDistance + 4.5f,
                        startHeight: 1.1f,
                        peakHeight: 2.65f,
                        endHeight: 1.1f
                    ));

                    // Ocasionalmente bloquea un carril vecino para enmarcar el salto
                    if (random.NextDouble() < 0.45)
                    {
                        int neighbor = (safeLane + 1 + random.Next(2)) % 3;
                        ObstacleDefinition sideObstacle = catalog.Pick(random, vehicle: false, maximumLength: 4.5f);
                        if (sideObstacle != null)
                        {
                            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(neighbor, nextObstacleDistance, sideObstacle));
                            scheduler.RegisterObstacleBlock(neighbor, nextObstacleDistance - sideObstacle.Length * 0.5f, nextObstacleDistance + sideObstacle.Length * 0.5f + 2f);
                        }
                    }
                }
            }
            else
            {
                int blockedLane = (safeLane + 1 + (wave % 2)) % 3;

                ObstacleDefinition obstacle = catalog.Pick(random, vehicle: wave % 3 == 0, maximumLength: 6f);
                if (obstacle != null)
                {
                    scheduler.EmitItem(TrackSpawnItem.CreateObstacle(blockedLane, nextObstacleDistance, obstacle));
                    scheduler.RegisterObstacleBlock(blockedLane, nextObstacleDistance - obstacle.Length * 0.5f, nextObstacleDistance + obstacle.Length * 0.5f + 4f);
                }

                // En modo recupero o con alta intensidad, se bloquea un segundo carril no seguro
                float extraChance = isRecovery ? 0.70f : Mathf.Clamp01(intensity - 1f + (context.Profile != null ? context.Profile.ExtraObstacleChance : 0.2f));
                if (random.NextDouble() < extraChance)
                {
                    int secondBlocked = 3 - safeLane - blockedLane;
                    ObstacleDefinition secondObstacle = catalog.Pick(random, vehicle: false, maximumLength: 4.5f);
                    if (secondObstacle != null)
                    {
                        scheduler.EmitItem(TrackSpawnItem.CreateObstacle(secondBlocked, nextObstacleDistance + 2f, secondObstacle));
                        scheduler.RegisterObstacleBlock(secondBlocked, nextObstacleDistance - 2f, nextObstacleDistance + secondObstacle.Length + 4f);
                    }
                }
            }

            float baseInterval = context.Profile != null ? context.Profile.ObstacleInterval : 24f;
            float spacing = isRecovery ? Mathf.Max(8f, 15f / intensity) : Mathf.Max(12f, baseInterval / intensity);
            nextObstacleDistance += spacing;
        }

        private void SpawnElevatedEncounter(ObstacleSet catalog, in GenerationContext context, TrackScheduler scheduler)
        {
            ObstacleDefinition bus = catalog.PickPatternVehicle(random);
            if (bus == null)
            {
                nextObstacleDistance += 20f;
                return;
            }

            int lane = previousVehicleLane < 0 ? random.Next(3) : (previousVehicleLane + 1 + random.Next(2)) % 3;
            previousVehicleLane = lane;
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
                scheduler.RegisterObstacleBlock(lane, startZ, bus1EndZ + 4f);

                // 3. Colectivo 2 en 'neighborLane' (desfasado 2.5 metros adelante)
                float bus2StartZ = rampEndZ + 2.5f;
                float bus2EndZ = bus2StartZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(neighborLane, bus2StartZ + busLength * 0.5f, bus));
                scheduler.RegisterSurface(new TraversableSurface(neighborLane, bus2StartZ, bus2EndZ, busHeight, busHeight));
                scheduler.RegisterObstacleBlock(neighborLane, bus2StartZ, bus2EndZ + 4f);

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

                nextObstacleDistance = dropEndZ + 16f;
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
                scheduler.RegisterObstacleBlock(lane, startZ, bus1EndZ + 4f);

                // 3. Colectivo 2
                float bus2EndZ = bus1EndZ + busLength + 1.4f;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, bus1EndZ + 1.4f + busLength * 0.5f, bus));
                scheduler.RegisterSurface(new TraversableSurface(lane, bus1EndZ + 1.4f, bus2EndZ, busHeight, busHeight));
                scheduler.RegisterObstacleBlock(lane, bus1EndZ, bus2EndZ + 4f);

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

                nextObstacleDistance = jumpEndZ + 16f;
            }
        }
    }
}
