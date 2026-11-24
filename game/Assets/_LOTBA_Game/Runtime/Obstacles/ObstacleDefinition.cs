using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lotba.Game
{
    [Serializable]
    public struct BusLivery
    {
        [SerializeField] private string lineNumber;
        [SerializeField] private Color bodyColour;
        [SerializeField] private Color stripeColour;
        public string LineNumber => lineNumber;
        public Color BodyColour => bodyColour;
        public Color StripeColour => stripeColour;
        public BusLivery(string lineNumber, Color bodyColour, Color stripeColour)
        {
            this.lineNumber = lineNumber;
            this.bodyColour = bodyColour;
            this.stripeColour = stripeColour;
        }
    }

    /// <summary>Gameplay footprint and replaceable visual prefab. The origin is at ground level, centred in X/Z.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Obstacles/Obstacle definition")]
    public sealed class ObstacleDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private ObstacleView prefab;
        [SerializeField, Min(.1f)] private float width = 1.8f;
        [SerializeField, Min(.1f)] private float height = 1f;
        [SerializeField, Min(.1f)] private float length = 1f;
        [FormerlySerializedAs("jumpable")]
        [SerializeField] private bool tumbleOnImpact;
        [SerializeField] private bool vehicle;
        [FormerlySerializedAs("jumpClearance")]
        [SerializeField, Min(0), Tooltip("Altura efectiva para choques. Cero usa la altura completa.")]
        private float collisionHeight = 1.05f;
        [SerializeField] private bool rideableRoof;
        [SerializeField, Min(2)] private int poolCapacity = 12;
        [SerializeField] private BusLivery livery;
        public string Id => id;
        public ObstacleView Prefab => prefab;
        public float Width => width;
        public float Height => height;
        public float Length => length;
        public bool TumblesOnImpact => tumbleOnImpact && !vehicle;
        public float ContactPadding => rideableRoof ? 0f : vehicle ? .2f : .6f;
        public float CollisionHeight => rideableRoof ? height : collisionHeight > 0f ? collisionHeight : height;
        public bool RideableRoof => rideableRoof;
        public int PoolCapacity => poolCapacity;
        public BusLivery Livery => livery;
    }
}
