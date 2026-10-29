using System;
using UnityEngine;

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
        [SerializeField] private bool jumpable;
        [SerializeField, Min(0)] private float jumpClearance = 1.05f;
        [SerializeField, Min(2)] private int poolCapacity = 12;
        [SerializeField] private BusLivery livery;
        public string Id => id;
        public ObstacleView Prefab => prefab;
        public float Width => width;
        public float Height => height;
        public float Length => length;
        public bool Jumpable => jumpable;
        public float JumpClearance => jumpClearance;
        public int PoolCapacity => poolCapacity;
        public BusLivery Livery => livery;
    }
}
