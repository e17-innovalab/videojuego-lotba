using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Reusable camera smoothing. The adapter supplies targets, tuning and its presentation clock.</summary>
    public sealed class RunnerCameraPresentation
    {
        public float FollowStrength, FollowTime, BoostPullback, BoostBlendRate, BoostFieldOfView;
        private Vector3 velocity;
        private float boostBlend;

        public void Reset()
        {
            velocity = Vector3.zero;
            boostBlend = 0f;
        }

        public void Step(Vector3 currentPosition, Vector3 origin, float targetX, float targetHeight,
            float currentFov, float paceIntensity, bool reducedMotion, bool boostEnabled, bool boosting,
            float deltaTime, out Vector3 position, out float referenceFov)
        {
            boostBlend = reducedMotion || !boostEnabled ? 0f : Mathf.Lerp(boostBlend,
                boosting ? 1f : 0f, 1f - Mathf.Exp(-BoostBlendRate * deltaTime));
            Vector3 target = origin + (reducedMotion ? Vector3.zero : Vector3.right * (targetX * FollowStrength));
            target.y += targetHeight;
            target += Vector3.back * (BoostPullback * boostBlend);
            position = Vector3.SmoothDamp(currentPosition, target, ref velocity, FollowTime, Mathf.Infinity, deltaTime);
            float fov = reducedMotion ? 60f : Mathf.Lerp(60f, 68f, paceIntensity) + BoostFieldOfView * boostBlend;
            referenceFov = Mathf.Lerp(currentFov, fov, 1f - Mathf.Exp(-10f * deltaTime));
        }
    }
}
