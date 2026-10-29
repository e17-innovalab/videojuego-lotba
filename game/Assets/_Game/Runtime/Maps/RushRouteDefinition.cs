using System;
using UnityEngine;

namespace Lotba.CityRush
{
    [Serializable]
    public sealed class RushRouteSection
    {
        [SerializeField] private RushMapDefinition map;
        [Tooltip("Number of 12 m modules; the last section repeats indefinitely. Use positive counts before it.")]
        [SerializeField, Min(0)] private int moduleCount;
        [SerializeField] private bool transition;
        public RushMapDefinition Map => map;
        public int ModuleCount => moduleCount;
        public bool Transition => transition;
    }

    /// <summary>Distance-based visual itinerary. The last section continues indefinitely.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Maps/Route definition")]
    public sealed class RushRouteDefinition : ScriptableObject
    {
        [SerializeField] private RushRouteSection[] sections;
        public RushRouteSection[] Sections => sections;
        public int SectionAt(float distance, out float startDistance)
        {
            startDistance = 0;
            for (int i = 0; i < sections.Length - 1; i++)
            {
                float end = startDistance + sections[i].ModuleCount * RushRoadStandard.ModuleLength;
                if (distance < end) return i;
                startDistance = end;
            }
            return sections.Length - 1;
        }
        public RushMapDefinition MapAtDistance(float distance) => sections[SectionAt(distance, out _)].Map;
        public RushMapDefinition MapAtModule(int moduleIndex)
        {
            return sections[SectionAt(Mathf.Max(0, moduleIndex) * RushRoadStandard.ModuleLength, out _)].Map;
        }
        public RushRoadModule ModuleAt(int moduleIndex)
        {
            float distance = Mathf.Max(0, moduleIndex) * RushRoadStandard.ModuleLength;
            int section = SectionAt(distance, out float start);
            int local = moduleIndex - Mathf.RoundToInt(start / RushRoadStandard.ModuleLength);
            return sections[section].Map.ModuleAt(local);
        }
        public bool IsTransition(float distance)
        {
            int section = SectionAt(distance, out _);
            return sections[section].Transition;
        }
        public bool NeedsGentlePatterns(float near, float far)
        {
            // The whole wave, not only its first coin, must fit inside the section.
            int first = SectionAt(near, out _), last = SectionAt(far, out _);
            return first != last || sections[first].Transition || sections[last].Transition;
        }
    }
}
