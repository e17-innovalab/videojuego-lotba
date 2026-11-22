using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class RunnerWorld
    {
        private bool heldBrakePose, heldBrakeMoving, heldBrakeParticles, completedProgressiveBrake;
        private bool ridingPoseCaptured;
        private Vector3 ridingVisualPosition, ridingVisualScale;
        private Quaternion ridingVisualRotation, ridingSteeringRotation;
        private float markDistance;
        private float heldBrakeSpeed;
        private bool normalBrakeSpeedRestored;

        private void CaptureRidingPose()
        {
            ridingVisualPosition = riderVisual.localPosition;
            ridingVisualRotation = riderVisual.localRotation;
            ridingVisualScale = riderVisual.localScale;
            ridingSteeringRotation = steeringPivot.localRotation;
            ridingPoseCaptured = true;
        }

        private void RestoreRidingPose()
        {
            if (!ridingPoseCaptured) return;
            // Remove last frame's cosmetic overlay before the regular animator reads its input pose.
            riderVisual.SetLocalPositionAndRotation(ridingVisualPosition, ridingVisualRotation);
            riderVisual.localScale = ridingVisualScale;
            steeringPivot.localRotation = ridingSteeringRotation;
        }

        private void ResetProgressiveBrakePresentation()
        {
            heldBrakePose = heldBrakeMoving = heldBrakeParticles = completedProgressiveBrake = false;
            markDistance = riderDistance;
            CaptureRidingPose();
        }

        public void RenderHeldBrake(bool held, bool moving, bool particlesEnabled, float speed, bool normalSpeedRestored)
        {
            heldBrakePose = held;
            heldBrakeMoving = moving;
            heldBrakeParticles = particlesEnabled;
            heldBrakeSpeed = speed;
            normalBrakeSpeedRestored = normalSpeedRestored;
        }

        private void UpdateProgressiveBrakePresentation()
        {
            if (brakeEffects == null || !ridingPoseCaptured) return;
            float travel = Mathf.Max(0f, riderDistance - markDistance);
            markDistance = riderDistance;
            brakeEffects.ScrollTrail(-transform.forward * travel);

            bool grounded = rider.localPosition.y <= .06f && jump.HeightAt(cameraSession?.Elapsed ?? 0f) <= .01f;
            float previous = brakeEffects.PoseBlend;
            float previousPitch = brakeEffects.FrontPitch;
            brakeEffects.UpdatePose(heldBrakePose, completedProgressiveBrake, grounded,
                normalBrakeSpeedRestored, heldBrakeSpeed, Mathf.Min(Time.unscaledDeltaTime, .15f));
            float blend = brakeEffects.PoseBlend;
            float pitch = brakeEffects.FrontPitch;
            if (previous > 0f || blend > 0f || previousPitch > 0f || pitch > 0f)
            {
                RestoreRidingPose();
                steeringPivot.localRotation = Quaternion.Slerp(ridingSteeringRotation, Quaternion.identity, blend);
                // Rotate about the front axle, keeping its position fixed instead of sweeping sideways.
                Vector3 axle = frontWheel != null ? riderVisual.InverseTransformPoint(frontWheel.position) : Vector3.zero;
                Vector3 scaledAxle = Vector3.Scale(axle, ridingVisualScale);
                Vector3 anchor = ridingVisualPosition + ridingVisualRotation * scaledAxle;
                Quaternion rotation = Quaternion.Slerp(ridingVisualRotation, Quaternion.identity, blend) * Quaternion.Euler(pitch, 0f, 0f);
                Vector3 position = anchor - rotation * scaledAxle;
                position.y = Mathf.Max(position.y, WheelClearance(rotation, ridingVisualScale));
                riderVisual.SetLocalPositionAndRotation(position, rotation);
                riderVisual.localScale = ridingVisualScale;
                PoseRiderBody(pedalPhase, sprintPose * (1f - blend), riderSway * (1f - blend),
                    riderBank * (1f - blend), Mathf.Lerp(riderBrace, 1f, blend), riderTakeoff * (1f - blend),
                    Mathf.Lerp(riderLanding, .4f, blend), 0f);
                UpdateRiderShadow(rider.localPosition.x + position.x, rider.localPosition.y, position.z);
            }

            bool frontContact = TryFrontBrakeContact(out Vector3 contact);
            // Front braking: never emit a ground mark or dust from the raised rear wheel.
            brakeEffects.RenderHold(heldBrakePose, contact, -riderVisual.forward,
                heldBrakeMoving && heldBrakeParticles && grounded && frontContact, Time.unscaledTime);
        }

        private bool TryFrontBrakeContact(out Vector3 contact)
        {
            contact = rider.position;
            if (frontWheel == null || tyreRadii.x <= 0f) return false;
            Vector3 up = Vector3.Scale(frontWheel.InverseTransformDirection(Vector3.up), frontWheel.lossyScale);
            float tyreBottom = frontWheel.position.y - TyreRadiusAlong(up);
            float ground = transform.position.y;
            contact = new Vector3(frontWheel.position.x, ground + .009f, frontWheel.position.z);
            return Mathf.Abs(tyreBottom - ground) <= .04f;
        }
    }
}
