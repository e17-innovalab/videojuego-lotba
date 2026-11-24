using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Orquestador central de la generación de pista.
    /// Coordina los spawners modulares, mantiene el registro de superficies transitables y arcos de salto,
    /// y asegura el cumplimiento estricto de las reglas de zona y el determinismo por semilla.
    /// </summary>
    public sealed class TrackScheduler
    {
        public const float GridStep = 3.0f;

        private readonly ObstacleSpawner obstacleSpawner = new ObstacleSpawner();
        private readonly GemSpawner gemSpawner = new GemSpawner();
        private readonly LetterSpawner letterSpawner = new LetterSpawner();

        private readonly List<TraversableSurface> activeSurfaces = new List<TraversableSurface>(32);
        private readonly List<JumpArcTrajectory> activeJumpArcs = new List<JumpArcTrajectory>(16);
        private readonly List<ObstacleBlock> activeBlocks = new List<ObstacleBlock>(64);
        private readonly List<TrackSpan> letterReservations = new List<TrackSpan>(8);
        private readonly Dictionary<int, int> activeGemLaneBySlot = new Dictionary<int, int>(256);
        private readonly List<int> gemSlotCleanupBuffer = new List<int>(32);

        public IReadOnlyList<TraversableSurface> ActiveSurfaces => activeSurfaces;
        public IReadOnlyList<JumpArcTrajectory> ActiveJumpArcs => activeJumpArcs;
        public event Action<TrackSpawnItem> ItemEmitted;

        private RunnerWorld world;
        private float recoveryStartDistance = -1f;
        private float recoveryEndDistance = -1f;
        private int baseSeed;

        public readonly struct ObstacleBlock
        {
            public readonly int Lane;
            public readonly float StartZ;
            public readonly float EndZ;
            public readonly float MinHeight;
            public readonly float MaxHeight;

            public ObstacleBlock(int lane, float startZ, float endZ, float minHeight = 0f, float maxHeight = 4f)
            {
                Lane = lane;
                StartZ = startZ;
                EndZ = endZ;
                MinHeight = minHeight;
                MaxHeight = maxHeight;
            }

            public bool Blocks(int lane, float z, float height = 1.1f) =>
                Lane == lane && z >= StartZ && z <= EndZ && height >= MinHeight && height <= MaxHeight;
        }

        public void Initialize(RunnerWorld owner, int seed)
        {
            world = owner;
            baseSeed = seed;
            Reset(seed);
        }

        public void Reset(int seed)
        {
            baseSeed = seed;
            activeSurfaces.Clear();
            activeJumpArcs.Clear();
            activeBlocks.Clear();
            letterReservations.Clear();
            activeGemLaneBySlot.Clear();
            gemSlotCleanupBuffer.Clear();
            recoveryStartDistance = -1f;
            recoveryEndDistance = -1f;

            obstacleSpawner.Reset(baseSeed);
            gemSpawner.Reset(baseSeed);
            letterSpawner.Reset(baseSeed);
        }

        public void NotifyRecoveryEntered(float startDistance, float endDistance)
        {
            recoveryStartDistance = startDistance;
            recoveryEndDistance = endDistance;
        }

        public void NotifyRecoveryExited()
        {
            // Mantiene la exclusión de gemas hasta el final del tramo
        }

        public bool IsGemForbiddenAt(float z)
        {
            if (recoveryStartDistance >= 0f && recoveryEndDistance > recoveryStartDistance)
            {
                // Dentro del rango completo de la zona de recupero, NUNCA se permiten gemas comunes
                if (z >= recoveryStartDistance - 2f && z <= recoveryEndDistance + 2f)
                    return true;
            }

            // Si está dentro de una zona restringida (transiciones o reservas de pista), prohibir gemas comunes
            if (IsZoneRestricted(z))
                return true;

            return false;
        }

        public bool IsZoneRestricted(float z, bool isRecovery = false)
        {
            if (world != null && world.TrackReservations.Blocks(z, 4f)) return true;
            if (!isRecovery && world != null && world.TransitionReservations.Blocks(z, 4f)) return true;
            return false;
        }

        public void RegisterLetterReservation(float z, float radius = 8f)
        {
            letterReservations.Add(new TrackSpan(z - radius, z + radius));
        }

        public bool IsObstacleForbiddenAt(float z, bool isRecovery = false)
        {
            if (IsZoneRestricted(z, isRecovery)) return true;

            // Zona de gracia post-recupero: 35 metros libres después de salir de la zona de recupero para que no haya una pared de obstáculos repentina
            if (recoveryEndDistance > 0f && z >= recoveryEndDistance && z < recoveryEndDistance + 35f)
                return true;

            for (int i = 0; i < letterReservations.Count; i++)
            {
                if (z >= letterReservations[i].Start && z <= letterReservations[i].End)
                    return true;
            }
            if (TryGetSurfaceAt(z, out _)) return true;
            return false;
        }

        public bool IsObstacleSpanForbidden(float startZ, float endZ, bool isRecovery = false)
        {
            float step = GridStep;
            for (float z = startZ; z <= endZ; z += step)
            {
                if (IsObstacleForbiddenAt(z, isRecovery))
                    return true;
            }
            return IsObstacleForbiddenAt(endZ, isRecovery);
        }

        public bool IsLaneClearOfObstacles(int lane, float startZ, float endZ)
        {
            for (int i = 0; i < activeBlocks.Count; i++)
            {
                var block = activeBlocks[i];
                if (block.Lane == lane && block.MinHeight <= 0.5f)
                {
                    // Solapamiento de intervalos [StartZ, EndZ] con [startZ, endZ]
                    if (block.EndZ >= startZ && block.StartZ <= endZ)
                        return false;
                }
            }
            return true;
        }

        public int GetSafeLaneForSpan(float startZ, float endZ, int preferredLane = -1, float clearance = 6.0f)
        {
            int targetLane = preferredLane >= 0 ? preferredLane : GetCorridorLaneAt((startZ + endZ) * 0.5f);

            if (IsLaneClearOfObstacles(targetLane, startZ - clearance, endZ + clearance))
            {
                return targetLane;
            }

            int[] tryOrder = targetLane == 1
                ? new int[] { 0, 2 }
                : (targetLane == 0 ? new int[] { 1, 2 } : new int[] { 1, 0 });

            for (int i = 0; i < tryOrder.Length; i++)
            {
                int lane = tryOrder[i];
                if (IsLaneClearOfObstacles(lane, startZ - clearance, endZ + clearance))
                {
                    return lane;
                }
            }

            return targetLane;
        }

        public int GetCorridorLaneAt(float z)
        {
            // Cambia de carril preferente cada 38 metros de forma determinista y orgánica
            int segment = Mathf.FloorToInt(z / 38f);
            uint hash = (uint)(baseSeed ^ (segment * 0x27d4eb2d));
            hash = (hash ^ (hash >> 15)) * 0x85ebca6b;
            hash = (hash ^ (hash >> 13)) * 0xc2b2ae35;
            return (int)((hash ^ (hash >> 16)) % 3);
        }

        public void RegisterSurface(TraversableSurface surface)
        {
            activeSurfaces.Add(surface);
        }

        public void RegisterJumpArc(JumpArcTrajectory arc)
        {
            activeJumpArcs.Add(arc);
        }

        public void RegisterObstacleBlock(int lane, float startZ, float endZ, float minHeight = 0f, float maxHeight = 4f)
        {
            activeBlocks.Add(new ObstacleBlock(lane, startZ, endZ, minHeight, maxHeight));
        }

        public bool HasObstacleAt(int lane, float z, float height = 1.1f, float padding = 1.5f)
        {
            for (int i = 0; i < activeBlocks.Count; i++)
            {
                var block = activeBlocks[i];
                if (block.Lane == lane && z >= (block.StartZ - padding) && z <= (block.EndZ + padding))
                {
                    // Para gemas de piso (altura <= 1.5m), cualquier obstáculo que toque el suelo
                    // (incluso cajas bajas de 0.5m o conos) prohíbe gemas para que nunca queden encima ni solapadas.
                    if (height <= 1.5f && block.MinHeight <= 0.5f)
                        return true;

                    if (height >= (block.MinHeight - 0.2f) && height <= (block.MaxHeight + 0.2f))
                        return true;
                }
            }
            return false;
        }

        public bool IsLetterAt(float z, float padding = 4f)
        {
            for (int i = 0; i < letterReservations.Count; i++)
            {
                if (z >= (letterReservations[i].Start - padding) && z <= (letterReservations[i].End + padding))
                    return true;
            }
            return false;
        }

        public bool TryGetSurfaceAt(float z, out TraversableSurface foundSurface, float padding = 3.0f)
        {
            for (int i = activeSurfaces.Count - 1; i >= 0; i--)
            {
                if (z >= (activeSurfaces[i].StartZ - padding) && z <= (activeSurfaces[i].EndZ + padding))
                {
                    foundSurface = activeSurfaces[i];
                    return true;
                }
            }
            foundSurface = default;
            return false;
        }

        public bool TryGetJumpArcAt(float z, out JumpArcTrajectory foundArc, float padding = 3.0f)
        {
            for (int i = activeJumpArcs.Count - 1; i >= 0; i--)
            {
                if (z >= (activeJumpArcs[i].StartZ - padding) && z <= (activeJumpArcs[i].EndZ + padding))
                {
                    foundArc = activeJumpArcs[i];
                    return true;
                }
            }
            foundArc = default;
            return false;
        }

        public int GetSafeLaneAt(float z, int preferredLane = -1)
        {
            int targetLane = preferredLane >= 0 ? preferredLane : GetCorridorLaneAt(z);

            // Verifica si targetLane está libre de obstáculos a nivel de suelo con tolerancia de grid
            if (!HasObstacleAt(targetLane, z, 1.1f, 1.5f))
            {
                return targetLane;
            }

            // Busca el carril contiguo libre más conveniente
            int[] tryOrder = targetLane == 1
                ? new int[] { 0, 2 }
                : (targetLane == 0 ? new int[] { 1, 2 } : new int[] { 1, 0 });

            for (int i = 0; i < tryOrder.Length; i++)
            {
                int lane = tryOrder[i];
                if (!HasObstacleAt(lane, z, 1.1f, 1.5f))
                {
                    return lane;
                }
            }

            return targetLane;
        }

        public void EmitItem(TrackSpawnItem item)
        {
            // Garantía estricta de camino único: en una misma cota de pista Z NUNCA se permiten gemas en dos carriles distintos
            if (item.Kind == RunnerWorld.ItemKind.Gem)
            {
                // Verificación de colisión estricta con obstáculos físicos
                if (item.Height <= 1.5f && HasObstacleAt(item.Lane, item.TrackDistance, item.Height, padding: 2.0f))
                {
                    Debug.LogWarning($"[LOTBA-AUDIT] Gema de piso en carril {item.Lane} a {item.TrackDistance:F1}m rechazada porque coincide con un obstáculo físico.");
                    return;
                }

                int slot = Mathf.RoundToInt(item.TrackDistance / GridStep);
                if (activeGemLaneBySlot.TryGetValue(slot, out int existingLane))
                {
                    if (existingLane != item.Lane)
                    {
                        // Ya existe una gema activa en otro carril a esta misma cota Z; descartar para no crear gemas paralelas
                        return;
                    }
                }
                else
                {
                    activeGemLaneBySlot[slot] = item.Lane;
                }
            }

            ItemEmitted?.Invoke(item);

            if (world != null)
            {
                world.MaterializeSpawnItem(item);
            }
        }

        public void CreateRamp(int lane, float startZ, float endZ, float low, float high, ObstacleDefinition bus)
        {
            if (world != null)
            {
                world.CreateRamp(lane, startZ, endZ, low, high, bus);
            }
        }

        public void CreateRoofBridge(int fromLane, int toLane, float startZ, float endZ, float height, ObstacleDefinition bus)
        {
            if (world != null)
            {
                world.CreateRoofBridge(fromLane, toLane, startZ, endZ, height, bus);
            }
        }

        public void RequestLetterPlacement(float z, int lane)
        {
            if (world != null)
            {
                world.TryPlaceLetterAt(z, lane);
            }
        }

        public void PopulateAhead(in GenerationContext context)
        {
            // Limpia registros viejos que ya quedaron detrás del ciclista
            float cleanupZ = context.Distance - 30f;
            for (int i = activeSurfaces.Count - 1; i >= 0; i--)
                if (activeSurfaces[i].EndZ < cleanupZ) activeSurfaces.RemoveAt(i);
            for (int i = activeJumpArcs.Count - 1; i >= 0; i--)
                if (activeJumpArcs[i].EndZ < cleanupZ) activeJumpArcs.RemoveAt(i);
            for (int i = activeBlocks.Count - 1; i >= 0; i--)
                if (activeBlocks[i].EndZ < cleanupZ) activeBlocks.RemoveAt(i);
            for (int i = letterReservations.Count - 1; i >= 0; i--)
                if (letterReservations[i].End < cleanupZ) letterReservations.RemoveAt(i);

            int minSlotToKeep = Mathf.FloorToInt(cleanupZ / GridStep);
            gemSlotCleanupBuffer.Clear();
            foreach (var kvp in activeGemLaneBySlot)
            {
                if (kvp.Key < minSlotToKeep)
                    gemSlotCleanupBuffer.Add(kvp.Key);
            }
            for (int i = 0; i < gemSlotCleanupBuffer.Count; i++)
            {
                activeGemLaneBySlot.Remove(gemSlotCleanupBuffer[i]);
            }

            // CRÍTICO: Geometría física (letras y obstáculos) SIEMPRE se genera con un horizonte adelantado (+48m).
            // De este modo, la pista física está 100% resuelta y sellada antes de que el GemSpawner intente trazar el camino.
            var geometryContext = context.WithHorizon(context.SpawnHorizon + 48f);

            // 1. Letras coleccionables ("HABLAR ES GANAR") y registro de sus reservas de despeje
            letterSpawner.Populate(geometryContext, this);

            // 2. Geometría (Obstáculos, Colectivos, Superficies) respetando reservas
            obstacleSpawner.Populate(geometryContext, this);

            // 3. Gemas (montadas sobre superficies, arcos de salto o en camino libre)
            gemSpawner.Populate(context, this);
        }
    }
}
