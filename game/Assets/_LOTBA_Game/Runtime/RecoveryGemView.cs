using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Componente visual para la gema dorada de recupero que aparece al final del atajo.
    /// Controla animación de rotación, flotación y visibilidad sin generar mallas por código.
    /// </summary>
    public sealed class RecoveryGemView : MonoBehaviour
    {
        [Header("Transform")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float bobAmplitude = 0.25f;
        [SerializeField] private float bobFrequency = 2.5f;

        [Header("Efectos")]
        [SerializeField] private ParticleSystem glowParticles;

        private Vector3 initialLocalPos;
        private bool collected;

        public bool IsCollected => collected;

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.Find("Visual") ?? transform;
            }
            initialLocalPos = visualRoot.localPosition;
        }

        public void ResetGem()
        {
            collected = false;
            gameObject.SetActive(true);
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(true);
                visualRoot.localPosition = initialLocalPos;
            }
            if (glowParticles != null)
            {
                glowParticles.Play();
            }
        }

        public void Step(float dt, float elapsed)
        {
            if (collected || visualRoot == null) return;

            // Rotación sobre el eje Y
            visualRoot.Rotate(Vector3.up, rotationSpeed * dt, Space.World);

            // Flotación senoidal
            float yOffset = Mathf.Sin(elapsed * bobFrequency) * bobAmplitude;
            visualRoot.localPosition = initialLocalPos + new Vector3(0f, yOffset, 0f);
        }

        public void OnCollected()
        {
            collected = true;
            if (glowParticles != null)
            {
                glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }

        public void OnMissed()
        {
            if (glowParticles != null)
            {
                glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            gameObject.SetActive(false);
        }
    }
}
