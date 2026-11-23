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
        [Min(72f)] public float recoveryDistanceMetres = 500f;
        [Min(0)] public int maximumRecoveryGems = 20;
        [Min(0)] public int maximumRecoveryPoints = 2000;
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
