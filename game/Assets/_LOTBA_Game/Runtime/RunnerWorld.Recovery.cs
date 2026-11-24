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
        private int activeRecoveryGemAttempt = -1;
        private float recoveryGemLaneX;
        private bool recoveryGemResolved;
        private System.Random recoveryGemRandom = new System.Random(2710);

        // Day environment references & captured defaults
        private Light sunLight;
        private float daySunIntensity = 0.9f;
        private Color daySunColor = Color.white;
        private Color dayAmbientColor = new Color(0.65f, 0.70f, 0.75f, 1f);
        private Color daySkyColor = new Color(0.35f, 0.72f, 0.91f, 1f);

        // Night environment targets (violeta noche estilizado y con buena visibilidad)
        private readonly Color nightSkyColor = new Color(0.18f, 0.08f, 0.28f, 1f);
        private readonly Color nightAmbientColor = new Color(0.40f, 0.30f, 0.55f, 1f);
        private readonly Color nightSunColor = new Color(0.70f, 0.55f, 0.95f, 1f);
        private const float nightSunIntensity = 0.45f;

        private bool environmentCaptured;

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

        private void CaptureEnvironment()
        {
            if (environmentCaptured) return;
            if (worldCamera != null)
            {
                daySkyColor = worldCamera.backgroundColor;
            }
            dayAmbientColor = RenderSettings.ambientLight;
            var sunGo = GameObject.Find("Sun");
            if (sunGo != null)
            {
                sunLight = sunGo.GetComponent<Light>();
                if (sunLight != null)
                {
                    daySunIntensity = sunLight.intensity;
                    daySunColor = sunLight.color;
                }
            }
            environmentCaptured = true;
        }

        private void RestoreDayEnvironment()
        {
            if (!environmentCaptured) return;
            if (worldCamera != null) worldCamera.backgroundColor = daySkyColor;
            RenderSettings.ambientLight = dayAmbientColor;
            if (sunLight != null)
            {
                sunLight.intensity = daySunIntensity;
                sunLight.color = daySunColor;
            }
        }

        private void UpdateRecoveryEnvironment(RunSession session)
        {
            CaptureEnvironment();

            bool isNight = session != null && session.State == RunState.Running && session.RecoveryActive;
            float lerpSpeed = Time.unscaledDeltaTime * 3.5f;

            Color targetSky = isNight ? nightSkyColor : daySkyColor;
            Color targetAmbient = isNight ? nightAmbientColor : dayAmbientColor;
            float targetSunIntensity = isNight ? nightSunIntensity : daySunIntensity;
            Color targetSunColor = isNight ? nightSunColor : daySunColor;

            if (worldCamera != null)
            {
                worldCamera.backgroundColor = Color.Lerp(worldCamera.backgroundColor, targetSky, lerpSpeed);
            }

            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, lerpSpeed);

            if (sunLight != null)
            {
                sunLight.intensity = Mathf.Lerp(sunLight.intensity, targetSunIntensity, lerpSpeed);
                sunLight.color = Color.Lerp(sunLight.color, targetSunColor, lerpSpeed);
            }
        }

        private void UpdateRecoveryCurtainPresentation()
        {
            var session = curtainSession;
            UpdateRecoveryEnvironment(session);
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
                        recoveryGemInstance.transform.localPosition = new Vector3(recoveryGemLaneX, 1.2f, remainingDistance);
                        recoveryGemInstance.ResetGem();
                    }
                }
            }
            else if (!session.RecoveryActive && recoveryGemInstance != null && recoveryGemInstance.gameObject.activeSelf)
            {
                recoveryGemInstance.gameObject.SetActive(false);
            }

            if (recoveryGemInstance == null || !recoveryGemInstance.gameObject.activeSelf || recoveryGemResolved)
                return;

            Vector3 currentPos = recoveryGemInstance.transform.localPosition;
            float nextZ = currentPos.z - travel;

            if (nextZ >= 0f)
            {
                recoveryGemInstance.transform.localPosition = new Vector3(recoveryGemLaneX, 1.2f, nextZ);
                recoveryGemInstance.Step(dt, session.Elapsed);
                return;
            }

            // Gem reached or crossed rider position (nextZ < 0f)
            recoveryGemResolved = true;
            float fraction = travel > 0f ? Mathf.Clamp01(currentPos.z / travel) : 0f;
            float riderX = step.XAt(fraction);
            float riderHeight = step.HeightAt(fraction);

            bool inLane = Mathf.Abs(recoveryGemLaneX - riderX) < 0.85f;
            bool canCollect = JumpArc.CanCollect(riderHeight, 1.2f);

            if (inLane && canCollect)
            {
                session.CollectRecoveryGem();
                recoveryGemInstance.OnCollected();
                EmitPickupFlash(transform.TransformPoint(new Vector3(recoveryGemLaneX, 1.2f, 0f)), settings.GemFlash);
                GemFeedback?.Invoke(0, session.CanActivateTurbo);
            }
            else
            {
                session.MissRecoveryGem();
                recoveryGemInstance.OnMissed();
            }
        }

        private void ResetRecoveryPresentation()
        {
            activeRecoveryGemAttempt = -1;
            recoveryGemResolved = false;
            if (recoveryGemInstance != null)
            {
                recoveryGemInstance.gameObject.SetActive(false);
            }
            trackScheduler?.NotifyRecoveryExited();
            RestoreDayEnvironment();
        }

        private void OnDisable()
        {
            RestoreDayEnvironment();
        }
    }
}
