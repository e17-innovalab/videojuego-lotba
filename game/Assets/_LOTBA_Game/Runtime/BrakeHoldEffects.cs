using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Visual brake effects only. Audio belongs to AudioManager; gameplay belongs to the caller.</summary>
    public sealed class BrakeHoldEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private LineRenderer skidMark;
        [SerializeField, Min(.01f)] private float dustInterval = .035f;
        [SerializeField, Min(.01f)] private float holdDustSize = .25f;
        [SerializeField, Min(.05f)] private float holdDustLifetime = .6f;
        [SerializeField] private FrontBrakePoseSettings frontBraking = new FrontBrakePoseSettings();
        private FrontBrakePoseState frontPose = new FrontBrakePoseState();
        private float nextDust;
        private bool wasHeld, hasContact;
        private Vector3 lastContact;
        private float markStarted = float.NegativeInfinity;
        private ParticleSystem.Particle[] trailParticles;
        private static readonly Color MarkColor = new Color32(40, 42, 45, 175);
        public ParticleSystem Dust => dust;
        public LineRenderer SkidMark => skidMark;
        public bool HasGroundEffects => dust != null && skidMark != null;
        public float PoseBlend => frontPose.BodyBlend;
        public float FrontPitch => frontPose.Pitch;
        public float SettleSeconds => frontBraking.SettleSeconds;
        public void UpdatePose(bool held, bool completed, bool grounded, bool normalSpeedRestored, float speed, float deltaTime)
        {
            frontPose.Step(held, completed, grounded, normalSpeedRestored, speed, deltaTime, frontBraking);
        }

        public void RenderHold(bool held, Vector3 contact, Vector3 backward, bool emitDust, float now)
        {
            if (held && !wasHeld) BeginSkid(now);
            wasHeld = held;
            if (!held || !emitDust) return;
            markStarted = now;
            AddMark(contact);
            if (now < nextDust) return;
            EmitDust(contact, backward, holdDustSize, .8f);
            nextDust = now + dustInterval;
        }

        private void EmitDust(Vector3 contact, Vector3 backward, float size, float strength)
        {
            if (dust == null) return;
            if (!dust.isPlaying) dust.Play(false);
            var particle = new ParticleSystem.EmitParams
            {
                position = contact,
                velocity = (backward * .8f + Vector3.up * .65f) * strength,
                startColor = new Color32(218, 217, 210, 175),
                startSize = size,
                startLifetime = holdDustLifetime
            };
            dust.Emit(particle, 2);
        }

        public void ScrollTrail(Vector3 displacement)
        {
            if (displacement.sqrMagnitude <= 0f) return;
            if (skidMark != null)
                for (int i = 0; i < skidMark.positionCount; i++)
                    skidMark.SetPosition(i, skidMark.GetPosition(i) + displacement);
            lastContact += displacement;
            if (dust == null || dust.particleCount == 0) return;
            // Reuse storage; translate existing particles by the actual track travel, not a guessed speed.
            int capacity = dust.main.maxParticles;
            if (trailParticles == null || trailParticles.Length < capacity)
                trailParticles = new ParticleSystem.Particle[capacity];
            var main = dust.main;
            Vector3 particleDisplacement = displacement;
            if (main.simulationSpace == ParticleSystemSimulationSpace.Local)
                particleDisplacement = dust.transform.InverseTransformVector(displacement);
            else if (main.simulationSpace == ParticleSystemSimulationSpace.Custom && main.customSimulationSpace != null)
                particleDisplacement = main.customSimulationSpace.InverseTransformVector(displacement);
            int count = dust.GetParticles(trailParticles);
            for (int i = 0; i < count; i++) trailParticles[i].position += particleDisplacement;
            dust.SetParticles(trailParticles, count);
        }

        private void AddMark(Vector3 contact)
        {
            if (skidMark == null || (hasContact && (contact - lastContact).sqrMagnitude < .000225f)) return;
            int index = skidMark.positionCount;
            if (index >= 128)
            {
                for (int i = 1; i < 128; i++) skidMark.SetPosition(i - 1, skidMark.GetPosition(i));
                index = 127;
            }
            else skidMark.positionCount = index + 1;
            skidMark.SetPosition(index, contact);
            lastContact = contact;
            hasContact = true;
        }

        public void BeginSkid(float now)
        {
            StopHold();
            hasContact = false;
            markStarted = now;
            nextDust = now;
            if (skidMark == null) return;
            skidMark.positionCount = 0;
            skidMark.startColor = skidMark.endColor = MarkColor;
        }

        public void AddSkidContact(Vector3 contact, Vector3 velocity, float visualSpeed, float now)
        {
            AddMark(contact);
            if (dust == null || visualSpeed <= 0f || now < nextDust) return;
            if (!dust.isPlaying) dust.Play(false);
            var particle = new ParticleSystem.EmitParams
            {
                position = contact, velocity = velocity,
                startColor = new Color32(218, 217, 210, 175), startSize = .17f
            };
            dust.Emit(particle, 2);
            if (now - markStarted < .14f)
            {
                particle.velocity = velocity * 1.8f;
                particle.startColor = new Color32(255, 215, 134, 235);
                particle.startSize = .045f; particle.startLifetime = .10f;
                dust.Emit(particle, 1);
            }
            nextDust = now + .035f;
        }

        public void UpdatePresentation(float now)
        {
            if (skidMark == null || float.IsNegativeInfinity(markStarted)) return;
            float age = now - markStarted;
            Color color = MarkColor;
            color.a *= 1f - Mathf.InverseLerp(.55f, 2.5f, age);
            skidMark.startColor = skidMark.endColor = color;
            if (age < 2.5f) return;
            skidMark.positionCount = 0;
            markStarted = float.NegativeInfinity;
        }

        public void StopHold() => wasHeld = false;
        public void Clear()
        {
            StopHold();
            frontPose = new FrontBrakePoseState();
            if (dust != null) dust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (skidMark != null) skidMark.positionCount = 0;
            markStarted = float.NegativeInfinity;
            hasContact = false;
        }
        private void OnDisable() => Clear();
    }
}
