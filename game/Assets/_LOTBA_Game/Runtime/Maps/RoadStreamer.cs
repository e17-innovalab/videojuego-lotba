using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Streams standardized visual modules independently of pickups, collisions and run state.</summary>
    public sealed class RoadStreamer : MonoBehaviour
    {
        [SerializeField] private RouteDefinition route;
        [SerializeField] private Camera worldCamera;
        private const int Behind = 2, MinimumVisibleModules = 14;
        private readonly Dictionary<RoadModule, Queue<RoadModule>> pools = new Dictionary<RoadModule, Queue<RoadModule>>();
        private readonly List<LandmarkView> landmarks = new List<LandmarkView>();
        private readonly List<BackdropView> backdrops = new List<BackdropView>();
        private Slot[] slots;
        private Shader bendShader;
        private bool bendEnabled;
        private Mesh bendRoadMesh;
        private readonly Dictionary<MeshFilter, Mesh> bendRoadSources = new Dictionary<MeshFilter, Mesh>();
        private readonly Dictionary<Material, Material> bendMaterials = new Dictionary<Material, Material>();
        private readonly Dictionary<Renderer, Material[]> bendSources = new Dictionary<Renderer, Material[]>();
        private static readonly int DepthBend = Shader.PropertyToID("_LotbaDepthBend");
        private sealed class Slot { public int Index = int.MinValue; public RoadModule Source, View; }
        private sealed class LandmarkView { public Transform View; public float Start, Offset, Lateral; public bool Pass; }
        private sealed class BackdropView { public Transform View; public float Start, PassDistance; public Vector2 ExitInterval; }
        public RouteDefinition Route => route;
        private bool UsesLinearFog => RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear;
        public float VisibilityDistance => UsesLinearFog
            ? Mathf.Min(worldCamera.farClipPlane, RenderSettings.fogEndDistance)
            : worldCamera.farClipPlane;
        /// <summary>Assign before Initialize; changing a live route requires rebuilding its pools.</summary>
        public void SetRoute(RouteDefinition value) => route = value;
        public void Initialize(Shader depthBendShader = null)
        {
            bendShader = depthBendShader != null && depthBendShader.isSupported ? depthBendShader : null;
            if (route == null) throw new InvalidOperationException("Assign a route to RoadStreamer in the Inspector.");
            if (route.ValidationError != null) throw new InvalidOperationException(route.ValidationError);
            float longestVisibility = worldCamera.farClipPlane;
            if (UsesLinearFog)
            {
                float longestFog = 0f;
                foreach (RouteSection section in route.Sections)
                    longestFog = Mathf.Max(longestFog, section.Map.FogEnd);
                longestVisibility = Mathf.Min(longestVisibility, longestFog);
            }
            // Keep the road under distant traffic even near the end of the current module.
            int moduleCount = Mathf.Max(MinimumVisibleModules,
                Behind + 1 + Mathf.CeilToInt(longestVisibility / RoadStandard.ModuleLength));
            slots = new Slot[moduleCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = new Slot();
            var capacity = new Dictionary<RoadModule, int>();
            float start = 0;
            for (int sectionIndex = 0; sectionIndex < route.Sections.Length; sectionIndex++)
            {
                RouteSection section = route.Sections[sectionIndex];
                RoadModule[] modules = section.Map.Modules;
                foreach (RoadModule prefab in modules)
                {
                    int frequency = 0;
                    foreach (var candidate in modules) if (candidate == prefab) frequency++;
                    int needed = Mathf.CeilToInt(slots.Length * frequency / (float)modules.Length) + 2;
                    if (!capacity.ContainsKey(prefab) || capacity[prefab] < needed) capacity[prefab] = needed;
                }
                if (section.Map.Landmark != null)
                {
                    Transform landmark = Instantiate(section.Map.Landmark, transform).transform;
                    RegisterBendRenderers(landmark);
                    landmarks.Add(new LandmarkView { View = landmark, Start = start, Offset = section.Map.LandmarkDistance,
                        Lateral = section.LandmarkLateralOffset, Pass = section.PassLandmark });
                }
                if (section.Map.ZoneBackdrop != null)
                {
                    Transform view = Instantiate(section.Map.ZoneBackdrop, transform).transform;
                    view.localPosition = Vector3.zero;
                    float farthest = 0f;
                    foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
                    {
                        Bounds bounds = renderer.bounds;
                        float center = transform.InverseTransformPoint(bounds.center).z;
                        Vector3 extent = bounds.extents;
                        float depth = Mathf.Abs(transform.InverseTransformVector(Vector3.right * extent.x).z)
                            + Mathf.Abs(transform.InverseTransformVector(Vector3.up * extent.y).z)
                            + Mathf.Abs(transform.InverseTransformVector(Vector3.forward * extent.z).z);
                        farthest = Mathf.Max(farthest, center + depth);
                    }
                    backdrops.Add(new BackdropView { View = view, Start = start,
                        ExitInterval = route.BackdropExitInterval(sectionIndex, start),
                        PassDistance = farthest + 24f });
                }
                start += section.ModuleCount * RoadStandard.ModuleLength;
            }
            foreach (var entry in capacity)
            {
                var pool = new Queue<RoadModule>(entry.Value);
                for (int i = 0; i < entry.Value; i++)
                {
                    RoadModule view = Instantiate(entry.Key, transform);
                    RegisterBendRenderers(view.transform);
                    if (bendShader != null && view.RoadSurface.TryGetComponent<MeshFilter>(out var surface) &&
                        surface.sharedMesh != null && surface.sharedMesh.isReadable && surface.sharedMesh.name == "Cube")
                    {
                        bendRoadSources.Add(surface, surface.sharedMesh);
                    }
                    view.gameObject.SetActive(false); pool.Enqueue(view);
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
            int first = Mathf.FloorToInt(distance / RoadStandard.ModuleLength) - Behind;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = first + i;
                Slot slot = slots[((index % slots.Length) + slots.Length) % slots.Length];
                if (slot.Index != index)
                {
                    Release(slot); slot.Source = route.ModuleAt(index);
                    // Pools are warmed for the maximum simultaneous use of each variant.
                    slot.View = pools[slot.Source].Dequeue(); slot.Index = index;
                    Material roadMaterial = route.MapAtModule(index).RoadMaterial;
                    slot.View.SetRoadMaterial(roadMaterial);
                    Renderer surface = slot.View.RoadSurface;
                    if (roadMaterial != null && bendSources.TryGetValue(surface, out var materials))
                    {
                        materials[0] = roadMaterial;
                        ApplyBendMaterials(surface, materials);
                    }
                    slot.View.gameObject.SetActive(true);
                }
                slot.View.transform.localPosition = new Vector3(0, 0, index * RoadStandard.ModuleLength - distance);
            }
            foreach (var landmark in landmarks)
            {
                float z = landmark.Start + landmark.Offset - distance;
                landmark.View.localPosition = new Vector3(landmark.Lateral, 0, landmark.Pass ? z : Mathf.Max(landmark.Offset, z));
            }
            foreach (var backdrop in backdrops)
            {
                const float blendDistance = 48f;
                float approach = Mathf.Clamp01((distance - backdrop.Start + blendDistance) / blendDistance);
                float travelled = Mathf.Max(0f, distance - backdrop.Start);
                float parallax = Mathf.Min(20f, travelled * .04f);
                float departure = 0f;
                if (distance > backdrop.ExitInterval.x)
                    departure = Mathf.InverseLerp(backdrop.ExitInterval.x, backdrop.ExitInterval.y, distance);
                float entryOffset = Mathf.SmoothStep(worldCamera.farClipPlane, 0f, approach);
                float passOffset = backdrop.PassDistance * departure;
                backdrop.View.localPosition = Vector3.forward * (entryOffset - parallax - passOffset);
                bool visible = approach > 0f && departure < 1f;
                if (backdrop.View.gameObject.activeSelf != visible) backdrop.View.gameObject.SetActive(visible);
            }
            ApplyEnvironment(distance);
        }

        public bool ConfigureDepthBend(bool enabled, float start, float strength)
        {
            if (bendShader == null) return false;
            Shader.SetGlobalVector(DepthBend, new Vector4(start, enabled ? strength : 0f, transform.position.z, 0f));
            if (bendEnabled == enabled) return enabled;
            bendEnabled = enabled;
            foreach (var source in bendRoadSources)
            {
                if (enabled && bendRoadMesh == null) bendRoadMesh = CreateBendRoadMesh(source.Value);
                source.Key.sharedMesh = enabled ? bendRoadMesh : source.Value;
            }
            foreach (var source in bendSources) ApplyBendMaterials(source.Key, source.Value);
            return enabled;
        }

        private void RegisterBendRenderers(Transform root)
        {
            if (bendShader == null) return;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                if (Array.Exists(materials, material => material != null && material.shader.name == "Standard"))
                    bendSources.Add(renderer, materials);
            }
        }

        private void ApplyBendMaterials(Renderer renderer, Material[] sources)
        {
            if (!bendEnabled)
            {
                renderer.sharedMaterials = sources;
                renderer.ResetLocalBounds();
                return;
            }
            var materials = (Material[])sources.Clone();
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || source.shader.name != "Standard") continue;
                if (!bendMaterials.TryGetValue(source, out var bent))
                {
                    bent = new Material(source) { shader = bendShader, hideFlags = HideFlags.HideAndDontSave };
                    bendMaterials.Add(source, bent);
                }
                materials[i] = bent;
            }
            renderer.sharedMaterials = materials;
            // Use a conservative world displacement converted to the renderer's local coordinates.
            renderer.ResetLocalBounds();
            Bounds bounds = renderer.localBounds;
            Vector3 reach = renderer.transform.InverseTransformVector(Vector3.right * 150f);
            bounds.Expand(2f * new Vector3(Mathf.Abs(reach.x), Mathf.Abs(reach.y), Mathf.Abs(reach.z)));
            renderer.localBounds = bounds;
        }

        private static Mesh CreateBendRoadMesh(Mesh source)
        {
            var mesh = new Mesh { name = "Subdivided preview road" };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            Vector3[] sourceVertices = source.vertices, sourceNormals = source.normals;
            Vector2[] sourceUv = source.uv;
            int[] triangles = source.triangles;
            const int segments = 8;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                int first = vertices.Count;
                for (int row = 0; row <= segments; row++)
                for (int column = 0; column <= segments - row; column++)
                {
                    float v = row / (float)segments, u = column / (float)segments, w = 1f - u - v;
                    vertices.Add(sourceVertices[a] * w + sourceVertices[b] * u + sourceVertices[c] * v);
                    normals.Add(sourceNormals[a] * w + sourceNormals[b] * u + sourceNormals[c] * v);
                    uv.Add(sourceUv[a] * w + sourceUv[b] * u + sourceUv[c] * v);
                }
                int offset = first;
                for (int row = 0; row < segments; row++)
                {
                    int width = segments + 1 - row;
                    for (int column = 0; column < width - 1; column++)
                    {
                        int left = offset + column, below = left + width;
                        indices.Add(left); indices.Add(left + 1); indices.Add(below);
                        if (column == width - 2) continue;
                        indices.Add(left + 1); indices.Add(below + 1); indices.Add(below);
                    }
                    offset += width;
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (Material material in bendMaterials.Values) Destroy(material);
            if (bendRoadMesh != null) Destroy(bendRoadMesh);
        }
        private void ApplyEnvironment(float distance)
        {
            int index = route.SectionAt(distance, out float start);
            RouteSection section = route.Sections[index];
            MapDefinition from = section.Map, to = from;
            float blend = 0;
            if (section.Transition && index + 1 < route.Sections.Length)
            {
                to = route.Sections[index + 1].Map;
                blend = Mathf.Clamp01((distance - start) / (section.ModuleCount * RoadStandard.ModuleLength));
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
