using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Per-map selection data; replacing visuals or changing available traffic needs no generator changes.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Obstacles/Obstacle set")]
    public sealed class ObstacleSet : ScriptableObject
    {
        [SerializeField] private ObstacleDefinition[] low;
        [SerializeField] private ObstacleDefinition[] traffic;
        public ObstacleDefinition[] Low => low;
        public ObstacleDefinition[] Traffic => traffic;
        public ObstacleDefinition Pick(System.Random random, bool vehicle, float maximumLength, bool preferLong = false)
        {
            var candidates = vehicle ? traffic : low;
            if (candidates == null || candidates.Length == 0) return null;
            bool longOnly = false;
            if (vehicle && preferLong)
                foreach (var candidate in candidates)
                    if (candidate != null && candidate.Length > 4.5f && candidate.Length <= maximumLength) { longOnly = true; break; }
            int eligible = 0;
            foreach (var candidate in candidates)
                if (candidate != null && candidate.Length <= maximumLength && (!longOnly || candidate.Length > 4.5f)) eligible++;
            if (eligible == 0) return null;
            int selected = random.Next(eligible);
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.Length > maximumLength || (longOnly && candidate.Length <= 4.5f)) continue;
                if (selected-- == 0) return candidate;
            }
            return null;
        }
        public ObstacleDefinition Find(string id)
        {
            if (low != null) foreach (var item in low) if (item != null && item.Id == id) return item;
            if (traffic != null) foreach (var item in traffic) if (item != null && item.Id == id) return item;
            return null;
        }
    }
}
