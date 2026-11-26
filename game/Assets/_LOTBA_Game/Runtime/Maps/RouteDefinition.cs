using System;
using UnityEngine;

namespace Lotba.Game
{
    public enum TransitionTurn { Straight = 0, Left = -1, Right = 1 }
    public enum BackdropExitSection { CurrentSection = 0, NextTransition = 1 }

    [Serializable]
    public sealed class RouteSection
    {
        [SerializeField] private MapDefinition map;
        [Tooltip("Optional encounter tuning for this section. Empty uses the default generation.")]
        [SerializeField] private EncounterProfile encounterProfile;
        [Tooltip("Gem cost at this section's exit. Zero calculates it from the previous playable zone's expected gems; use a positive value to override. Zero cost with zero speed increase keeps the temporary boost offer.")]
        [SerializeField, Min(0)] private int speedUpgradeCost;
        [SerializeField, Min(0f)] private float speedUpgradeMetresPerSecond;
        [Tooltip("Whether collectible letters may be generated in this route section.")]
        [SerializeField] private bool allowLetters = true;
        [Tooltip("Number of 12 m modules. Zero means infinite and is allowed only in the final section.")]
        [SerializeField, Min(0)] private int moduleCount;
        [SerializeField] private bool transition;
        [Tooltip("Visual turn when approaching this transition section.")]
        [SerializeField] private TransitionTurn transitionTurn;
        [Tooltip("Section whose length defines the backdrop exit interval.")]
        [SerializeField] private BackdropExitSection backdropExitSection;
        [Tooltip("Normalized start and end of the exit within the selected section.")]
        [SerializeField] private Vector2 backdropExitRange = new Vector2(0f, 1f);
        public MapDefinition Map => map;
        public EncounterProfile EncounterProfile => encounterProfile;
        public int SpeedUpgradeCost => speedUpgradeCost;
        public float SpeedUpgradeMetresPerSecond => speedUpgradeMetresPerSecond;
        public bool AllowLetters => allowLetters;
        public int ModuleCount => moduleCount;
        public bool Transition => transition;
        public TransitionTurn Turn => transitionTurn;
        public BackdropExitSection BackdropExit => backdropExitSection;
        public Vector2 BackdropExitRange => backdropExitRange;
#if UNITY_EDITOR
        internal void ConfigurePreview(MapDefinition previewMap, EncounterProfile profile)
        {
            map = previewMap;
            encounterProfile = profile;
        }
#endif
    }

    /// <summary>Distance-based itinerary. A final zero-length section repeats indefinitely.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Maps/Route definition")]
    public sealed class RouteDefinition : ScriptableObject
    {
        [SerializeField] private RouteSection[] sections;
        [SerializeField, Min(0f)] private float finalSectionSpeedBonus = 7f;
        [SerializeField, Min(1f)] private float finalSectionAccelerationDistance = 1800f;
        public float FinalSectionSpeedBonus => finalSectionSpeedBonus;

        public float SpeedBonusAt(float distance)
        {
            int index = SectionAt(distance, out float start);
            if (index != sections.Length - 1) return 0f;
            float span = finalSectionAccelerationDistance;
            if (sections[index].ModuleCount > 0)
                span = Mathf.Min(span, sections[index].ModuleCount * RoadStandard.ModuleLength);
            return finalSectionSpeedBonus * Mathf.Clamp01((distance - start) / Mathf.Max(1f, span));
        }
        [Tooltip("Target for the last scheduled letter, in modules from the selected zone or final section.")]
        [SerializeField, Min(0)] private int lastLetterFinalSectionModule;
        [SerializeField] private MapDefinition lastLetterZone;
        public MapDefinition LastLetterZone => lastLetterZone;
        public int LastLetterFinalSectionModule => lastLetterFinalSectionModule;
        public float FinishDistance
        {
            get
            {
                if (sections == null || sections.Length == 0 || sections[sections.Length - 1].ModuleCount == 0)
                    return 0f;
                float distance = 0f;
                foreach (RouteSection section in sections) distance += section.ModuleCount * RoadStandard.ModuleLength;
                return distance;
            }
        }
        public RouteSection[] Sections => sections;
        public string ValidationError
        {
            get
            {
                if (sections == null || sections.Length == 0) return "Add at least one section.";
                for (int i = 0; i < sections.Length; i++)
                {
                    var section = sections[i];
                    if (section == null || section.Map == null) return $"Section {i + 1}: assign a zone.";
                    if (section.Map.ValidationError != null) return $"Section {i + 1}: {section.Map.ValidationError}";
                    if (i < sections.Length - 1 && section.ModuleCount <= 0) return "Only the final section can use zero (infinite).";
                    if (section.Transition && (i == sections.Length - 1 || section.ModuleCount <= 0)) return "A transition needs a positive length and a destination section.";
                    if (section.Map.ZoneBackdrop != null && section.ModuleCount > 0)
                    {
                        Vector2 range = section.BackdropExitRange;
                        if (range.x < 0f || range.y > 1f || range.x >= range.y)
                            return $"Section {i + 1}: backdrop exit must use an increasing range from 0 to 1.";
                        if (section.BackdropExit == BackdropExitSection.NextTransition &&
                            (i + 1 >= sections.Length || sections[i + 1] == null ||
                             !sections[i + 1].Transition || sections[i + 1].ModuleCount <= 0))
                            return $"Section {i + 1}: backdrop exit needs a finite transition immediately after this section.";
                    }
                }
                return null;
            }
        }

