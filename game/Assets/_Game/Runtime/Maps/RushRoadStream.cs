using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.CityRush
{
    /// <summary>Streams standardized visual modules independently of pickups, collisions and run state.</summary>
    public sealed class RushRoadStream : MonoBehaviour
    {
        [SerializeField] private RushRouteDefinition route;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameObject editorPreview;
        private const int Behind = 2, VisibleModules = 14;
        private readonly Dictionary<RushRoadModule, Queue<RushRoadModule>> pools = new Dictionary<RushRoadModule, Queue<RushRoadModule>>();
        private readonly List<LandmarkView> landmarks = new List<LandmarkView>();
        private Slot[] slots;
        private sealed class Slot { public int Index = int.MinValue; public RushRoadModule Source, View; }
        private sealed class LandmarkView { public Transform View; public float Start, Offset; }
        public RushRouteDefinition Route => route;
        public void Initialize()
        {
            if (editorPreview != null) editorPreview.SetActive(false);
            slots = new Slot[VisibleModules];
            for (int i = 0; i < slots.Length; i++) slots[i] = new Slot();
            var capacity = new Dictionary<RushRoadModule, int>();
            float start = 0;
            foreach (var section in route.Sections)
            {
                RushRoadModule[] modules = section.Map.Modules;
                foreach (RushRoadModule prefab in modules)
                {
                    int frequency = 0;
                    foreach (var candidate in modules) if (candidate == prefab) frequency++;
                    int needed = Mathf.CeilToInt(VisibleModules * frequency / (float)modules.Length) + 2;
                    if (!capacity.ContainsKey(prefab) || capacity[prefab] < needed) capacity[prefab] = needed;
                }
                if (section.Map.Landmark != null)
                {
                    Transform landmark = Instantiate(section.Map.Landmark, transform).transform;
                    landmarks.Add(new LandmarkView { View = landmark, Start = start, Offset = section.Map.LandmarkDistance });
                }
                start += section.ModuleCount * RushRoadStandard.ModuleLength;
            }
            foreach (var entry in capacity)
            {
                var pool = new Queue<RushRoadModule>(entry.Value);
                for (int i = 0; i < entry.Value; i++)
                {
                    RushRoadModule view = Instantiate(entry.Key, transform); view.gameObject.SetActive(false); pool.Enqueue(view);
                }
                pools.Add(entry.Key, pool);
            }
        }
        public void ResetRoad()
        {
            foreach (Slot slot in slots) Release(slot);
            Advance(0);
        }
        public void Advance(float distance)
        {
            int first = Mathf.FloorToInt(distance / RushRoadStandard.ModuleLength) - Behind;
            for (int i = 0; i < VisibleModules; i++)
            {
                int index = first + i;
                Slot slot = slots[((index % VisibleModules) + VisibleModules) % VisibleModules];
                if (slot.Index != index)
                {
                    Release(slot); slot.Source = route.ModuleAt(index);
                    // Pools are warmed for the maximum simultaneous use of each variant.
                    slot.View = pools[slot.Source].Dequeue(); slot.Index = index; slot.View.SetRoadMaterial(route.MapAtModule(index).RoadMaterial); slot.View.gameObject.SetActive(true);
                }
                slot.View.transform.localPosition = new Vector3(0, 0, index * RushRoadStandard.ModuleLength - distance);
            }
            foreach (var landmark in landmarks)
                landmark.View.localPosition = new Vector3(0, 0, Mathf.Max(landmark.Offset, landmark.Start + landmark.Offset - distance));
            ApplyEnvironment(distance);
        }
        private void ApplyEnvironment(float distance)
        {
            int index = route.SectionAt(distance, out float start);
            RushRouteSection section = route.Sections[index];
            RushMapDefinition from = section.Map, to = from;
            float blend = 0;
            if (section.Transition && index + 1 < route.Sections.Length)
            {
                to = route.Sections[index + 1].Map;
                blend = Mathf.Clamp01((distance - start) / (section.ModuleCount * RushRoadStandard.ModuleLength));
            }
            worldCamera.backgroundColor = Color.Lerp(from.Sky, to.Sky, blend);
            RenderSettings.fogColor = Color.Lerp(from.Fog, to.Fog, blend);
            RenderSettings.fogStartDistance = Mathf.Lerp(from.FogStart, to.FogStart, blend);
            RenderSettings.fogEndDistance = Mathf.Lerp(from.FogEnd, to.FogEnd, blend);
        }
        private void Release(Slot slot)
        {
            if (slot.View == null) return;
            slot.View.gameObject.SetActive(false); pools[slot.Source].Enqueue(slot.View); slot.View = null; slot.Index = int.MinValue;
        }
    }
}
