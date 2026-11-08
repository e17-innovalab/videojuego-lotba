using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Plays gameplay sound effects and applies the shared mute setting.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioManager : MonoBehaviour
    {
        private AudioSource source;
        [SerializeField] private AudioClip gemPickup;
        [SerializeField, Range(0f, 1f)] private float gemPickupVolume = .7f;
        private AudioClip impact;
        public bool Muted { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
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
            if (impactFeedback) source.PlayOneShot(impact, .15f);
            else if (gemPickup != null) source.PlayOneShot(gemPickup, gemPickupVolume);
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
            if (impact != null) Destroy(impact);
        }
    }
}
