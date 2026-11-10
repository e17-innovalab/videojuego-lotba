using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Plays gameplay sound effects and applies the shared mute setting.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioManager : MonoBehaviour
    {
        private AudioSource source;
        private readonly AudioSource[] airborneSources = new AudioSource[4];
        [SerializeField] private AudioClip gemPickup;
        [SerializeField, Range(0f, 1f)] private float gemPickupVolume = .7f;
        [SerializeField, Tooltip("Semitone offsets for the four airborne gems, in track order.")]
        private Vector4 airborneGemSemitones = new Vector4(0f, 4f, 7f, 12f);
        [SerializeField] private AudioClip laneSlide;
        [SerializeField, Range(0f, 1f)] private float laneSlideVolume = .55f;
        [SerializeField] private AudioClip jump;
        [SerializeField, Range(0f, 1f)] private float jumpVolume = .55f;
        [SerializeField] private AudioClip collision;
        [SerializeField, Range(0f, 1f)] private float collisionVolume = .65f;
        [Header("Boost")]
        [SerializeField] private AudioClip boostStart;
        [SerializeField, Range(0f, 1f)] private float boostStartVolume = .7f;
        [SerializeField] private AudioClip boostCollision;
        [SerializeField, Range(0f, 1f)] private float boostCollisionVolume = .8f;
        [Header("Streak")]
        [SerializeField] private AudioClip streakLevelUp;
        [SerializeField, Range(0f, 1f)] private float streakLevelUpVolume = .7f;
        [Header("Word collection")]
        [SerializeField] private AudioClip letterPickup;
        [SerializeField, Range(0f, 1f)] private float letterPickupVolume = .7f;
        [SerializeField] private AudioClip wordCompleted;
        [SerializeField, Range(0f, 1f)] private float wordCompletedVolume = .7f;
        public bool Muted { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            for (int i = 0; i < airborneSources.Length; i++)
            {
                AudioSource voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.outputAudioMixerGroup = source.outputAudioMixerGroup;
                voice.volume = source.volume;
                airborneSources[i] = voice;
            }
        }

        public void SetMuted(bool muted)
        {
            if (source == null) source = GetComponent<AudioSource>();
            Muted = muted;
            source.mute = muted;
            if (muted) Stop();
        }

        public void PlayGemPickup(int airborneNote)
        {
            if (airborneNote < 0 || airborneNote >= airborneSources.Length)
            {
                Play(gemPickup, gemPickupVolume);
                return;
            }
            if (Muted || gemPickup == null) return;
            AudioSource voice = airborneSources[airborneNote];
            voice.pitch = Mathf.Pow(2f, Mathf.Clamp(airborneGemSemitones[airborneNote], -24f, 19f) / 12f);
            voice.PlayOneShot(gemPickup, gemPickupVolume);
        }
        public void PlayLaneSlide() => Play(laneSlide, laneSlideVolume);
        public void PlayJump() => Play(jump, jumpVolume);
        public void PlayBoostStart() => Play(boostStart, boostStartVolume);
        public void PlayCollision(bool duringBoost)
        {
            if (duringBoost && boostCollision != null) Play(boostCollision, boostCollisionVolume);
            else Play(collision, collisionVolume);
        }
        public void PlayStreakLevelUp() => Play(streakLevelUp, streakLevelUpVolume);
        public void PlayLetterPickup() => Play(letterPickup, letterPickupVolume);
        public void PlayWordCompleted() => Play(wordCompleted, wordCompletedVolume);

        public void Stop()
        {
            source.Stop();
            foreach (AudioSource voice in airborneSources)
                if (voice != null) voice.Stop();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (!Muted && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
