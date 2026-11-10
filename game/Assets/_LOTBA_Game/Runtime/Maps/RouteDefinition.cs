using System;
using UnityEngine;

namespace Lotba.Game
{
    public enum TransitionTurn { Straight = 0, Left = -1, Right = 1 }

    [Serializable]
    public sealed class RouteSection
    {
        [SerializeField] private MapDefinition map;
        [Tooltip("Number of 12 m modules. Zero means infinite and is allowed only in the final section.")]
        [SerializeField, Min(0)] private int moduleCount;
        [SerializeField] private bool transition;
        [Tooltip("Visual turn when approaching this transition section.")]
        [SerializeField] private TransitionTurn transitionTurn;
        public MapDefinition Map => map;
        public int ModuleCount => moduleCount;
        public bool Transition => transition;
        public TransitionTurn Turn => transitionTurn;
#if UNITY_EDITOR
        internal void SetPreviewMap(MapDefinition value) => map = value;
#endif
    }

    /// <summary>Distance-based itinerary. A final zero-length section repeats indefinitely.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Maps/Route definition")]
    public sealed class RouteDefinition : ScriptableObject
    {
        [SerializeField] private RouteSection[] sections;
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
                    if (i == sections.Length - 1 && section.ModuleCount != 0) return "Set the final section to zero (infinite).";
                    if (section.Transition && (i == sections.Length - 1 || section.ModuleCount <= 0)) return "A transition needs a positive length and a destination section.";
                }
                return null;
            }
        }
#if UNITY_EDITOR
        /// <summary>Creates an unsaved, single-zone route for the artist preview.</summary>
        public static RouteDefinition CreatePreview(MapDefinition map)
        {
            var route = CreateInstance<RouteDefinition>();
            route.hideFlags = HideFlags.DontSave;
            route.sections = new[] { new RouteSection() };
            route.sections[0].SetPreviewMap(map);
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
