using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class SectionTransitionSettings
    {
        [Min(12f)] public float clearApproachMetres = 96f;
        [Min(12f)] public float clearExitMetres = 96f;
        [Min(12f)] public float announcementMetres = 120f;
        [Min(1f)] public float referenceApproachSpeed = 70f;
        [Min(1f)] public float announcementSeconds = 3f;
        [Min(0f)] public float rejoinSeconds = 1.25f;
        [Min(1f)] public float automaticDeceleration = 16f;
        [Header("Reingreso tras una decisión")]
        [Min(.1f), Tooltip("Segundos de extensión del patrón de salida según la velocidad al prepararlo.")]
        public float transitionExitPatternSeconds = 2.2f;
        [Min(72f), Tooltip("Extensión mínima del patrón de reingreso, independiente de la distancia de recupero.")]
        public float minimumTransitionExitPatternMetres = 72f;
        [Min(72f), Tooltip("Extensión máxima del patrón de reingreso para evitar separar demasiado sus encuentros.")]
        public float maximumTransitionExitPatternMetres = 180f;
        [Header("Desafío de recupero · distancia")]
        [Min(72f)] public float recoveryBaseDistanceMetres = 500f;
        [Min(0f), Tooltip("Metros añadidos por cada 100 gemas ofrecidas.")]
        public float recoveryMetresPer100Gems = 8f;
        [Min(0f), Tooltip("Metros añadidos por cada 1000 puntos ofrecidos.")]
        public float recoveryMetresPer1000Points = 8f;
        [Min(0f), Tooltip("Metros adicionales distribuidos según el avance de la ruta.")]
        public float recoveryProgressionMetres = 60f;
        [Min(0f), Tooltip("Tramo sin obstáculos ni coleccionables después del desafío, para recuperar el ritmo y leer la siguiente señal.")]
        public float recoveryExitBreathMetres = 60f;
        [Min(1)] public int purchasedGemMultiplier = 2;
        [Min(1)] public int purchasedPointMultiplier = 2;
        public RecoveryCollisionPolicy recoveryCollision = new RecoveryCollisionPolicy();
        public SectionTransitionSettings Capture()
        {
            var copy = (SectionTransitionSettings)MemberwiseClone();
            copy.recoveryCollision = new RecoveryCollisionPolicy
            {
                pointFraction = Mathf.Clamp01(recoveryCollision.pointFraction),
                minimumPointLoss = Mathf.Max(0, recoveryCollision.minimumPointLoss),
                gemFraction = Mathf.Clamp01(recoveryCollision.gemFraction)
            };
            return copy;
        }

        public float RecoveryDistance(int gems, long points, float routeProgress)
        {
            float byGems = Mathf.Max(0, gems) / 100f * Mathf.Max(0f, recoveryMetresPer100Gems);
            float byPoints = (float)Math.Max(0L, points) / 1000f * Mathf.Max(0f, recoveryMetresPer1000Points);
            float byProgress = Mathf.Clamp01(routeProgress) * Mathf.Max(0f, recoveryProgressionMetres);
            return Mathf.Max(72f, recoveryBaseDistanceMetres + byGems + byPoints + byProgress);
        }

        public float TransitionExitPatternDistance(float speed)
        {
            float minimum = Mathf.Max(72f, minimumTransitionExitPatternMetres);
            float maximum = Mathf.Max(minimum, maximumTransitionExitPatternMetres);
            float distance = Mathf.Max(0f, speed) * Mathf.Max(0f, transitionExitPatternSeconds);
            return Mathf.Clamp(distance, minimum, maximum);
        }
    }

    /// <summary>Immutable route facts shared by simulation and generation. No scene ownership.</summary>
    public sealed class SectionTransitionPlan
    {
        public readonly int Section;
        public readonly float Stop, Announce, Deceleration;
        public readonly TrackSpan Corridor;
        public readonly int Cost;
        public readonly float ExtraSpeed;
        private SectionTransitionPlan(int section, float stop, TrackSpan corridor, float announce,
            float deceleration, int cost, float extraSpeed)
        {
            Section = section; Stop = stop; Corridor = corridor; Announce = announce;
            Deceleration = deceleration; Cost = cost; ExtraSpeed = extraSpeed;
        }
        public float SpeedLimit(float distance) => (float)Math.Sqrt(2f * Deceleration * Math.Max(0f, Stop - distance));

        public static SectionTransitionPlan[] Build(RouteDefinition route, SectionTransitionSettings settings)
        {
            if (route == null) return Array.Empty<SectionTransitionPlan>();
            var result = new List<SectionTransitionPlan>();
            float start = 0f, previousExit = 0f;
            for (int i = 0; i < route.Sections.Length; i++)
            {
                var section = route.Sections[i];
                float length = section.ModuleCount * RoadStandard.ModuleLength;
                if (route.HasDecision(i))
                {
                    float stop = section.Transition ? start + 14f : start + length;
                    float referenceSpeed = Math.Max(1f, settings.referenceApproachSpeed);
                    float deceleration = section.Transition ? 28f : Math.Max(1f, settings.automaticDeceleration);
                    float approach = section.Transition ? 14f : Math.Max(settings.clearApproachMetres, referenceSpeed * referenceSpeed / (2f * deceleration) + 12f);
                    float entry = section.Transition ? start : Math.Max(previousExit + 12f, stop - approach);
                    float exit = stop + (section.Transition ? 36f : Math.Max(settings.clearExitMetres, referenceSpeed * Math.Max(0f, settings.rejoinSeconds) + 12f));
                    if (stop - entry >= 4f && (route.FinishDistance <= 0f || exit + 12f < route.FinishDistance))
                    {
                        var upgrade = route.SpeedOfferAt(i);
                        result.Add(new SectionTransitionPlan(i, stop, new TrackSpan(entry, exit),
                            entry,
                            deceleration, upgrade.SpeedUpgradeCost, upgrade.SpeedUpgradeMetresPerSecond));
                        previousExit = exit;
                    }
                }
                start += length;
            }
            return result.ToArray();
        }
    }
}
