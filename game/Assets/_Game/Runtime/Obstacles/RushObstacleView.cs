using UnityEngine;

namespace Lotba.CityRush
{
    /// <summary>Keep this wrapper and swap the children of Art. Collision dimensions live in the definition.</summary>
    public sealed class RushObstacleView : MonoBehaviour
    {
        [SerializeField] private Transform artRoot;
        public Transform ArtRoot => artRoot;
        public void Apply(RushObstacleDefinition definition)
        {
            var skin = artRoot.GetComponentInChildren<RushBusSkin>(true);
            if (skin != null) skin.Apply(definition.Livery);
        }
    }
}
