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

    }
}
