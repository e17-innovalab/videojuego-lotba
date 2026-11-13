using UnityEngine;

namespace Lotba.Game
{
    [CreateAssetMenu(menuName = "LOTBA/Maps/Map definition")]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [Tooltip("Ordered standardized module prefabs. Replace visuals inside their Art child.")]
        [SerializeField] private RoadModule[] modules;
        [SerializeField] private GameObject landmark;
        [SerializeField] private GameObject zoneBackdrop;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private ObstacleSet obstacles;
        public ObstacleSet Obstacles => obstacles;
        [SerializeField, Min(80)] private float landmarkDistance = 115;
        [SerializeField] private Color sky = new Color(.35f, .72f, .91f);
        [SerializeField] private Color fog = new Color(.57f, .77f, .84f);
        [SerializeField] private float fogStart = 65, fogEnd = 170;
        public Material RoadMaterial => roadMaterial;
        public string DisplayName => displayName;
        public RoadModule[] Modules => modules;
        public string ValidationError
        {
            get
            {
                if (modules == null || modules.Length == 0) return "Assign at least one module to the zone.";
                foreach (var module in modules)
                {
                    if (module == null) return "The zone has an empty module slot.";
                    if (module.ValidationError != null) return $"{module.name}: {module.ValidationError}";
                }
                return null;
            }
        }
#if UNITY_EDITOR
        public static MapDefinition CreatePreview(MapDefinition environment, RoadModule module)
        {
            var map = Instantiate(environment);
            map.hideFlags = HideFlags.DontSave;
            map.modules = new[] { module };
            return map;
        }
#endif
        public GameObject Landmark => landmark;
        public GameObject ZoneBackdrop => zoneBackdrop;
        public float LandmarkDistance => landmarkDistance;
        public Color Sky => sky;
        public Color Fog => fog;
        public float FogStart => fogStart;
        public float FogEnd => fogEnd;
        public RoadModule ModuleAt(int index)
        {
            return modules[((index % modules.Length) + modules.Length) % modules.Length];
        }
    }
}
