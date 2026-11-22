using System;

namespace Lotba.Game
{
    public enum BrakingMode { Immediate, HoldToStop }

    public readonly struct BrakingParameters
    {
        public readonly BrakingMode Mode;
        public readonly float Deceleration, ReleaseAcceleration;
        public BrakingParameters(BrakingMode mode, float deceleration, float releaseAcceleration)
        {
            Mode = mode;
            Deceleration = Math.Max(.1f, deceleration);
            ReleaseAcceleration = Math.Max(.1f, releaseAcceleration);
        }
    }

    /// <summary>Independent speed limiter. No input devices, Unity clock, session, rewards or visuals.</summary>
    public sealed class BrakingState
    {
        private readonly BrakingParameters parameters;
        private float initialSpeed;
        public bool IsHeld { get; private set; }
        public bool IsRestricting { get; private set; }
        public bool Complete => IsHeld && SpeedLimit <= .001f;
        public float SpeedLimit { get; private set; }
        public float Progress => IsHeld ? Math.Max(0f, Math.Min(1f, 1f - SpeedLimit / Math.Max(.001f, initialSpeed))) : 0f;

        public BrakingState(BrakingParameters parameters) => this.parameters = parameters;

        public void Step(bool held, float currentSpeed, float freeSpeed, float deltaTime)
        {
            float dt = Math.Max(0f, deltaTime);
            if (held && !IsHeld)
            {
                initialSpeed = SpeedLimit = Math.Max(0f, currentSpeed);
                IsRestricting = true;
            }
            IsHeld = held;
            if (held)
                SpeedLimit = parameters.Mode == BrakingMode.Immediate ? 0f : Math.Max(0f, SpeedLimit - parameters.Deceleration * dt);
            else if (IsRestricting)
            {
                SpeedLimit = Math.Min(Math.Max(0f, freeSpeed), SpeedLimit + parameters.ReleaseAcceleration * dt);
                if (SpeedLimit >= freeSpeed) IsRestricting = false;
            }
        }
    }
}
