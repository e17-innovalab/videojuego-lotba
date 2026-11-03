using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Common seam and gameplay dimensions. Visual assets must respect this envelope.</summary>
    public static class RoadStandard
    {
        public const float ModuleLength = 12f;
        public const float RoadWidth = 9f;
        public const float LaneSpacing = 2.7f;
        public const float ClearHeight = 4.2f;
    }

    /// <summary>Stable prefab wrapper: replace children of Art without changing the road or connectors.</summary>
    public sealed class RoadModule : MonoBehaviour
    {
        [SerializeField] private Transform artRoot;
        [SerializeField] private Renderer roadSurface;
        [SerializeField] private Transform entry;
        [SerializeField] private Transform exit;
        public void SetRoadMaterial(Material material) { if (material != null) roadSurface.sharedMaterial = material; }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 1, .5f); Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, RoadStandard.ClearHeight * .5f, 6), new Vector3(RoadStandard.RoadWidth, RoadStandard.ClearHeight, RoadStandard.ModuleLength));
        }
        public Transform ArtRoot => artRoot;
        public Transform Entry => entry;
        public Transform Exit => exit;
        public string ValidationError
        {
            get
            {
                if (artRoot == null || roadSurface == null || entry == null || exit == null)
                    return "Assign Art Root, Road Surface, Entry and Exit.";
                if (Vector3.Distance(transform.localScale, Vector3.one) > .001f) return "Keep module root scale at one.";
                if (Vector3.Distance(entry.localPosition, Vector3.zero) > .001f ||
                    Vector3.Distance(exit.localPosition, Vector3.forward * RoadStandard.ModuleLength) > .001f)
                    return "Entry must be at (0,0,0); Exit at (0,0,12).";
                return null;
            }
        }
    }
}
