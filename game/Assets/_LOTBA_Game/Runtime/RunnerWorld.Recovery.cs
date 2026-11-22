using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class RunnerWorld
    {
        private Transform impactVisualRoot;
        private RecoveryCurtain recoveryCurtain;
        private bool missingCurtainReported;
        private RunSession curtainSession;
        private RunSession configuredCurtainSession;
        public bool RecoveryImpactComplete => impactPresentation.IsComplete(Time.unscaledTime);

        public void RenderRecoveryCurtain(RunSession session)
        {
            curtainSession = session;
        }

        public float SelectRecoveryFogZ(RunSession session)
        {
            var prefab = settings.RecoveryCurtainPrefab;
            if (prefab == null) return 0f;
            var difficulty = settings.RecoveryFogDifficulty;
            return difficulty != null
                ? difficulty.Evaluate(prefab.AuthoredDistanceZ, session.RecoveryAmount, session.Distance)
                : prefab.AuthoredDistanceZ;
        }

        private void UpdateRecoveryCurtainPresentation()
        {
            var session = curtainSession;
            if (session == null) return;
            bool active = session.State == RunState.Running && session.RecoveryActive;
            if (active && recoveryCurtain == null)
            {
                if (settings.RecoveryCurtainPrefab == null)
                {
                    if (!missingCurtainReported) Debug.LogError("Assign the recovery curtain prefab in GameSettings.", this);
                    missingCurtainReported = true;
                    return;
                }
                // Follow the rider through parenting, preserving all authored local transforms.
                recoveryCurtain = Instantiate(settings.RecoveryCurtainPrefab, rider, false);
            }
            if (active && recoveryCurtain != null && configuredCurtainSession != session)
            {
                recoveryCurtain.SetDistanceZ(session.RecoveryFogZ);
                configuredCurtainSession = session;
            }
            if (recoveryCurtain != null)
                recoveryCurtain.Render(active,
                    session.State != RunState.Running || session.OfferPending);
        }

        /// <summary>Common obstacle-free rejoin for either answer; does not award pickups or clears.</summary>
        public int PrepareRecoveryResume(RunSession session)
        {
            float expectedSpeed = Mathf.Max(session.Speed,
                settings.CruiseSpeed + session.PurchasedSpeed + roadStream.Route.SpeedBonusAt(session.Distance)) +
                session.RecoverySpeed + RunSession.RiskSpeedForFullIntensity;
            float clearDistance = expectedSpeed * (RunSession.RecoveryResumeSeconds + .75f);
            int cleared = 0;
            foreach (TrackItem item in items)
            {
                if (!item.Active || item.Definition == null) continue;
                float halfLength = item.Definition.Length * .5f + item.Definition.ContactPadding;
                float front = item.View.localPosition.z - halfLength;
                float rear = item.View.localPosition.z + halfLength;
                if (rear <= 0f || front >= clearDistance) continue;
                // Keep roofs already beneath the rider instead of deleting their support.
                if (item.Definition.RideableRoof && front <= 0f) continue;
                Hide(item);
                cleared++;
            }
            // Prevent the generator from immediately filling the reserved exit again.
            float exit = session.Distance + clearDistance;
            nextVehicleSafeDistance = Mathf.Max(nextVehicleSafeDistance, exit);
            nextProfileObstacle = Mathf.Max(nextProfileObstacle, exit);
            ClearImpactBlink();
            return cleared;
        }

    }
}
