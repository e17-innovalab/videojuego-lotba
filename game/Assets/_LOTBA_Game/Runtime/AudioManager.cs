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
        [SerializeField] private AudioClip laneSlide;
        [SerializeField, Range(0f, 1f)] private float laneSlideVolume = .55f;
        [SerializeField] private AudioClip jump;
        [SerializeField, Range(0f, 1f)] private float jumpVolume = .55f;
        [SerializeField] private AudioClip collision;
        [SerializeField, Range(0f, 1f)] private float collisionVolume = .65f;
        [SerializeField] private AudioClip streakLevelUp;
        [SerializeField, Range(0f, 1f)] private float streakLevelUpVolume = .7f;
        public bool Muted { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
        }

        public void SetMuted(bool muted)
        {
            if (source == null) source = GetComponent<AudioSource>();
            Muted = muted;
            source.mute = muted;
            if (muted) source.Stop();
        }

        public void PlayGemPickup() => Play(gemPickup, gemPickupVolume);
        public void PlayLaneSlide() => Play(laneSlide, laneSlideVolume);
        public void PlayJump() => Play(jump, jumpVolume);
        public void PlayCollision() => Play(collision, collisionVolume);
        public void PlayStreakLevelUp() => Play(streakLevelUp, streakLevelUpVolume);

        public void Stop() => source.Stop();

        private void Play(AudioClip clip, float volume)
        {
            if (!Muted && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