        internal Vector2 BackdropExitInterval(int sectionIndex, float sectionStart)
        {
            RouteSection section = sections[sectionIndex];
            if (section.ModuleCount == 0)
                return new Vector2(float.PositiveInfinity, float.PositiveInfinity);

            float length = section.ModuleCount * RoadStandard.ModuleLength;
            if (section.BackdropExit == BackdropExitSection.NextTransition)
            {
                sectionStart += length;
                length = sections[sectionIndex + 1].ModuleCount * RoadStandard.ModuleLength;
            }
            return Vector2.one * sectionStart + section.BackdropExitRange * length;
        }
#if UNITY_EDITOR
        /// <summary>Creates an unsaved, single-zone route for the artist preview.</summary>
        public static RouteDefinition CreatePreview(MapDefinition map, EncounterProfile profile = null)
        {
            var route = CreateInstance<RouteDefinition>();
            route.hideFlags = HideFlags.DontSave;
            route.sections = new[] { new RouteSection() };
            route.sections[0].ConfigurePreview(map, profile);
            return route;
        }
#endif
        public int SectionAt(float distance, out float startDistance)
        {
            startDistance = 0;
            for (int i = 0; i < sections.Length - 1; i++)
            {
                float end = startDistance + sections[i].ModuleCount * RoadStandard.ModuleLength;
                if (distance < end) return i;
                startDistance = end;
            }
            return sections.Length - 1;
        }
        public MapDefinition MapAtDistance(float distance) => sections[SectionAt(distance, out _)].Map;
        public bool AllowLettersAt(float distance) => sections[SectionAt(distance, out _)].AllowLetters;
        public MapDefinition DestinationMapAfterSection(int sectionIndex)
        {
            for (int i = sectionIndex + 1; i < sections.Length; i++)
                if (!sections[i].Transition) return sections[i].Map;
            return sectionIndex >= 0 && sectionIndex < sections.Length ? sections[sectionIndex].Map : null;
        }
        public float SectionEndDistanceAt(float distance)
        {
            int section = SectionAt(distance, out float start);
            return sections[section].ModuleCount == 0
                ? float.PositiveInfinity
                : start + sections[section].ModuleCount * RoadStandard.ModuleLength;
        }
        public bool HasDecision(int index) => sections[index].ModuleCount > 0 && index < sections.Length - 1 &&
            (sections[index].Transition || (!sections[index + 1].Transition && !sections[index].Transition));

        public RouteSection SpeedOfferAt(int index) =>
            sections[index].Transition && sections[index].SpeedUpgradeCost == 0 && index > 0
                ? sections[index - 1] : sections[index];

        public float MaximumDecisionSpeed
        {
            get
            {
                float total = 0f;
                int temporaryOffers = 0;
                for (int index = 0; index < sections.Length; index++)
                {
                    if (!HasDecision(index)) continue;
                    RouteSection offer = SpeedOfferAt(index);
                    if (offer.SpeedUpgradeCost > 0) total += offer.SpeedUpgradeMetresPerSecond;
                    else temporaryOffers++;
                }
                return total + RunSession.RiskSpeedForFullIntensity * Mathf.Min(1f, temporaryOffers / 3f);
            }
        }

        public bool TouchesDecisionApproach(float from, float to)
        {
            int index = SectionAt(from, out float start);
            for (; index < sections.Length && start <= to; index++)
            {
                float length = sections[index].ModuleCount * RoadStandard.ModuleLength;
                float midpoint = start + length * .5f;
                if (HasDecision(index) && from <= midpoint + Mathf.Min(14f, length * .25f) &&
                    to >= midpoint - Mathf.Min(24f, length * .25f)) return true;
                start += length;
            }
            return false;
        }
        public MapDefinition MapAtModule(int moduleIndex)
        {
            return sections[SectionAt(Mathf.Max(0, moduleIndex) * RoadStandard.ModuleLength, out _)].Map;
        }
        public RoadModule ModuleAt(int moduleIndex)
        {
            float distance = Mathf.Max(0, moduleIndex) * RoadStandard.ModuleLength;
            int section = SectionAt(distance, out float start);
            int local = moduleIndex - Mathf.RoundToInt(start / RoadStandard.ModuleLength);
            return sections[section].Map.ModuleAt(local);
        }
        public bool IsTransition(float distance)
        {
            int section = SectionAt(distance, out _);
            return sections[section].Transition;
        }

        public float TransitionBendAt(float distance, float leadDistance)
        {
            float start = 0f, previousTurnEnd = 0f;
            for (int i = 0; i < sections.Length - 1; i++)
            {
                RouteSection section = sections[i];
                float end = start + section.ModuleCount * RoadStandard.ModuleLength;
                if (section.Transition && section.Turn != TransitionTurn.Straight)
                {
                    float leadStart = Mathf.Max(previousTurnEnd, start - leadDistance);
                    if (distance < leadStart) return 0f;
                    if (distance < end)
                    {
                        float peak = leadStart < start ? start : (start + end) * .5f;
                        float weight = distance < peak
                            ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(leadStart, peak, distance))
                            : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(peak, end, distance));
                        return (int)section.Turn * weight;
                    }
                    previousTurnEnd = end;
                }
                start = end;
            }
            return 0f;
        }
        public bool NeedsGentlePatterns(float near, float far)
        {
            // The whole wave, not only its first gem, must fit inside the section.
            int first = SectionAt(near, out _), last = SectionAt(far, out _);
            return first != last || sections[first].Transition || sections[last].Transition;
        }
    }
}
