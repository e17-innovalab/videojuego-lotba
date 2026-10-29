using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Keep this wrapper and swap the children of Art. Collision dimensions live in the definition.</summary>
    public sealed class ObstacleView : MonoBehaviour
    {
        [SerializeField] private Transform artRoot;
        public Transform ArtRoot => artRoot;
        public void Apply(ObstacleDefinition definition)
        {
            var skin = artRoot.GetComponentInChildren<BusSkin>(true);
            if (skin != null) skin.Apply(definition.Livery);
        }
    }
}
