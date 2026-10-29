using UnityEngine;

namespace Lotba.CityRush
{
    [CreateAssetMenu(menuName = "LOTBA/Maps/Map definition")]
    public sealed class RushMapDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [Tooltip("Ordered standardized module prefabs. Replace visuals inside their Art child.")]
        [SerializeField] private RushRoadModule[] modules;
        [SerializeField] private GameObject landmark;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private RushObstacleSet obstacles;
        public RushObstacleSet Obstacles => obstacles;
        [SerializeField, Min(80)] private float landmarkDistance = 115;
        [SerializeField] private Color sky = new Color(.35f, .72f, .91f);
        [SerializeField] private Color fog = new Color(.57f, .77f, .84f);
        [SerializeField] private float fogStart = 65, fogEnd = 170;
        public Material RoadMaterial => roadMaterial;
        public string DisplayName => displayName;
        public RushRoadModule[] Modules => modules;
        public GameObject Landmark => landmark;
        public float LandmarkDistance => landmarkDistance;
        public Color Sky => sky;
        public Color Fog => fog;
        public float FogStart => fogStart;
        public float FogEnd => fogEnd;
        public RushRoadModule ModuleAt(int index)
        {
            return modules[((index % modules.Length) + modules.Length) % modules.Length];
        }
    }
}
