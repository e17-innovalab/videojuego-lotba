using System;
using UnityEngine;

namespace Lotba.Game
{
    [Serializable]
    public sealed class FrontBrakePoseSettings
    {
        [SerializeField, Min(1f)] private float liftMinimumSpeed = 26f;
        [SerializeField, Range(0f, 40f)] private float maximumPitch = 24f;
        [SerializeField, Min(.05f)] private float liftSeconds = .8f;
        [SerializeField, Min(.05f)] private float returnSeconds = .22f;
        [SerializeField, Min(.05f)] private float settleSeconds = .6f;
        [SerializeField, Range(0f, 6f)] private float inertiaDegrees = 2f;
        public float LiftMinimumSpeed => liftMinimumSpeed;
        public float MaximumPitch => maximumPitch;
        public float LiftSeconds => Mathf.Max(.05f, liftSeconds);
        public float ReturnSeconds => Mathf.Max(.05f, returnSeconds);
        public float SettleSeconds => Mathf.Max(.05f, settleSeconds);
        public float InertiaDegrees => inertiaDegrees;
    }

    /// <summary>Cosmetic forward braking state. Explicit inputs and clock; no run, input device or scene ownership.</summary>
    public sealed class FrontBrakePoseState
    {
        private bool wasHeld, interrupted, lifting, completed;
        private float liftAge, initialPitch, targetPitch, settleAge, stopPitch, stopBlend;
        public float Pitch { get; private set; }
        public float BodyBlend { get; private set; }

        public void Step(bool held, bool stopped, bool grounded, bool normalSpeedRestored,
            float speed, float dt, FrontBrakePoseSettings settings)
        {
            dt = Mathf.Max(0f, dt);
            if (stopped)
            {
                if (!completed)
                {
                    completed = true;
                    stopPitch = Pitch; stopBlend = BodyBlend; settleAge = 0f;
                }
                settleAge += dt;
                float t = Mathf.Clamp01(settleAge / settings.SettleSeconds);
                float ease = Mathf.SmoothStep(0f, 1f, t);
                float inertia = settings.InertiaDegrees * Mathf.Clamp01(stopPitch / 5f) *
                    Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Exp(-4f * t) * (1f - t);
                Pitch = Mathf.Max(0f, stopPitch * (1f - ease) + inertia);
                BodyBlend = stopBlend * (1f - ease);
                return;
            }
            if (!held && wasHeld) { interrupted = true; lifting = false; }
            if (!held && normalSpeedRestored && Pitch <= .01f && BodyBlend <= .01f) interrupted = false;
            if (held && !wasHeld)
            {
                lifting = grounded && !interrupted && speed >= settings.LiftMinimumSpeed;
                liftAge = 0f; initialPitch = Pitch;
                float speedFactor = Mathf.InverseLerp(settings.LiftMinimumSpeed, settings.LiftMinimumSpeed * 1.6f, speed);
                targetPitch = settings.MaximumPitch * Mathf.Lerp(.35f, 1f, speedFactor);
            }
            if (held && lifting && grounded)
            {
                liftAge += dt;
                Pitch = Mathf.Lerp(initialPitch, targetPitch, Mathf.SmoothStep(0f, 1f, liftAge / settings.LiftSeconds));
            }
            else Pitch = Mathf.MoveTowards(Pitch, 0f, Mathf.Max(1f, settings.MaximumPitch) * dt / settings.ReturnSeconds);
            BodyBlend = Mathf.MoveTowards(BodyBlend, held && grounded ? 1f : 0f, dt / settings.ReturnSeconds);
            wasHeld = held;
        }
    }
}
