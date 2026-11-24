using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Editable presentation for the recovery reward and its distant pursuit preview.</summary>
    [DefaultExecutionOrder(110)]
    public sealed class RecoveryGemView : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform glowRoot;
        [SerializeField] private ParticleSystem glowParticles;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform pursuitDestination;
        [SerializeField] private bool playOnStart = true;

        [Header("Trayectoria intro")]
        [SerializeField, Min(.1f)] private float introDuration = 3f;
        [SerializeField] private AnimationCurve curveX;
        [SerializeField] private AnimationCurve curveY;
        [SerializeField] private AnimationCurve curveZ;

        [Header("Persecución")]
        [SerializeField, Min(0f)] private float pursuitDistance = 60f;
        [SerializeField, Min(0f)] private float pursuitHeight = 6f;
        [SerializeField, Min(.01f)] private float destinationBlendDuration = .45f;

        [Header("Flotación y giro")]
        [SerializeField, Min(0f)] private float floatAmplitude = .25f;
        [SerializeField, Min(0f)] private float floatHorizontalAmplitude = .08f;
        [SerializeField, Min(0f)] private float floatSpeed = 2.5f;
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField, Min(0f)] private float tiltAmount = 18f;

        private Vector3 initialVisualLocalPosition;
        private Vector3 initialGlowLocalPosition;
        private Quaternion initialVisualLocalRotation;
        private bool collected;
        private bool introActive;
        private bool destinationBlendActive;
        private bool atDestination;
        private float introElapsed;
        private float destinationBlendElapsed;
        private float spinAngle;
        private float previousX;
        private float currentX;
        private float tilt;
        private Vector3 destinationBlendStart;
        private Vector3 finalPursuitOffset;
        private Vector3 finalDestinationPosition;
        private Vector3 worldPursuitPosition;

        public bool IsCollected => collected;
        public bool IsAnimating => introActive || destinationBlendActive;
        public bool IsAtDestination => atDestination;

        private void Reset()
        {
            curveX = new AnimationCurve(
                new Keyframe(0f, -4.5f), new Keyframe(.25f, -3.5f),
                new Keyframe(.48f, 2.5f), new Keyframe(.62f, 3f),
                new Keyframe(.82f, 1.1f), new Keyframe(1f, 0f));
            curveY = new AnimationCurve(
                new Keyframe(0f, 1.7f), new Keyframe(.4f, 1.7f),
                new Keyframe(.65f, 1.9f), new Keyframe(1f, 1.9f));
            curveZ = new AnimationCurve(
                new Keyframe(0f, 1.8f), new Keyframe(.42f, 1.8f),
                new Keyframe(.65f, 2.6f), new Keyframe(.84f, 6f),
                new Keyframe(1f, 12f));
            SetSmoothTangents(curveX);
            SetSmoothTangents(curveY);
            SetSmoothTangents(curveZ);
        }

        private static void SetSmoothTangents(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
        }

        private void SmoothIntroCurves()
        {
            if (curveX != null) SetSmoothTangents(curveX);
            if (curveY != null) SetSmoothTangents(curveY);
            if (curveZ != null) SetSmoothTangents(curveZ);
        }

        private void OnValidate()
        {
            if (curveX == null || curveX.length == 0 || curveY == null || curveY.length == 0 ||
                curveZ == null || curveZ.length == 0)
                Reset();
        }

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform.Find("Visual") ?? transform;
            if (glowRoot == null) glowRoot = transform.Find("Glow suave");
            initialVisualLocalPosition = visualRoot.localPosition;
            initialVisualLocalRotation = visualRoot.localRotation;
            if (glowRoot != null) initialGlowLocalPosition = glowRoot.localPosition;
            CachePursuitOffset();
        }

        private void Start()
        {
            if (playOnStart && playerTarget != null && !introActive) PlayIntro();
        }

        /// <summary>Supplies runtime targets without coupling this reusable view to game-session code.</summary>
        public void ConfigurePursuit(Transform player, Transform destination, float distance, float height)
        {
            playerTarget = player;
            pursuitDestination = destination;
            pursuitDistance = Mathf.Max(0f, distance);
            pursuitHeight = Mathf.Max(0f, height);
            CachePursuitOffset();
        }

        public void PlayIntro()
        {
            if (playerTarget == null) return;
            collected = false;
            atDestination = false;
            destinationBlendActive = false;
            introElapsed = 0f;
            spinAngle = 0f;
            previousX = 0f;
            currentX = 0f;
            tilt = 0f;
            introActive = introDuration > 0f;
            SmoothIntroCurves();
            CachePursuitOffset();
            if (!introActive)
            {
                transform.position = playerTarget.TransformPoint(finalPursuitOffset);
                worldPursuitPosition = transform.position;
            }
        }

        public void BeginFinalApproach()
        {
            if (atDestination || destinationBlendActive) return;
            introActive = false;
            destinationBlendElapsed = 0f;
            destinationBlendStart = transform.position;
            worldPursuitPosition = destinationBlendStart;
            destinationBlendActive = destinationBlendDuration > 0f;
            if (!destinationBlendActive) SnapToDestination();
        }

        public void ResetGem()
        {
            collected = false;
            atDestination = false;
            introActive = false;
            destinationBlendActive = false;
            gameObject.SetActive(true);
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
                visualRoot.localPosition = initialVisualLocalPosition;
                visualRoot.localRotation = initialVisualLocalRotation;
            }
            if (glowRoot != null)
            {
                glowRoot.gameObject.SetActive(true);
                glowRoot.localPosition = initialGlowLocalPosition;
            }
            if (glowParticles != null)
            {
                glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                glowParticles.Play();
            }
        }

        public void SetVisualVisible(bool visible)
        {
            if (visualRoot != null && visualRoot.gameObject.activeSelf != visible)
                visualRoot.gameObject.SetActive(visible);
            if (glowRoot != null && glowRoot.gameObject.activeSelf != visible)
                glowRoot.gameObject.SetActive(visible);
            if (glowParticles == null) return;
            if (visible && !glowParticles.isPlaying) glowParticles.Play();
            else if (!visible && glowParticles.isPlaying)
                glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>Advances presentation time from the game simulation; LateUpdate owns the transforms.</summary>
        public void Step(float dt, float elapsed)
        {
            if (collected) return;
            dt = Mathf.Max(0f, dt);
            spinAngle = Mathf.Repeat(spinAngle + spinSpeed * dt, 360f);

            if (introActive)
            {
                introElapsed += dt;
                if (introElapsed >= introDuration)
                {
                    introElapsed = introDuration;
                    float endX = curveX != null ? curveX.Evaluate(1f) : 0f;
                    worldPursuitPosition = playerTarget.TransformPoint(
                        new Vector3(endX, EvaluateChaseY(1f), EvaluateChaseZ(1f)));
                    introActive = false;
                }
            }

            if (destinationBlendActive)
            {
                destinationBlendElapsed += dt;
                if (destinationBlendElapsed >= destinationBlendDuration)
                {
                    destinationBlendActive = false;
                    atDestination = true;
                }
            }

            if (visualRoot != null)
            {
                float bob = Mathf.Sin(elapsed * floatSpeed) * floatAmplitude;
                float swayOffset = Mathf.Cos(elapsed * floatSpeed) * floatHorizontalAmplitude;
                visualRoot.localPosition = initialVisualLocalPosition + new Vector3(swayOffset, bob, 0f);
                float sway = Mathf.Clamp((currentX - previousX) / Mathf.Max(dt, .0001f), -1f, 1f);
                tilt = Mathf.Lerp(tilt, -sway * tiltAmount, Mathf.Clamp01(dt * 8f));
                visualRoot.localRotation = initialVisualLocalRotation * Quaternion.Euler(0f, spinAngle, 0f) *
                                           Quaternion.Euler(0f, 0f, tilt);
                if (glowRoot != null)
                    glowRoot.localPosition = initialGlowLocalPosition + new Vector3(swayOffset, bob, 0f);
            }
        }

        private void LateUpdate()
        {
            if (collected) return;
            if (destinationBlendActive)
            {
                float t = Mathf.Clamp01(destinationBlendElapsed / destinationBlendDuration);
                Vector3 target = GetDestinationPosition();
                transform.position = Vector3.Lerp(destinationBlendStart, target, Mathf.SmoothStep(0f, 1f, t));
                finalDestinationPosition = target;
            }
            else if (atDestination)
            {
                transform.position = GetDestinationPosition();
            }
            else if (introActive)
            {
                float t = Mathf.Clamp01(introElapsed / Mathf.Max(.0001f, introDuration));
                float easedT = Mathf.SmoothStep(0f, 1f, t);
                float x = curveX != null ? curveX.Evaluate(easedT) : 0f;
                float y = EvaluateChaseY(easedT);
                float z = EvaluateChaseZ(easedT);
                currentX = x;
                transform.position = playerTarget.TransformPoint(new Vector3(x, y, z));
                if (!introActive)
                    worldPursuitPosition = transform.position;
            }
            else if (playerTarget != null)
            {
                // Anchor the gem in world space after the intro. The child keeps floating,
                // but player motion no longer moves the pursuit preview with it.
                transform.position = worldPursuitPosition;
            }
            previousX = currentX;
        }

        private float EvaluateChaseY(float t)
        {
            float curveValue = curveY != null ? curveY.Evaluate(t) : pursuitHeight;
            float endValue = curveY != null ? curveY.Evaluate(1f) : pursuitHeight;
            return curveValue + (pursuitHeight - endValue) * Mathf.SmoothStep(0f, 1f, t);
        }

        private float EvaluateChaseZ(float t)
        {
            float start = curveZ != null ? curveZ.Evaluate(0f) : 0f;
            float end = curveZ != null ? curveZ.Evaluate(1f) : 1f;
            float value = curveZ != null ? curveZ.Evaluate(t) : Mathf.Lerp(start, end, t);
            float normalized = Mathf.Abs(end - start) > .0001f ? Mathf.InverseLerp(start, end, value) : t;
            return Mathf.Lerp(start, pursuitDistance, normalized);
        }

        private void CachePursuitOffset()
        {
            float x = curveX != null ? curveX.Evaluate(1f) : 0f;
            finalPursuitOffset = new Vector3(x, pursuitHeight, pursuitDistance);
        }

        private Vector3 GetDestinationPosition()
        {
            if (pursuitDestination != null) return pursuitDestination.position;
            if (atDestination) return finalDestinationPosition;
            return playerTarget != null ? playerTarget.TransformPoint(finalPursuitOffset) : transform.position;
        }

        private void SnapToDestination()
        {
            transform.position = GetDestinationPosition();
            finalDestinationPosition = transform.position;
            atDestination = true;
        }

        private void OnDrawGizmosSelected()
        {
            Transform target = playerTarget != null ? playerTarget : transform;
            AnimationCurve xCurve = curveX;
            AnimationCurve yCurve = curveY;
            AnimationCurve zCurve = curveZ;
            if (xCurve == null || xCurve.length == 0 || yCurve == null || yCurve.length == 0 ||
                zCurve == null || zCurve.length == 0) return;

            Gizmos.color = new Color(1f, .78f, .08f, .9f);
            Vector3 previous = target.TransformPoint(new Vector3(xCurve.Evaluate(0f), yCurve.Evaluate(0f), zCurve.Evaluate(0f)));
            for (int i = 1; i <= 40; i++)
            {
                float t = i / 40f;
                Vector3 point = target.TransformPoint(new Vector3(xCurve.Evaluate(t), EvaluateChaseY(t), EvaluateChaseZ(t)));
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.DrawWireSphere(previous, .25f);
        }

        public void OnCollected()
        {
            collected = true;
            if (glowParticles != null) glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        public void OnMissed()
        {
            if (glowParticles != null) glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            gameObject.SetActive(false);
        }
    }
}
