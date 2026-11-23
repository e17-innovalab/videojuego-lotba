using System;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Generador coordinado de letras coleccionables ("HABLAR ES GANAR").
    /// </summary>
    public sealed class LetterSpawner : IContentSpawner
    {
        public string Name => "LetterSpawner";
        public float NextRequestedDistance => nextLetterDistance;

        private float nextLetterDistance = 45f;
        private const float LetterInterval = 70f;
        private const float LetterClearance = 12f;
        private System.Random random;

        public void Reset(int baseSeed)
        {
            random = new System.Random(baseSeed ^ 0x9B24);
            nextLetterDistance = 45f;
        }

        public void Populate(in GenerationContext context, TrackScheduler scheduler)
        {
            if (!context.Rules.AllowLetters)
            {
                nextLetterDistance = Mathf.Max(nextLetterDistance, context.SpawnHorizon);
                return;
            }

            while (nextLetterDistance < context.SpawnHorizon)
            {
                if (scheduler.IsZoneRestricted(nextLetterDistance) || scheduler.TryGetSurfaceAt(nextLetterDistance, out _))
                {
                    nextLetterDistance += 20f;
                    continue;
                }

                // Elegir el carril según el meandro determinista del scheduler
                int lane = scheduler.GetCorridorLaneAt(nextLetterDistance);

                // Registrar reserva de despeje de 8 metros para que ningún obstáculo caiga cerca de la letra
                scheduler.RegisterLetterReservation(nextLetterDistance, LetterClearance);

                // Materializar la letra en ese carril exacto
                scheduler.RequestLetterPlacement(nextLetterDistance, lane);

                nextLetterDistance += LetterInterval + (float)(random.NextDouble() * 10f);
            }
        }
    }
}
