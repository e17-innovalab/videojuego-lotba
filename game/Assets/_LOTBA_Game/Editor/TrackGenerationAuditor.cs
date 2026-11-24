using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lotba.Game.Editor
{
    /// <summary>
    /// Auditor exhaustivo de generación de pista.
    /// Permite simular miles de metros de pista a través de múltiples semillas
    /// para verificar invariantes de jugabilidad sin necesidad de playtest manual.
    /// </summary>
    public static class TrackGenerationAuditor
    {
        private readonly struct EmittedItem
        {
            public readonly RunnerWorld.ItemKind Kind;
            public readonly int Lane;
            public readonly float TrackDistance;
            public readonly float Height;
            public readonly ObstacleDefinition Definition;
            public readonly int AirborneNote;

            public EmittedItem(TrackSpawnItem item)
            {
                Kind = item.Kind;
                Lane = item.Lane;
                TrackDistance = item.TrackDistance;
                Height = item.Height;
                Definition = item.Definition;
                AirborneNote = item.AirborneNote;
            }
        }

        [MenuItem("LOTBA/Audit Track Generation")]
        public static void RunAuditMenu()
        {
            RunAudit();
        }

        public static bool RunAudit()
        {
            var route = AssetDatabase.LoadAssetAtPath<RouteDefinition>("Assets/_LOTBA_Game/Maps/Routes/Definitions/Route_LaBocaToObelisco.asset");
            if (route == null)
            {
                Debug.LogError("[TrackAudit] No se encontró la ruta de prueba Route_LaBocaToObelisco.");
                return false;
            }

            int[] testSeeds = new int[] { 2710, 42, 12345, 9999, 777 };
            float simulationDistance = 2000f;
            float stepDistance = 5f;
            int totalConflicts = 0;
            int totalGemsAudited = 0;
            int totalObstaclesAudited = 0;

            Debug.Log($"[TrackAudit] Iniciando auditoría exhaustiva en 5 semillas ({simulationDistance * testSeeds.Length:N0} metros totales)...");

            for (int s = 0; s < testSeeds.Length; s++)
            {
                int seed = testSeeds[s];
                var scheduler = new TrackScheduler();
                scheduler.Initialize(null, seed);

                var emittedItems = new List<EmittedItem>(2048);
                scheduler.ItemEmitted += item => emittedItems.Add(new EmittedItem(item));

                var rules = ZoneRules.CreateDefault(ZoneType.Normal);

                for (float dist = 0f; dist <= simulationDistance; dist += stepDistance)
                {
                    float speed = 26f;
                    float spawnHorizon = dist + 80f;

                    var context = new GenerationContext(
                        distance: dist,
                        speed: speed,
                        currentLane: 1,
                        safeLane: 1,
                        spawnHorizon: spawnHorizon,
                        activeZone: ZoneType.Normal,
                        rules: rules,
                        route: route,
                        profile: null,
                        pinkGemsUnlocked: false,
                        recoveryActive: false,
                        recoveryZoneEnd: -1f,
                        seed: seed
                    );

                    scheduler.PopulateAhead(context);
                }

                // Separar colecciones para análisis cruzado O(N)
                var floorGems = new List<EmittedItem>(1024);
                var obstacles = new List<EmittedItem>(512);
                var letters = new List<EmittedItem>(64);
                var gemSlots = new Dictionary<int, int>(1024);

                for (int i = 0; i < emittedItems.Count; i++)
                {
                    var it = emittedItems[i];
                    if (it.Kind == RunnerWorld.ItemKind.Gem)
                    {
                        totalGemsAudited++;
                        if (it.Height <= 1.5f)
                            floorGems.Add(it);

                        // Invariante 1: No debe haber dos gemas en carriles distintos a la misma cota Z
                        int slot = Mathf.RoundToInt(it.TrackDistance / TrackScheduler.GridStep);
                        if (gemSlots.TryGetValue(slot, out int existingLane))
                        {
                            if (existingLane != it.Lane)
                            {
                                Debug.LogError($"[TrackAudit - FALLO] Semilla {seed}: Dos gemas en carriles distintos a la misma distancia Z={it.TrackDistance:F1}m (carriles {existingLane} y {it.Lane}).");
                                totalConflicts++;
                            }
                        }
                        else
                        {
                            gemSlots[slot] = it.Lane;
                        }
                    }
                    else if (it.Kind == RunnerWorld.ItemKind.Obstacle)
                    {
                        totalObstaclesAudited++;
                        obstacles.Add(it);
                    }
                }

                // Invariante 2: Ninguna gema de suelo puede coincidir longitudinalmente con un obstáculo en el mismo carril
                for (int g = 0; g < floorGems.Count; g++)
                {
                    var gem = floorGems[g];
                    for (int o = 0; o < obstacles.Count; o++)
                    {
                        var obs = obstacles[o];
                        if (gem.Lane != obs.Lane) continue;

                        float halfLen = obs.Definition != null ? obs.Definition.Length * 0.5f : 1.5f;
                        float obsStart = obs.TrackDistance - halfLen - 0.2f;
                        float obsEnd = obs.TrackDistance + halfLen + 0.2f;

                        if (gem.TrackDistance >= obsStart && gem.TrackDistance <= obsEnd)
                        {
                            string obsId = obs.Definition != null ? obs.Definition.Id : "desconocido";
                            Debug.LogError($"[TrackAudit - FALLO] Semilla {seed}: Gema de piso en carril {gem.Lane} a {gem.TrackDistance:F1}m se solapa con obstáculo '{obsId}' en [{obsStart:F1}m - {obsEnd:F1}m].");
                            totalConflicts++;
                        }
                    }
                }
            }

            if (totalConflicts == 0)
            {
                Debug.Log($"[TrackAudit - ÉXITO] 10,000 metros auditados con 0 conflictos. {totalGemsAudited:N0} gemas y {totalObstaclesAudited:N0} obstáculos validados sin solapamientos ni carriles duplicados.");
                return true;
            }
            else
            {
                Debug.LogError($"[TrackAudit - FALLO] Se detectaron {totalConflicts} conflictos de generación.");
                return false;
            }
        }

        public static void FindCenterSeeds()
        {
            var matching = new List<int>();
            for (int s = 1000; s <= 3000; s++)
            {
                var scheduler = new TrackScheduler();
                scheduler.Initialize(null, s);
                if (scheduler.GetCorridorLaneAt(0f) == 1 && scheduler.GetCorridorLaneAt(15f) == 1)
                {
                    matching.Add(s);
                }
            }
            Debug.Log($"[TrackAudit] Semillas con carril inicial 1 (centro): {string.Join(", ", matching.GetRange(0, Math.Min(10, matching.Count)))}");
        }

        public static void AuditSeedStart()
        {
            var scheduler = new TrackScheduler();
            scheduler.Initialize(null, 1001);
            var emitted = new List<string>();
            scheduler.ItemEmitted += item =>
            {
                if (item.TrackDistance <= 120f)
                {
                    emitted.Add($"{item.Kind} en Carril {item.Lane} a {item.TrackDistance:F1}m (H={item.Height:F1}m)");
                }
            };
            var route = AssetDatabase.LoadAssetAtPath<RouteDefinition>("Assets/_LOTBA_Game/Maps/Routes/Definitions/Route_LaBocaToObelisco.asset");
            var rules = ZoneRules.CreateDefault(ZoneType.Normal);
            for (float dist = 0f; dist <= 120f; dist += 5f)
            {
                var context = new GenerationContext(dist, 26f, 1, 1, dist + 80f, ZoneType.Normal, rules, route, null, false, false, -1f, 1001);
                scheduler.PopulateAhead(context);
            }
            Debug.Log($"[TrackAudit - Inicio Semilla 1001]:\n{string.Join("\n", emitted)}");
        }
    }
}
