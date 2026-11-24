using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class RunnerWorld
    {
        private SectionTransitionPlan[] sectionTransitions;
        private readonly TrackReservations transitionPatternReservations = new TrackReservations();
        public TrackReservations TransitionReservations => transitionPatternReservations;
        private readonly List<Transform> sectionMarkers = new List<Transform>();
        private readonly List<ChoiceMarkerView> sectionInvitations = new List<ChoiceMarkerView>();
        private int populatedTransitionExits;
        private LanePatternParameters transitionExitPattern;
        private SectionTransitionSettings sectionTransitionRules;

        // --- Recovery fork visual objects --------------------------------
        private Transform activeForkBarrier;
        private Transform activeForkPortalContinue;
        private Transform activeForkPortalRecover;
        private float lastForkDistance = -1f;

        private void PrepareSectionTransitions()
        {
            foreach (var marker in sectionMarkers) if (marker != null) Destroy(marker.gameObject);
            sectionMarkers.Clear(); transitionPatternReservations.Clear(); populatedTransitionExits = 0;
            sectionInvitations.Clear();
            ClearForkVisuals();
            lastForkDistance = -1f;
            sectionTransitionRules = settings.Transitions.Capture();
            transitionExitPattern = settings.TransitionExitPattern != null ? settings.TransitionExitPattern.Capture() : null;
            sectionTransitions = SectionTransitionPlan.Build(roadStream.Route, sectionTransitionRules);
#if UNITY_EDITOR
            if (roadStream.Route.Sections.Length == 1) return;
#endif
            foreach (var plan in sectionTransitions)
            {
                trackReservations.Reserve(plan.Corridor);
                transitionPatternReservations.Reserve(plan.Corridor);
                if (settings.SectionGatePrefab != null)
                {
                    var marker = Instantiate(settings.SectionGatePrefab, transform, false);
                    marker.localPosition += Vector3.forward * plan.Stop;
                    sectionMarkers.Add(marker);
                    sectionInvitations.Add(marker.GetComponent<ChoiceMarkerView>());
                }
            }
        }

        public void RenderSectionMarkers(float distance, bool visible, bool offerRecovery)
        {
            for (int i = 0; i < sectionMarkers.Count; i++)
            {
                var marker = sectionMarkers[i];
                var position = marker.localPosition;
                position.z = settings.SectionGatePrefab.localPosition.z + sectionTransitions[i].Stop - distance;
                marker.localPosition = position;
                marker.gameObject.SetActive(visible && position.z >= -12f && position.z < roadStream.VisibilityDistance);
                sectionInvitations[i]?.ShowInvitation(offerRecovery && Mathf.Abs(sectionTransitions[i].Stop - distance) < .1f);
            }
        }

        /// <summary>Place or update the three fork visuals (barrier + two portals).
        /// Called each frame from Advance so they scroll with the world.</summary>
        public void RenderRecoveryFork(RunSession session)
        {
            if (!session.RecoveryForkActive)
            {
                ClearForkVisuals();
                return;
            }
            float forkDistance = session.RecoveryForkDistance;
            // Create visuals once per fork.
            if (!Mathf.Approximately(lastForkDistance, forkDistance))
            {
                ClearForkVisuals();
                lastForkDistance = forkDistance;
                ClearAndReserve(new TrackSpan(forkDistance - 6f, forkDistance + 24f));
                float laneSpacing = RoadStandard.LaneSpacing;
                int recoveryLane = session.RecoveryForkLane;   // 0 or 2
                int continueLane = session.RecoveryForkLane == 0 ? 2 : 0;
                float recoveryX = (recoveryLane - 1) * laneSpacing;
                float continueX = (continueLane - 1) * laneSpacing;
                // Barrier: center lane, no collider, purely visual
                if (settings.RecoveryBarrierPrefab != null)
                {
                    activeForkBarrier = Instantiate(settings.RecoveryBarrierPrefab, transform, false);
                    activeForkBarrier.localPosition = new Vector3(0f, 0f, forkDistance - riderDistance);
                }
                // Portal RECUPERAR
                if (settings.RecoveryPortalRecoverPrefab != null)
                {
                    activeForkPortalRecover = Instantiate(settings.RecoveryPortalRecoverPrefab, transform, false);
                    activeForkPortalRecover.localPosition = new Vector3(recoveryX, 0f, forkDistance - riderDistance);
                    var portalView = activeForkPortalRecover.GetComponent<RecoveryPortalView>();
                    if (portalView != null)
                    {
                        string labelText = session.RecoveryPointAmount > 0 && session.RecoveryAmount > 0
                            ? $"RECUPERAR\n{session.RecoveryPointAmount} pts · {session.RecoveryAmount} gemas"
                            : session.RecoveryPointAmount > 0
                                ? $"RECUPERAR\n{session.RecoveryPointAmount} pts"
                                : $"RECUPERAR\n{session.RecoveryAmount} gemas";
                        portalView.SetLabel(labelText);
                    }
                }
                // Portal SEGUIR
                if (settings.RecoveryPortalContinuePrefab != null)
                {
                    activeForkPortalContinue = Instantiate(settings.RecoveryPortalContinuePrefab, transform, false);
                    activeForkPortalContinue.localPosition = new Vector3(continueX, 0f, forkDistance - riderDistance);
                    var portalView = activeForkPortalContinue.GetComponent<RecoveryPortalView>();
                    if (portalView != null) portalView.SetLabel("SEGUIR");
                }
            }
            // Scroll with the world each frame.
            float z = forkDistance - riderDistance;
            if (activeForkBarrier != null) { var p = activeForkBarrier.localPosition; p.z = z; activeForkBarrier.localPosition = p; }
            if (activeForkPortalRecover != null) { var p = activeForkPortalRecover.localPosition; p.z = z; activeForkPortalRecover.localPosition = p; }
            if (activeForkPortalContinue != null) { var p = activeForkPortalContinue.localPosition; p.z = z; activeForkPortalContinue.localPosition = p; }
            // Hide once the rider passed the fork.
            bool behind = z < -4f;
            if (activeForkBarrier != null) activeForkBarrier.gameObject.SetActive(!behind);
            if (activeForkPortalRecover != null) activeForkPortalRecover.gameObject.SetActive(!behind);
            if (activeForkPortalContinue != null) activeForkPortalContinue.gameObject.SetActive(!behind);
        }

        private void ClearForkVisuals()
        {
            if (activeForkBarrier != null) { Destroy(activeForkBarrier.gameObject); activeForkBarrier = null; }
            if (activeForkPortalRecover != null) { Destroy(activeForkPortalRecover.gameObject); activeForkPortalRecover = null; }
            if (activeForkPortalContinue != null) { Destroy(activeForkPortalContinue.gameObject); activeForkPortalContinue = null; }
        }

        /// <summary>Prepares legible existing obstacle prefabs ahead, for either exit choice.</summary>
        private void PopulateTransitionExits(float horizon, float speed)
        {
            while (populatedTransitionExits < sectionTransitions.Length)
            {
                var plan = sectionTransitions[populatedTransitionExits];
                if (plan.Corridor.End > horizon) break;
                float length = sectionTransitionRules.TransitionExitPatternDistance(speed);
                if (transitionExitPattern == null || !transitionExitPattern.CanPopulate(roadStream.Route, plan.Corridor.End, length))
                {
                    populatedTransitionExits++; continue;
                }
                transitionPatternReservations.Reserve(new TrackSpan(plan.Corridor.Start, plan.Corridor.End + length));
                foreach (var row in transitionExitPattern.Rows)
                {
                    float at = plan.Corridor.End + length * row.position;
                    var catalog = obstacleOverride != null ? obstacleOverride : roadStream.Route.MapAtDistance(at).Obstacles;
                    var definition = transitionExitPattern.PickObstacle(catalog, random);
                    if (definition == null) continue;
                    int blocked = row.obstacleLane;
                    Place(ItemKind.Obstacle, blocked, at - riderDistance, 0f, definition, plannedWindowContent: true);
                    for (int gem = 0; gem < row.gems; gem++)
                        Place(ItemKind.Gem, row.gemLane, at - riderDistance - (row.gems - 1) * row.gemSpacing + gem * row.gemSpacing, 1.1f, plannedWindowContent: true);
                }
                populatedTransitionExits++;
            }
        }
    }
}
