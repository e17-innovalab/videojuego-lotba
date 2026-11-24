using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class RunnerWorld
    {
        public void PrepareRecoveryChallenge(RunSession session)
        {
            if (session == null || !session.RecoveryActive) return;
            ClearContent(new TrackSpan(session.Distance, session.RecoveryZoneEnd + 12f));
            float firstAerialDelay = recoveryZoneRules != null && recoveryZoneRules.EncounterProfile != null
                ? recoveryZoneRules.EncounterProfile.RecoveryAerialStartDelay
                : 72f;
            trackScheduler.BeginRecoveryChallenge(session.Distance, session.RecoveryChallengeEnd,
                session.RecoveryZoneEnd, firstAerialDelay);
        }

        private Transform impactVisualRoot;
        private RecoveryCurtain recoveryCurtain;
        private bool missingCurtainReported;
        private RunSession curtainSession;
        private RunSession configuredCurtainSession;
        private int configuredCurtainAttempt;

        [Header("Recovery · Gem")]
        private RecoveryGemView recoveryGemInstance;
        private RecoveryGemView recoveryGemPreviewInstance;
        private int activeRecoveryGemAttempt = -1;
        private float recoveryGemLaneX;
        private float recoveryGemTargetHeight = 1.2f;
        private bool recoveryGemResolved;
        private bool recoveryGemPreviewApproachingDestination;
        private System.Random recoveryGemRandom = new System.Random(2710);

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
            bool active = session.State == RunState.Running && session.RecoveryActive && settings.EnableRecoveryFog;
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
            if (active && recoveryCurtain != null && (configuredCurtainSession != session || configuredCurtainAttempt != session.RecoveryAttempts))
            {
                float fogZ = session.RecoveryFogZ > 0f ? session.RecoveryFogZ : SelectRecoveryFogZ(session);
                recoveryCurtain.SetDistanceZ(fogZ);
                configuredCurtainSession = session;
                configuredCurtainAttempt = session.RecoveryAttempts;
            }
            if (recoveryCurtain != null)
                recoveryCurtain.Render(active,
                    session.State != RunState.Running || session.OfferPending);
        }

        private void AdvanceRecoveryGem(float travel, TrackContact step, RunSession session, float dt)
        {
            if (session == null) return;

            // Initialize or place recovery gem on new recovery attempt
            if (session.RecoveryActive && session.State == RunState.Running)
            {
                if (activeRecoveryGemAttempt != session.RecoveryAttempts)
                {
                    activeRecoveryGemAttempt = session.RecoveryAttempts;
                    recoveryGemResolved = false;
                    trackScheduler.NotifyRecoveryEntered(session.Distance, session.RecoveryZoneEnd,
                        settings.Transitions.recoveryExitBreathMetres);

                    if (settings.RecoveryGemPrefab != null)
                    {
                        if (recoveryGemInstance == null)
                        {
                            recoveryGemInstance = Instantiate(settings.RecoveryGemPrefab, transform, false);
                        }

                        // Pick a lane for the golden gem
                        int lane = recoveryGemRandom.Next(3);
                        recoveryGemLaneX = (lane - 1) * LaneSpacing;

                        // Position near the end of the recovery track (15 m before the end)
                        float remainingDistance = Mathf.Max(25f, (session.RecoveryChallengeEnd - session.Distance) - 15f);
                        recoveryGemTargetHeight = 1.2f;
                        float targetWorldDistance = session.Distance + remainingDistance;
                        if (trackScheduler.TryGetSurfaceAt(targetWorldDistance, out TraversableSurface surface, 0f) &&
                            !surface.IsRamp && surface.HighHeight > .5f)
                        {
                            recoveryGemLaneX = (surface.Lane - 1) * LaneSpacing;
                            recoveryGemTargetHeight = surface.HeightAt(targetWorldDistance) + 1.2f;
                        }
                        recoveryGemInstance.transform.localPosition = new Vector3(recoveryGemLaneX, recoveryGemTargetHeight, remainingDistance);
                        recoveryGemInstance.ResetGem();
                        bool targetVisible = remainingDistance <= settings.RecoveryGemPreviewDistance;
                        recoveryGemInstance.SetVisualVisible(targetVisible);
                        if (!targetVisible)
                        {
                            if (recoveryGemPreviewInstance == null)
                                recoveryGemPreviewInstance = Instantiate(settings.RecoveryGemPrefab, transform, false);
                            recoveryGemPreviewInstance.ResetGem();
                            recoveryGemPreviewInstance.ConfigurePursuit(rider, recoveryGemInstance.transform,
                                settings.RecoveryGemPreviewDistance, settings.RecoveryGemChaseHeight);
                            recoveryGemPreviewInstance.PlayIntro();
                            recoveryGemPreviewApproachingDestination = false;
                        }
                        else if (recoveryGemPreviewInstance != null)
                        {
                            HideRecoveryGemPreview();
                        }
                    }
                }
            }
            else if (!session.RecoveryActive)
            {
                if (recoveryGemInstance != null) recoveryGemInstance.gameObject.SetActive(false);
                HideRecoveryGemPreview();
            }

            if (recoveryGemInstance == null || !recoveryGemInstance.gameObject.activeSelf || recoveryGemResolved)
                return;

            Vector3 currentPos = recoveryGemInstance.transform.localPosition;
            float nextZ = currentPos.z - travel;

            if (recoveryGemPreviewInstance != null && recoveryGemPreviewInstance.gameObject.activeSelf)
            {
                if (nextZ <= settings.RecoveryGemPreviewDistance)
                {
                    recoveryGemInstance.transform.localPosition = new Vector3(recoveryGemLaneX, recoveryGemTargetHeight, nextZ);
                    if (!recoveryGemPreviewApproachingDestination)
                    {
                        recoveryGemPreviewInstance.BeginFinalApproach();
                        recoveryGemPreviewApproachingDestination = true;
                    }
                    recoveryGemPreviewInstance.Step(dt, session.Elapsed);
                    if (recoveryGemPreviewInstance.IsAtDestination)
                    {
                        HideRecoveryGemPreview();
                        recoveryGemInstance.SetVisualVisible(true);
                    }
                }
                else
                {
                    recoveryGemPreviewInstance.Step(dt, session.Elapsed);
                }
            }

            if (nextZ >= 0f)
            {
                recoveryGemInstance.transform.localPosition = new Vector3(recoveryGemLaneX, recoveryGemTargetHeight, nextZ);
                if (recoveryGemInstance.gameObject.activeSelf)
                    recoveryGemInstance.Step(dt, session.Elapsed);
                return;
            }

            // Gem reached or crossed rider position (nextZ < 0f)
            recoveryGemResolved = true;
            float fraction = travel > 0f ? Mathf.Clamp01(currentPos.z / travel) : 0f;
            float riderX = step.XAt(fraction);
            float riderHeight = step.HeightAt(fraction);

            bool inLane = Mathf.Abs(recoveryGemLaneX - riderX) < 0.85f;
            bool canCollect = JumpArc.CanCollect(riderHeight, recoveryGemTargetHeight);

            if (inLane && canCollect)
            {
                session.CollectRecoveryGem();
                recoveryGemInstance.OnCollected();
                HideRecoveryGemPreview();
                EmitPickupFlash(transform.TransformPoint(new Vector3(recoveryGemLaneX, recoveryGemTargetHeight, 0f)), settings.GemFlash);
                GemFeedback?.Invoke(0, session.CanActivateTurbo);
            }
            else
            {
                session.MissRecoveryGem();
                recoveryGemInstance.OnMissed();
                HideRecoveryGemPreview();
            }
        }

        private void ResetRecoveryPresentation()
        {
            activeRecoveryGemAttempt = -1;
            recoveryGemResolved = false;
            recoveryGemPreviewApproachingDestination = false;
            if (recoveryGemInstance != null)
            {
                recoveryGemInstance.gameObject.SetActive(false);
            }
            HideRecoveryGemPreview();
            trackScheduler?.NotifyRecoveryExited();
        }

        private void HideRecoveryGemPreview()
        {
            if (recoveryGemPreviewInstance == null) return;
            recoveryGemPreviewInstance.SetVisualVisible(false);
            recoveryGemPreviewInstance.gameObject.SetActive(false);
            recoveryGemPreviewApproachingDestination = false;
        }

    }
}
