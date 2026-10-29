using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Owns short generated feedback clips and releases them with the scene.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioFeedback : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip pickup;
        private AudioClip impact;
        public bool Muted { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = .15f;
            pickup = CreateTone("Coin pickup", 660f, .08f);
            impact = CreateTone("Loss feedback", 130f, .16f);
        }

        public void SetMuted(bool muted)
        {
            if (source == null) source = GetComponent<AudioSource>();
            Muted = muted;
            source.mute = muted;
            if (muted) source.Stop();
        }

        public void Play(bool impactFeedback)
        {
            if (Muted) return;
            if (!impactFeedback)
                source.PlayOneShot(pickup);
            else
                source.PlayOneShot(impact);
        }

        public void Stop() => source.Stop();

        private static AudioClip CreateTone(string name, float frequency, float duration)
        {
            const int sampleRate = 22050;
            float[] samples = new float[Mathf.CeilToInt(duration * sampleRate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float envelope = Mathf.Sin(Mathf.PI * i / (samples.Length - 1));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * envelope;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (pickup != null) Destroy(pickup);
            if (impact != null) Destroy(impact);
        }
    }
}
