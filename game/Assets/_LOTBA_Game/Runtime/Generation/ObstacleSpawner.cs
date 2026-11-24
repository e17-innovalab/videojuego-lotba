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
        private float nextElevatedDistance;
        private System.Random random;

        public void Reset(int baseSeed)
        {
            random = new System.Random(baseSeed ^ 0x3A5B);
            nextObstacleDistance = 35f;
            wave = 0;
            currentSafeLane = 1;
            previousVehicleLane = -1;
            nextElevatedDistance = 180f;
        }

        public void BeginRecoveryChallenge(float startDistance, float firstAerialDelay)
        {
            nextObstacleDistance = Mathf.Ceil(startDistance / TrackScheduler.GridStep) * TrackScheduler.GridStep
                + TrackScheduler.GridStep * 8f;
            nextElevatedDistance = startDistance + Mathf.Max(0f, firstAerialDelay);
        }

        public void Populate(in GenerationContext context, TrackScheduler scheduler)
        {
            if (!context.Rules.AllowProfileObstacles)
            {
                nextObstacleDistance = Mathf.Max(nextObstacleDistance, context.SpawnHorizon);
                return;
            }

            if (context.Route == null) return;

            float intensity = Mathf.Max(0.5f, context.Rules.ObstacleIntensity * context.ObstacleIntensity);

            while (nextObstacleDistance < context.SpawnHorizon)
            {
                ObstacleSet catalog = context.Route.MapAtDistance(nextObstacleDistance).Obstacles;
                if (catalog == null)
                {
                    nextObstacleDistance += TrackScheduler.GridStep * 2f;
                    continue;
                }
                EncounterProfile profile = context.ProfileAt(nextObstacleDistance);
                float challengeEnd = context.RecoveryChallengeEnd > 0f
                    ? context.RecoveryChallengeEnd
                    : context.RecoveryZoneEnd;
                bool isRecovery = (context.ActiveZone == ZoneType.Recovery || context.RecoveryActive) &&
                                  (challengeEnd <= 0f || nextObstacleDistance < challengeEnd);
                ObstacleSet elevatedCatalog = isRecovery && context.Rules.RecoveryAerialSet != null
                    ? context.Rules.RecoveryAerialSet
                    : catalog;
                float routeProgress = isRecovery
                    ? RouteProgress(context, nextObstacleDistance)
                    : profile != null ? ZoneProgress(context, profile, nextObstacleDistance) : RouteProgress(context, nextObstacleDistance);
                float recoveryProgress = RouteProgress(context, nextObstacleDistance);
                float reactionSeconds = profile != null ? profile.ObstacleReactionSecondsAt(routeProgress) : .85f;
                if (isRecovery && profile != null)
                    reactionSeconds = Mathf.Lerp(profile.ObstacleReactionSeconds, profile.RecoveryAdvancedReactionSeconds, recoveryProgress);
                float encounterLead = context.Speed * reactionSeconds;
                bool recoveryAerialWindow = isRecovery && profile != null &&
                    profile.RecoveryAerialEncounterChance > 0f && nextObstacleDistance >= nextElevatedDistance;

                float encounterSpanAhead = 21f;
                if ((!isRecovery || recoveryAerialWindow) && context.Rules.AllowElevatedEncounters &&
                    elevatedCatalog.ElevatedBus != null && elevatedCatalog.ElevatedBus.RideableRoof &&
                    nextObstacleDistance >= nextElevatedDistance)
                {
                    // Incluye rampa, buses, salto de salida y aterrizaje para no invadir letras
                    // ni encuentros ya reservados a lo largo de un patrón elevado.
                    encounterSpanAhead = 21f + elevatedCatalog.ElevatedBus.Length * 2f +
                        context.Speed * (JumpArc.Duration + .45f);
                }

                if (scheduler.IsObstacleForbiddenAt(nextObstacleDistance, isRecovery) ||
                    scheduler.IsObstacleSpanForbidden(nextObstacleDistance - encounterLead - 3f,
                        nextObstacleDistance + encounterSpanAhead, isRecovery))
                {
                    nextObstacleDistance += TrackScheduler.GridStep * 2f;
                    continue;
                }

                if (isRecovery && challengeEnd > 0f &&
                    nextObstacleDistance + encounterSpanAhead >= challengeEnd - 25f &&
                    nextObstacleDistance < challengeEnd + 12f)
                {
                    // Mantener los últimos 25m de la zona de recupero libres para la aproximación y recolección de la gema dorada
                    nextObstacleDistance = challengeEnd + 12f;
                    continue;
                }

                bool canSpawnElevated = context.Rules.AllowElevatedEncounters && elevatedCatalog.ElevatedBus != null &&
                    elevatedCatalog.ElevatedBus.RideableRoof && nextObstacleDistance >= nextElevatedDistance &&
                    (!isRecovery || recoveryAerialWindow && random.NextDouble() <
                        Mathf.Lerp(profile.RecoveryAerialEncounterChance, 1f, routeProgress));

                if (canSpawnElevated)
                {
                    SpawnElevatedEncounter(elevatedCatalog, context, isRecovery, routeProgress, scheduler);
                    float interval = profile != null
                        ? isRecovery
                            ? profile.RecoveryAerialInterval * Mathf.Lerp(1f,
                                profile.RecoveryAdvancedAerialIntervalMultiplier, routeProgress)
                            : Mathf.Max(profile.VehiclePatternInterval, context.Speed * profile.VehiclePatternIntervalSecondsAt(routeProgress))
                        : 240f;
                    nextElevatedDistance = nextObstacleDistance + interval;
                }
                else
                {
                    SpawnGroundObstacles(catalog, profile, context, isRecovery, intensity, routeProgress, scheduler);
                    if (recoveryAerialWindow && profile != null)
                        nextElevatedDistance = nextObstacleDistance + profile.RecoveryAerialInterval *
                            Mathf.Lerp(1f, profile.RecoveryAdvancedAerialIntervalMultiplier, routeProgress);
                }

                wave++;
            }
        }

        private void SpawnGroundObstacles(ObstacleSet catalog, EncounterProfile profile, in GenerationContext context, bool isRecovery, float intensity, float profileProgress, TrackScheduler scheduler)
        {
            // Progreso dinámico de dificultad a lo largo del recorrido (0 = inicio/fácil, 1 = avanzado/difícil)
            float routeProgress = isRecovery ? RouteProgress(context, nextObstacleDistance) : profileProgress;
            float sectionProgress = 0f;
            if (context.Route != null && context.Route.Sections != null && context.Route.Sections.Length > 0)
            {
                sectionProgress = (float)context.Route.SectionAt(nextObstacleDistance, out _) / Mathf.Max(1, context.Route.Sections.Length - 1);
            }
            float difficulty = Mathf.Clamp01(Mathf.Max(routeProgress, sectionProgress) * 0.75f + (context.ObstacleIntensity - 1f) * 0.25f);
            if (isRecovery) difficulty = Mathf.Lerp(.72f, 1f, routeProgress);

            // Transición suave de carril seguro cada pocas olas
            if (random.NextDouble() < 0.35)
            {
                currentSafeLane = Mathf.Clamp(currentSafeLane + (random.Next(2) == 0 ? -1 : 1), 0, 2);
            }

            int safeLane = currentSafeLane;
            // Alineación estricta a la grilla modular de 3.0m (4 celdas por módulo de calle de 12m)
            float patternZ = Mathf.Round(nextObstacleDistance / TrackScheduler.GridStep) * TrackScheduler.GridStep;
            float patternEndZ = patternZ;

            // Los perfiles editables eligen la mezcla; la dificultad queda como fallback sin perfil.
            bool isHurdleWave = !isRecovery && profile != null && random.NextDouble() < profile.HurdleChance;
            bool preferLongVehicle = profile != null && random.NextDouble() < profile.LongVehicleChance;

            if (isHurdleWave)
            {
                // Patrón: Salto de valla sobre obstáculo bajo en el carril seguro (con arco parabólico de 5 gemas sobre la grilla)
                patternEndZ = SpawnHurdleJump(catalog, safeLane, patternZ, scheduler);
            }
            else
            {
                GroundEncounterPattern pattern = profile != null
                    ? profile.PickGroundPattern(random)
                    : PickFallbackPattern(difficulty, random);
                switch (pattern)
                {
                    case GroundEncounterPattern.CorridorGate:
                        patternEndZ = SpawnCorridorGate(catalog, safeLane, patternZ, scheduler, profile, routeProgress);
                        break;
                    case GroundEncounterPattern.Slalom:
                        patternEndZ = SpawnSlalom(catalog, safeLane, patternZ, scheduler, profile, routeProgress);
                        break;
                    default:
                        patternEndZ = SpawnSingleBlocker(catalog, safeLane, patternZ, scheduler, profile, routeProgress, preferLongVehicle);
                        break;
                }
            }

            // Distancia de reacción garantizada alineada a la grilla de 3m
            float reactionSeconds = profile != null ? profile.ObstacleReactionSecondsAt(routeProgress) : .85f;
            if (isRecovery && profile != null)
                reactionSeconds = Mathf.Lerp(reactionSeconds, profile.RecoveryAdvancedReactionSeconds, routeProgress);
            float speedReactionDistance = context.Speed * reactionSeconds;
            float patternClearance = profile != null
                ? Mathf.Max(profile.ObstacleClearance, speedReactionDistance)
                : context.Rules.MinimumObstacleSpacing;
            float targetInterval = profile != null
                ? Mathf.Max(profile.ObstacleIntervalAt(routeProgress), speedReactionDistance)
                : Mathf.Lerp(27f, 18f, difficulty);
            float nextDistance;
            if (isRecovery)
            {
                // Recupero conserva dificultad alta, con una separación mayor para leer y esquivar cada patrón.
                float progressiveMinimum = Mathf.Lerp(context.Rules.MinimumObstacleSpacing,
                    Mathf.Max(6f, context.Rules.MinimumObstacleSpacing * .65f), routeProgress);
                float recoveryGap = Mathf.Clamp(Mathf.Max(progressiveMinimum, speedReactionDistance), 6f, 24f);
                nextDistance = patternEndZ + recoveryGap;
            }
            else
            {
                float minimumNext = patternEndZ + patternClearance;
                float profileNext = patternZ + targetInterval / intensity;
                nextDistance = Mathf.Max(minimumNext, profileNext);
            }

            nextObstacleDistance = Mathf.Ceil(nextDistance / TrackScheduler.GridStep) * TrackScheduler.GridStep;
        }

        private static float RouteProgress(in GenerationContext context, float distance)
        {
            float finishDistance = context.Route != null ? context.Route.FinishDistance : 0f;
            return finishDistance > 0f
                ? Mathf.Clamp01(distance / finishDistance)
                : Mathf.Clamp01(distance / 1800f);
        }

        private static float ZoneProgress(in GenerationContext context, EncounterProfile profile, float distance)
        {
            if (profile == null || profile.DifficultyProgressionMetres <= 0f || context.Route == null)
                return RouteProgress(context, distance);
            context.Route.SectionAt(distance, out float sectionStart);
            return Mathf.Clamp01((distance - sectionStart) / profile.DifficultyProgressionMetres);
        }

        private static GroundEncounterPattern PickFallbackPattern(float difficulty, System.Random random)
        {
            double roll = random.NextDouble();
            if (difficulty < .35f) return roll < .8f ? GroundEncounterPattern.SingleBlocker : GroundEncounterPattern.CorridorGate;
            if (difficulty < .70f)
                return roll < .45f ? GroundEncounterPattern.SingleBlocker
                    : roll < .8f ? GroundEncounterPattern.CorridorGate : GroundEncounterPattern.Slalom;
            return roll < .55f ? GroundEncounterPattern.CorridorGate
                : roll < .85f ? GroundEncounterPattern.Slalom : GroundEncounterPattern.SingleBlocker;
        }

        private float SpawnSingleBlocker(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler,
            EncounterProfile profile, float profileProgress, bool preferLong)
        {
            int blockedLane = (safeLane + 1 + random.Next(2)) % 3;
            bool vehicle = random.NextDouble() < (profile != null ? profile.TrafficObstacleChanceAt(profileProgress) : .40f);
            ObstacleDefinition obstacle = catalog.Pick(random, vehicle, maximumLength: vehicle ? 10f : 7f, preferLong: vehicle && preferLong);
            if (obstacle == null) return z + 9f;

            float halfLen = obstacle.Length * 0.5f;
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(blockedLane, z, obstacle));
            scheduler.RegisterObstacleBlock(blockedLane, z - halfLen, z + halfLen, 0f, obstacle.Height);
            return z + halfLen;
        }

        private float SpawnCorridorGate(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler, EncounterProfile profile, float profileProgress)
        {
            int laneA = (safeLane + 1) % 3;
            int laneB = (safeLane + 2) % 3;
            float maxEndZ = z;

            bool vehicleA = random.NextDouble() < (profile != null ? profile.TrafficObstacleChanceAt(profileProgress) : .35f);
            bool longVehicleA = vehicleA && profile != null && random.NextDouble() < profile.LongVehicleChance;
            ObstacleDefinition obsA = catalog.Pick(random, vehicleA, maximumLength: vehicleA ? 10f : 6f, preferLong: longVehicleA);
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

        private float SpawnSlalom(ObstacleSet catalog, int safeLane, float z, TrackScheduler scheduler, EncounterProfile profile, float profileProgress)
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
            bool vehicle = random.NextDouble() < (profile != null ? profile.TrafficObstacleChanceAt(profileProgress) : .4f);
            bool preferLong = vehicle && profile != null && random.NextDouble() < profile.LongVehicleChance;
            ObstacleDefinition obs2 = catalog.Pick(random, vehicle, maximumLength: vehicle ? 10f : 6f, preferLong: preferLong);
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

        private void SpawnElevatedEncounter(ObstacleSet catalog, in GenerationContext context, bool isRecovery,
            float routeProgress, TrackScheduler scheduler)
        {
            EncounterProfile profile = context.ProfileAt(nextObstacleDistance);
            ObstacleDefinition bus = catalog.PickPatternVehicle(random);
            if (bus == null || !bus.RideableRoof)
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

            // La bajada desde el techo usa la gravedad del recorrido elevado para
            // que la ventana de aterrizaje coincida con la física del ciclista.
            float speed = Mathf.Max(1f, context.Speed);

            if (isRecovery)
            {
                float jumpWeight = Mathf.Lerp(profile != null ? profile.RecoveryBusJumpChance : .6f,
                    profile != null ? profile.RecoveryAdvancedBusJumpChance : .9f, routeProgress);
                float rampWeight = Mathf.Lerp(profile != null ? profile.ThreeBusRampChance : .35f,
                    profile != null ? profile.RecoveryAdvancedThreeBusRampChance : .5f, routeProgress);
                bool useBusJump = random.NextDouble() * (jumpWeight + rampWeight) < jumpWeight;
                float reactionSeconds = profile != null
                    ? Mathf.Lerp(profile.ObstacleReactionSeconds, profile.RecoveryAdvancedReactionSeconds, routeProgress)
                    : .4f;
                float patternEnd = useBusJump
                    ? SpawnRecoveryBusJump(lane, neighborLane, startZ, rampEndZ, bus, speed, scheduler)
                    : SpawnThreeBusRamp(lane, startZ, rampEndZ, bus, speed,
                        profile != null && random.NextDouble() < profile.RoofJumpExitChance,
                        speed * reactionSeconds, scheduler);

                float recoveryIntensity = Mathf.Max(.5f, context.Rules.ObstacleIntensity * context.ObstacleIntensity);
                float recoveryMinimumGap = Mathf.Clamp(18f / recoveryIntensity, 12f, 24f);
                float recoveryGap = Mathf.Ceil(Mathf.Max(recoveryMinimumGap, speed * reactionSeconds) / TrackScheduler.GridStep)
                    * TrackScheduler.GridStep;
                nextObstacleDistance = patternEnd + recoveryGap;
                return;
            }

            float patternEndZ;
            if (catalog.TryPickVehiclePattern(random, out VehiclePattern configuredPattern) &&
                configuredPattern >= VehiclePattern.ArticulatedTripleChain)
            {
                patternEndZ = SpawnNewAerialPattern(configuredPattern, lane, neighborLane, startZ, rampEndZ,
                    bus, speed, scheduler);
                currentSafeLane = configuredPattern == VehiclePattern.LateralRoofLeap ? neighborLane :
                    configuredPattern == VehiclePattern.SplitLaneBusHops ? (neighborLane == 0 ? 2 : 0) : lane;
                float configuredGap = Mathf.Ceil(Mathf.Max(22f, speed *
                    (profile != null ? profile.ObstacleReactionSecondsAt(routeProgress) : .85f)) / TrackScheduler.GridStep)
                    * TrackScheduler.GridStep;
                nextObstacleDistance = patternEndZ + configuredGap;
                return;
            }
            bool isRoofBridge = (wave % 2 == 1);
            bool isThreeBusRamp = context.ActiveZone != ZoneType.Recovery && !context.RecoveryActive &&
                profile != null && random.NextDouble() < profile.ThreeBusRampChanceAt(routeProgress);

            if (isThreeBusRamp)
            {
                patternEndZ = SpawnThreeBusRamp(lane, startZ, rampEndZ, bus, speed,
                    profile != null && random.NextDouble() < profile.RoofJumpExitChanceAt(routeProgress),
                    speed * (profile != null ? profile.ObstacleReactionSecondsAt(routeProgress) : .85f), scheduler);
            }
            else if (isRoofBridge)
            {
                // Patrón RoofBridge: Dos colectivos contiguos unidos por una plataforma lateral
                // 1. Rampa de acceso en 'lane'
                scheduler.CreateRamp(lane, startZ, rampEndZ, 0f, busHeight, bus);
                scheduler.RegisterSurface(new TraversableSurface(lane, startZ, rampEndZ, 0f, busHeight, isRamp: true));

                // 2. Colectivo 1 en 'lane'
                float bus1EndZ = rampEndZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, rampEndZ + busLength * 0.5f, bus));
                scheduler.RegisterObstacleBlock(lane, startZ, bus1EndZ, 0f, busHeight);

                // 3. Colectivo 2 en 'neighborLane' (desfasado 1 celda de grilla adelante)
                float bridgeStartZ = rampEndZ + TrackScheduler.GridStep;
                float bus2StartZ = bridgeStartZ;
                float bus2EndZ = bus2StartZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(neighborLane, bus2StartZ + busLength * 0.5f, bus));
                scheduler.RegisterObstacleBlock(neighborLane, bus2StartZ, bus2EndZ, 0f, busHeight);

                // 4. Plataforma de cruce lateral entre ambos techos
                float bridgeEndZ = bus1EndZ;
                scheduler.CreateRoofBridge(lane, neighborLane, bridgeStartZ, bridgeEndZ, busHeight, bus);

                // ÚNICO camino de gemas continuo que guía al ciclista sin duplicación de carriles:
                // Colectivo 1 en 'lane' hasta llegar al puente:
                scheduler.RegisterSurface(new TraversableSurface(lane, rampEndZ, bridgeStartZ, busHeight, busHeight));
                // Colectivo 2 en 'neighborLane' desde el puente hasta la bajada:
                scheduler.RegisterSurface(new TraversableSurface(neighborLane, bridgeStartZ, bus2EndZ, busHeight, busHeight));

                // 5. Bajada balística o salto desde el techo hacia el piso.
                patternEndZ = RegisterRoofExit(scheduler, neighborLane, bus2EndZ, busHeight, speed,
                    profile != null && random.NextDouble() < profile.RoofJumpExitChanceAt(routeProgress));

                currentSafeLane = neighborLane;
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
                scheduler.RegisterObstacleBlock(lane, startZ, bus1EndZ, 0f, busHeight);

                // 3. Colectivo 2
                // Los techos deben tocarse: el jugador puede continuar por arriba sin saltar.
                float bus2StartZ = bus1EndZ;
                float bus2EndZ = bus2StartZ + busLength;
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, bus2StartZ + busLength * 0.5f, bus));
                scheduler.RegisterSurface(new TraversableSurface(lane, bus1EndZ, bus2EndZ, busHeight, busHeight));
                scheduler.RegisterObstacleBlock(lane, bus2StartZ, bus2EndZ, 0f, busHeight);

                // 4. Bajada balística o salto desde el techo hacia el piso.
                patternEndZ = RegisterRoofExit(scheduler, lane, bus2EndZ, busHeight, speed,
                    profile != null && random.NextDouble() < profile.RoofJumpExitChanceAt(routeProgress));

                currentSafeLane = lane;
            }

            float intensity = Mathf.Max(0.5f, context.Rules.ObstacleIntensity * context.ObstacleIntensity);
            float minimumGap = Mathf.Clamp(22f / intensity, 15f, 27f);
            float reactionGap = speed * (profile != null ? profile.ObstacleReactionSecondsAt(routeProgress) : .85f);
            float gap = Mathf.Ceil(Mathf.Max(minimumGap, reactionGap) / TrackScheduler.GridStep) * TrackScheduler.GridStep;
            nextObstacleDistance = patternEndZ + gap;
        }

        private float SpawnNewAerialPattern(VehiclePattern pattern, int lane, int neighborLane,
            float startZ, float rampEndZ, ObstacleDefinition bus, float speed, TrackScheduler scheduler)
        {
            float height = bus.Height;
            float length = bus.Length;
            float firstStart = rampEndZ;
            float firstEnd = firstStart + length;
            scheduler.CreateRamp(lane, startZ, rampEndZ, 0f, height, bus);
            scheduler.RegisterSurface(new TraversableSurface(lane, startZ, rampEndZ, 0f, height, isRamp: true));
            AddRoofBus(scheduler, lane, firstStart, firstEnd, bus, height);

            if (pattern == VehiclePattern.ArticulatedTripleChain)
            {
                float secondEnd = AddTouchingRoofBus(scheduler, lane, firstEnd, bus, height);
                float thirdEnd = AddTouchingRoofBus(scheduler, lane, secondEnd, bus, height);
                return RegisterRoofExit(scheduler, lane, thirdEnd, height, speed, false);
            }

            if (pattern == VehiclePattern.ParallelRoofBridge)
            {
                float secondStart = firstStart + TrackScheduler.GridStep;
                float secondEnd = secondStart + length;
                AddRoofBus(scheduler, neighborLane, secondStart, secondEnd, bus, height);
                scheduler.CreateRoofBridge(lane, neighborLane, secondStart, Mathf.Min(firstEnd, secondEnd), height, bus);
                scheduler.RegisterSurface(new TraversableSurface(lane, firstStart, secondStart, height, height));
                scheduler.RegisterSurface(new TraversableSurface(neighborLane, secondStart, secondEnd, height, height));
                return RegisterRoofExit(scheduler, neighborLane, secondEnd, height, speed, false);
            }

            int thirdLane = neighborLane == 1 ? (lane == 0 ? 2 : 0) : 1;
            int secondLane = neighborLane;
            float firstJumpStart = firstEnd - TrackScheduler.GridStep;
            float firstJumpEnd = firstJumpStart + speed * JumpArc.Duration;
            float secondStartBus = firstJumpEnd - length * .65f;
            float secondEndBus = secondStartBus + length;
            AddRoofBus(scheduler, secondLane, secondStartBus, secondEndBus, bus, height);
            scheduler.RegisterJumpArc(new JumpArcTrajectory(lane, secondLane, firstJumpStart, firstJumpEnd,
                height + 1.1f, height + 1.1f + JumpArc.ApexHeight, height + 1.1f));

            if (pattern == VehiclePattern.LateralRoofLeap)
                return RegisterRoofExit(scheduler, secondLane, secondEndBus, height, speed, false);

            int finalLane = pattern == VehiclePattern.ZigzagRoofJumps ? lane :
                pattern == VehiclePattern.SplitLaneBusHops ? (secondLane == 0 ? 2 : 0) : thirdLane;
            float secondJumpStart = secondEndBus - TrackScheduler.GridStep;
            float secondJumpEnd = secondJumpStart + speed * JumpArc.Duration;
            float thirdStartBus = secondJumpEnd - length * .65f;
            float thirdEndBus = thirdStartBus + length;
            AddRoofBus(scheduler, finalLane, thirdStartBus, thirdEndBus, bus, height);
            scheduler.RegisterJumpArc(new JumpArcTrajectory(secondLane, finalLane, secondJumpStart, secondJumpEnd,
                height + 1.1f, height + 1.1f + JumpArc.ApexHeight, height + 1.1f));
            return RegisterRoofExit(scheduler, finalLane, thirdEndBus, height, speed, false);
        }

        private static void AddRoofBus(TrackScheduler scheduler, int lane, float start, float end,
            ObstacleDefinition bus, float height)
        {
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, (start + end) * .5f, bus));
            scheduler.RegisterObstacleBlock(lane, start, end, 0f, height);
            scheduler.RegisterSurface(new TraversableSurface(lane, start, end, height, height));
        }

        private static float AddTouchingRoofBus(TrackScheduler scheduler, int lane, float start,
            ObstacleDefinition bus, float height)
        {
            float end = start + bus.Length;
            AddRoofBus(scheduler, lane, start, end, bus, height);
            return end;
        }

        private float SpawnRecoveryBusJump(int fromLane, int toLane, float startZ, float rampEndZ,
            ObstacleDefinition bus, float speed, TrackScheduler scheduler)
        {
            float height = bus.Height;
            float bus1Start = rampEndZ;
            float bus1End = bus1Start + bus.Length;
            scheduler.CreateRamp(fromLane, startZ, rampEndZ, 0f, height, bus);
            scheduler.RegisterSurface(new TraversableSurface(fromLane, startZ, rampEndZ, 0f, height, isRamp: true));
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(fromLane, bus1Start + bus.Length * .5f, bus));
            scheduler.RegisterObstacleBlock(fromLane, bus1Start, bus1End, 0f, height);
            scheduler.RegisterSurface(new TraversableSurface(fromLane, bus1Start, bus1End, height, height));

            // Separación calculada para que el salto de .72 s aterrice sobre el segundo techo.
            float jumpStart = bus1End - TrackScheduler.GridStep;
            float jumpEnd = jumpStart + speed * JumpArc.Duration;
            float bus2Start = jumpEnd - bus.Length * .65f;
            float bus2End = bus2Start + bus.Length;
            scheduler.EmitItem(TrackSpawnItem.CreateObstacle(toLane, bus2Start + bus.Length * .5f, bus));
            scheduler.RegisterObstacleBlock(toLane, bus2Start, bus2End, 0f, height);
            scheduler.RegisterSurface(new TraversableSurface(toLane, bus2Start, bus2End, height, height));
            scheduler.RegisterJumpArc(new JumpArcTrajectory(fromLane, toLane, jumpStart, jumpEnd,
                height + 1.1f, height + 1.1f + JumpArc.ApexHeight, height + 1.1f));

            currentSafeLane = toLane;
            return RegisterRoofExit(scheduler, toLane, bus2End, height, speed, requiresJump: false);
        }

        private float RegisterRoofExit(TrackScheduler scheduler, int lane, float roofEndZ, float roofHeight,
            float speed, bool requiresJump)
        {
            float startHeight = roofHeight + 1.1f;
            float landingTime;
            float arcStartZ;
            float arcStartHeight;

            if (requiresJump)
            {
                arcStartZ = roofEndZ - TrackScheduler.GridStep;
                float jumpEndZ = arcStartZ + speed * JumpArc.Duration;
                scheduler.RegisterJumpArc(new JumpArcTrajectory(lane, lane, arcStartZ, jumpEndZ,
                    startHeight, startHeight + JumpArc.ApexHeight, startHeight));

                // Al terminar el salto, el jugador ya está fuera del techo y conserva
                // la velocidad vertical de descenso del arco de salto.
                float fallSpeed = JumpArc.EndDescentSpeed;
                landingTime = (-fallSpeed + Mathf.Sqrt(fallSpeed * fallSpeed + 2f * 34f * roofHeight)) / 34f;
                arcStartZ = jumpEndZ;
                arcStartHeight = startHeight;
            }
            else
            {
                landingTime = Mathf.Sqrt(2f * roofHeight / 34f);
                arcStartZ = roofEndZ;
                arcStartHeight = startHeight;
            }

            float landingZ = arcStartZ + speed * landingTime;
            float fallApex = arcStartHeight + 34f * landingTime * landingTime / 8f;
            scheduler.RegisterJumpArc(new JumpArcTrajectory(lane, lane, arcStartZ, landingZ,
                arcStartHeight, fallApex, 1.1f));
            return landingZ + TrackScheduler.GridStep * 2f;
        }

        private float SpawnThreeBusRamp(int entryLane, float startZ, float rampEndZ, ObstacleDefinition bus,
            float speed, bool roofJumpExit, float approachDistance, TrackScheduler scheduler)
        {
            float busStartZ = rampEndZ;
            float busEndZ = busStartZ + bus.Length;
            float height = bus.Height;
            float approachStartZ = Mathf.Max(0f, startZ - approachDistance);
            scheduler.RegisterSurface(new TraversableSurface(entryLane, approachStartZ, startZ, 0f, 0f));
            scheduler.CreateRamp(entryLane, startZ, rampEndZ, 0f, height, bus);
            scheduler.RegisterSurface(new TraversableSurface(entryLane, startZ, rampEndZ, 0f, height, isRamp: true));

            for (int lane = 0; lane < 3; lane++)
            {
                scheduler.EmitItem(TrackSpawnItem.CreateObstacle(lane, busStartZ + bus.Length * .5f, bus));
                scheduler.RegisterObstacleBlock(lane, busStartZ, busEndZ, 0f, height);
            }

            int firstAcross = entryLane == 0 ? 1 : 0;
            int lastLane = 3 - entryLane - firstAcross;
            scheduler.CreateRoofBridge(entryLane, firstAcross, busStartZ, busEndZ, height, bus);
            scheduler.CreateRoofBridge(firstAcross, lastLane, busStartZ, busEndZ, height, bus);

            // Estela clara por encima del carril de entrada; los puentes permiten explorar
            // los otros techos sin obligar a cambiar de carril para recoger cada gema.
            scheduler.RegisterSurface(new TraversableSurface(entryLane, busStartZ, busEndZ, height, height));

            return RegisterRoofExit(scheduler, entryLane, busEndZ, height, speed, roofJumpExit);
        }
    }
}
