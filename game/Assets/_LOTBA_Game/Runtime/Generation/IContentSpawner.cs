using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Comando estructurado para instanciar/activar un elemento en la pista.
    /// </summary>
    public readonly struct TrackSpawnItem
    {
        public readonly RunnerWorld.ItemKind Kind;
        public readonly int Lane;
        public readonly float TrackDistance;
        public readonly float Height;
        public readonly ObstacleDefinition Definition;
        public readonly int AirborneNote;
        public readonly bool Pink;
        public readonly int PinkDodgeEncounter;

        public TrackSpawnItem(
            RunnerWorld.ItemKind kind,
            int lane,
            float trackDistance,
            float height,
            ObstacleDefinition definition = null,
            int airborneNote = -1,
            bool pink = false,
            int pinkDodgeEncounter = 0)
        {
            Kind = kind;
            Lane = Mathf.Clamp(lane, 0, 2);
            TrackDistance = trackDistance;
            Height = height;
            Definition = definition;
            AirborneNote = airborneNote;
            Pink = pink;
            PinkDodgeEncounter = pinkDodgeEncounter;
        }

        public static TrackSpawnItem CreateGem(int lane, float trackDistance, float height, int airborneNote = -1, bool pink = false, int pinkDodgeEncounter = 0)
        {
            return new TrackSpawnItem(RunnerWorld.ItemKind.Gem, lane, trackDistance, height, null, airborneNote, pink, pinkDodgeEncounter);
        }

        public static TrackSpawnItem CreateObstacle(int lane, float trackDistance, ObstacleDefinition definition)
        {
            return new TrackSpawnItem(RunnerWorld.ItemKind.Obstacle, lane, trackDistance, 0f, definition);
        }
    }

    public interface IContentSpawner
    {
        string Name { get; }
        float NextRequestedDistance { get; }
        void Reset(int baseSeed);
        void Populate(in GenerationContext context, TrackScheduler scheduler);
    }
}
