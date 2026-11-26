using System;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class LanePatternRow
    {
        [Range(.05f, .95f)] public float position = .22f;
        [Range(0, 2)] public int obstacleLane = 1;
        [Range(0, 2)] public int gemLane = 2;
        [Min(0)] public int gems = 3;
        [Min(.1f)] public float gemSpacing = 3f;
        public LanePatternRow Copy() => (LanePatternRow)MemberwiseClone();
    }

    /// <summary>Reusable authored pattern: fractional distance and lanes, independent of recovery and scene objects.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Track/Lane pattern")]
    public sealed class LanePatternDefinition : ScriptableObject
    {
        [SerializeField, Min(.1f)] private float maximumObstacleLength = 4.5f;
        [SerializeField, Min(.1f)] private float maximumObstacleWidth = 1.9f;
        [SerializeField] private LanePatternRow[] rows = {
            new LanePatternRow(),
            new LanePatternRow { position = .52f, obstacleLane = 2, gemLane = 0 },
            new LanePatternRow { position = .82f, obstacleLane = 0, gemLane = 1 }
        };
        public float MaximumObstacleLength => maximumObstacleLength;
        public float MaximumObstacleWidth => maximumObstacleWidth;
        public LanePatternRow[] CaptureRows() => rows == null ? null : Array.ConvertAll(rows, row => row?.Copy());
        public LanePatternParameters Capture() => new LanePatternParameters(name, maximumObstacleLength, maximumObstacleWidth, CaptureRows());
        public string ValidationError => Capture().ValidationError;
        public bool CanPopulate(RouteDefinition route, float start, float length) => Capture().CanPopulate(route, start, length);
    }

    /// <summary>Captured pattern data. Editing the authoring asset cannot change an active run.</summary>
    public sealed class LanePatternParameters
    {
        public readonly string Name;
        public readonly float MaximumObstacleLength, MaximumObstacleWidth;
        private readonly float maximumObstacleLength, maximumObstacleWidth;
        private readonly LanePatternRow[] rows;
        public LanePatternRow[] Rows => rows;
        public LanePatternParameters(string name, float length, float width, LanePatternRow[] rows)
        {
            Name = name; MaximumObstacleLength = maximumObstacleLength = length;
            MaximumObstacleWidth = maximumObstacleWidth = width; this.rows = rows;
        }
        public string ValidationError
        {
            get
            {
                if (rows == null || rows.Length == 0 || maximumObstacleLength <= 0f || maximumObstacleWidth <= 0f)
                    return "El patrón necesita obstáculos esquivables.";
                float last = 0f;
                foreach (var row in rows)
                {
                    if (row == null || row.position <= last || row.position >= 1f || row.obstacleLane < 0 || row.obstacleLane > 2 ||
                        row.gemLane < 0 || row.gemLane > 2 || row.gemLane == row.obstacleLane || row.gems < 0 || row.gemSpacing <= 0f)
                        return "Revisá orden, posiciones y carriles del patrón.";
                    last = row.position;
                }
                return null;
            }
        }
        public bool CanPopulate(RouteDefinition route, float start, float length)
        {
            if (ValidationError != null || route == null) return false;
            foreach (var row in rows)
            {
                if (row.position * length - row.gems * row.gemSpacing < 0f ||
                    (1f - row.position) * length < maximumObstacleLength) return false;
                var catalog = route.MapAtDistance(start + length * row.position).Obstacles;
                if (PickObstacle(catalog, null) == null) return false;
            }
            return true;
        }
        public ObstacleDefinition PickObstacle(ObstacleSet catalog, System.Random random)
        {
            if (catalog == null) return null;
            return Select(catalog.Low, random) ?? Select(catalog.Traffic, random);
        }
        private ObstacleDefinition Select(ObstacleDefinition[] candidates, System.Random random)
        {
            if (candidates == null) return null;
            ObstacleDefinition result = null;
            int count = 0;
            foreach (var item in candidates)
            {
                if (item == null || item.Prefab == null || item.Length > maximumObstacleLength || item.Width > maximumObstacleWidth) continue;
                if (random == null) return item;
                if (random.Next(++count) == 0) result = item;
            }
            return result;
        }
    }
}
