using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class RunnerWorld
    {
        private readonly TrackReservations trackReservations = new TrackReservations();
        private readonly System.Collections.Generic.List<SurfaceView> surfaceViews = new System.Collections.Generic.List<SurfaceView>();
        private readonly struct SurfaceView
        {
            public readonly Transform View;
            public readonly float Start, End;
            public SurfaceView(Transform view, float start, float end) { View = view; Start = start; End = end; }
        }

        /// <summary>Remove dynamic content intersecting a route interval and reserve it against later generation.</summary>
        public TrackClearanceResult ClearAndReserve(TrackSpan span)
        {
            trackReservations.Reserve(span);
            int obstacles = 0, gems = 0, letters = 0, platforms = 0, visuals = 0;
            foreach (TrackItem item in items)
            {
                if (!item.Active) continue;
                float center = riderDistance + item.View.localPosition.z;
                float half = item.Definition != null ? item.Definition.Length * .5f + item.Definition.ContactPadding : 0f;
                if (!(half > 0f ? span.Intersects(center - half, center + half) : span.Contains(center))) continue;
                if (item.Kind == ItemKind.Gem) gems++; else obstacles++;
                if (item.PinkDodgeEncounter > 0)
                    foreach (var related in items)
                        if (related.PinkDodgeEncounter == item.PinkDodgeEncounter && related != item)
                            related.PinkDodgeEncounter = 0;
                Hide(item);
            }
            foreach (var encounter in elevatedEncounters)
                for (int i = encounter.Surfaces.Count - 1; i >= 0; i--)
                {
                    var surface = encounter.Surfaces[i];
                    if (!span.Intersects(surface.Start, surface.End)) continue;
                    encounter.Surfaces.RemoveAt(i);
                    platforms++;
                }
            for (int i = surfaceViews.Count - 1; i >= 0; i--)
            {
                var visual = surfaceViews[i];
                if (visual.View == null) { surfaceViews.RemoveAt(i); continue; }
                if (!span.Intersects(visual.Start, visual.End)) continue;
                visual.View.gameObject.SetActive(false);
                surfaceViews.RemoveAt(i);
                visuals++;
            }
            if (activeLetter >= 0 && letterPickup != null && span.Contains(riderDistance + letterPickup.transform.localPosition.z))
            {
                // Requeue without collecting or reporting a miss; the next placement must respect the reservation.
                pendingLetters.Enqueue(activeLetter);
                activeLetter = -1;
                letterPickup.gameObject.SetActive(false);
                if (letterBeam != null) letterBeam.enabled = false;
                UpdateLetterMotes(null, 0f);
                nextLetterDistance = Mathf.Max(nextLetterDistance, span.End);
                letters++;
            }
            crossings.Clear();
            return new TrackClearanceResult(span, obstacles, gems, letters, platforms, visuals);
        }

        public void ClearGemsAhead(float from, float to)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!item.Active || item.Kind != ItemKind.Gem) continue;
                float center = riderDistance + item.View.localPosition.z;
                if (center >= from && center <= to)
                {
                    Hide(item);
                }
            }
        }

        /// <summary>Reset rider placement/presentation without resetting route distance, score or generation.</summary>
        public void RecenterRider(int lane)
        {
            lane = Mathf.Clamp(lane, 0, 2);
            jump.Reset();
            courseJumpBase = courseFallSpeed = 0f;
            visualLane = safeLane = lane;
            laneOrigin = (lane - 1) * LaneSpacing;
            laneTime = settings.LaneChangeTime;
            lanePoseDirection = pedalPhase = wheelSpin = 0f;
            lanePoseAge = LanePoseSeconds;
            sprintPose = sprintFrom = sprintAge = airPose = 0f;
            riderSway = riderBank = riderBrace = riderTakeoff = riderLanding = 0f;
            landingAge = LandingPoseSeconds; takeoffAge = TakeoffPreloadSeconds;
            sprinting = wasAirborne = false;
            rider.SetLocalPositionAndRotation(new Vector3(laneOrigin, 0f, 0f), Quaternion.identity);
            riderVisual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            riderVisual.localScale = Vector3.one;
            steeringPivot.localRotation = Quaternion.identity;
            hips.Reset(); torso.Reset(); head.Reset();
            foreach (RiderLimb limb in legs) limb.Reset();
            foreach (RiderLimb limb in arms) limb.Reset();
            for (int i = 0; i < wheels.Length; i++) wheels[i].localRotation = wheelOrigins[i];
            impactPresentation.ResetImpactReaction(); ClearImpactBlink(); ClearGemScatter();
            brakeEffects?.Clear();
            brakeSlideStarted = float.NegativeInfinity;
            ResetProgressiveBrakePresentation();
            cameraPresentation.Reset();
            worldCamera.transform.SetLocalPositionAndRotation(cameraOrigin, cameraRotation);
            SetFieldOfView(60f);
            UpdateRiderShadow(laneOrigin, 0f, 0f);
        }
    }
}
