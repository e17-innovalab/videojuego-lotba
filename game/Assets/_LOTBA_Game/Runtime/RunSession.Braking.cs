using System;

namespace Lotba.Game
{
    public sealed partial class RunSession
    {
        private BrakingState braking;
        public BrakingParameters BrakingConfiguration { get; private set; }
        public BrakingMode BrakeMode => BrakingConfiguration.Mode;
        public bool BrakeHeld => braking.IsHeld;
        public bool BrakingSpeedRestricted => braking.IsRestricting;
        public float BrakeProgress => braking.Progress;
        public int BrakePresses { get; private set; }
        public int BrakeReleases { get; private set; }
        public float BrakeHeldSeconds { get; private set; }

        /// <summary>Input adapter calls once per presentation frame, also during paused decisions.</summary>
        public void UpdateBrakeInput(bool held, bool clicked, float deltaTime)
        {
            if (State != RunState.Running && State != RunState.Paused) return;
            if (BrakeMode == BrakingMode.Immediate)
            {
                if (!clicked) return;
                BrakePresses++;
                Record("brake_started", "immediate");
                Record("brake_completed", "immediate");
                Brake();
                return;
            }
            if (held && !braking.IsHeld)
            {
                BrakePresses++;
                Record("brake_started", "hold_to_stop");
            }
            else if (!held && braking.IsHeld)
            {
                BrakeReleases++;
                Record("brake_released", "input_not_held");
            }
            float dt = Math.Max(0f, deltaTime);
            if (held) BrakeHeldSeconds += dt;
            braking.Step(held, Speed, CalculateUnbrakedSpeed(), dt);
            if (braking.IsRestricting) Speed = Math.Min(Speed, braking.SpeedLimit);
            if (!braking.Complete) return;
            Record("brake_completed", "hold_to_stop");
            Brake();
        }

        private float CalculateUnbrakedSpeed()
        {
            float progression = 1f - (float)Math.Exp(-Elapsed / settings.AccelerationTime);
            float baseSpeed = settings.InitialSpeed + (settings.CruiseSpeed - settings.InitialSpeed) * progression +
                settings.SustainedAcceleration * Elapsed;
            if (telemetryRoute != null) baseSpeed += telemetryRoute.SpeedBonusAt(Distance);
            float recovery = Math.Min(1f, (Elapsed - impactTime) / ImpactRecoverySeconds);
            return Math.Max(0f, baseSpeed + PurchasedSpeed + DecisionBoostSpeed +
                BoostSpeedBonus - ImpactSpeedLoss * (1f - recovery));
        }
    }
}
