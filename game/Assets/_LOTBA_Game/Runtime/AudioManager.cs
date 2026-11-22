using Unity.Profiling;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Plays gameplay sound effects and applies the shared mute setting.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioManager : MonoBehaviour
    {
        private AudioSource source;
        private AudioSource musicSource, interfaceSource, brakeSource;
        private static readonly ProfilerMarker StartMusicMarker = new ProfilerMarker("Lotba.Audio.StartMusic");
        [Header("Background music")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = .35f;
        [Header("UI sounds")]
        [SerializeField] private AudioClip uiClick, brakeButton, boostButton;
        [SerializeField, Range(0f, 1f)] private float uiClickVolume = .35f, brakeButtonVolume = .55f, boostButtonVolume = .3f;
        [Header("Gameplay sounds")]
        private readonly AudioSource[] airborneSources = new AudioSource[4];
        [SerializeField] private AudioClip gemPickup;
        [SerializeField] private AudioClip pinkGemMiss;
        [SerializeField, Range(0f, 1f)] private float pinkGemMissVolume = .65f;
        [SerializeField] private AudioClip pinkGemStreakLost;
        [SerializeField, Range(0f, 1f)] private float pinkGemStreakLostVolume = .65f;
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
        [SerializeField] private AudioClip decisionShown;
        [SerializeField, Range(0f, 1f)] private float decisionShownVolume = .55f;
        public void PlayDecisionShown() => PlayInterface(decisionShown, decisionShownVolume);
        [SerializeField] private AudioClip boostReady;
        [SerializeField, Range(0f, 1f)] private float boostReadyVolume = .5f;
        [SerializeField] private AudioClip boostStart;
        [SerializeField, Range(0f, 1f)] private float boostStartVolume = .7f;
        [SerializeField] private AudioClip boostCollision;
        [SerializeField, Range(0f, 1f)] private float boostCollisionVolume = .8f;
        [Header("Gem loss")]
        [SerializeField] private AudioClip gemLoss, boostGemLoss;
        [SerializeField, Range(0f, 1f)] private float gemLossVolume = .45f, boostGemLossVolume = .35f;
        [Header("Streak")]
        [Header("Word collection")]
        [SerializeField] private AudioClip letterPickup;
        [SerializeField, Range(0f, 1f)] private float letterPickupVolume = .7f;
        [SerializeField] private AudioClip wordCompleted;
        [SerializeField, Range(0f, 1f)] private float wordCompletedVolume = .7f;
        public bool Muted { get; private set; }
        public float MusicLevel { get; private set; } = 1f;
        public float EffectsLevel { get; private set; } = 1f;
        private float effectsSourceVolume;
        private bool volumePreferencesDirty;
        private float saveVolumeAt;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            effectsSourceVolume = source.volume;
            source.playOnAwake = false;
            musicSource = CreateAuxiliarySource();
            musicSource.loop = true;
            interfaceSource = CreateAuxiliarySource();
            brakeSource = CreateAuxiliarySource();
            for (int i = 0; i < airborneSources.Length; i++)
            {
                AudioSource voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.outputAudioMixerGroup = source.outputAudioMixerGroup;
                voice.volume = source.volume;
                airborneSources[i] = voice;
            }
            MusicLevel = Mathf.Clamp01(PlayerPrefs.GetFloat("Lotba.CityRush.MusicVolume", 1f));
            EffectsLevel = Mathf.Clamp01(PlayerPrefs.GetFloat("Lotba.CityRush.EffectsVolume", 1f));
            ApplyLevels();
        }

        public void SetMusicLevel(float value)
        {
            MusicLevel = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("Lotba.CityRush.MusicVolume", MusicLevel);
            ScheduleVolumeSave();
            ApplyLevels();
        }

        public void SetEffectsLevel(float value)
        {
            EffectsLevel = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("Lotba.CityRush.EffectsVolume", EffectsLevel);
            ScheduleVolumeSave();
            ApplyLevels();
        }

        private void ApplyLevels()
        {
            source.volume = effectsSourceVolume * EffectsLevel;
            if (musicSource != null) musicSource.volume = backgroundMusicVolume * MusicLevel;
            if (interfaceSource != null) interfaceSource.volume = EffectsLevel;
            if (brakeSource != null) brakeSource.volume = brakeButtonVolume * EffectsLevel;
            foreach (var voice in airborneSources)
                if (voice != null) voice.volume = effectsSourceVolume * EffectsLevel;
        }

        private void ScheduleVolumeSave()
        {
            volumePreferencesDirty = true;
            saveVolumeAt = Time.unscaledTime + .5f;
        }

        private void Update()
        {
            if (volumePreferencesDirty && Time.unscaledTime >= saveVolumeAt) SaveVolumePreferences();
        }

        private void SaveVolumePreferences()
        {
            if (!volumePreferencesDirty) return;
            PlayerPrefs.Save();
            volumePreferencesDirty = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveVolumePreferences();
        }

        private void OnDisable() => SaveVolumePreferences();

        private AudioSource CreateAuxiliarySource()
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.mute = Muted;
            voice.outputAudioMixerGroup = source.outputAudioMixerGroup;
            return voice;
        }

        public void UpdateMusic(RunState state)
        {
            if (backgroundMusic == null || state == RunState.Finished || state == RunState.Ready)
            {
                if (musicSource.clip == null) return;
                musicSource.Stop();
                musicSource.clip = null;
                return;
            }
            if (state == RunState.Paused)
            {
                musicSource.Pause();
                return;
            }
            musicSource.volume = backgroundMusicVolume * MusicLevel;
            if (musicSource.clip == backgroundMusic) musicSource.UnPause();
            else
            {
                using (StartMusicMarker.Auto())
                {
                    musicSource.clip = backgroundMusic;
                    musicSource.Play();
                }
            }
        }

        public void SetMuted(bool muted)
        {
            if (source == null) source = GetComponent<AudioSource>();
            Muted = muted;
            source.mute = muted;
            if (musicSource != null) musicSource.mute = muted;
            if (brakeSource != null) brakeSource.mute = muted;
            if (interfaceSource != null)
            {
                interfaceSource.mute = muted;
                if (muted) interfaceSource.Stop();
            }
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
        public void PlayGemLoss(bool duringBoost)
        {
            Play(gemLoss, gemLossVolume);
            if (duringBoost) Play(boostGemLoss, boostGemLossVolume);
        }
        public void PlayBoostStart() => Play(boostStart, boostStartVolume);
        public void PlayBoostReady() => Play(boostReady, boostReadyVolume);
        public void PlayPinkGemMiss() => Play(pinkGemMiss != null ? pinkGemMiss : gemLoss, pinkGemMissVolume);
        public void PlayPinkGemStreakLost() => Play(pinkGemStreakLost, pinkGemStreakLostVolume);
        public void PlayUiClick() => PlayInterface(uiClick, uiClickVolume);
        public void PlayBrakePress()
        {
            if (Muted || brakeButton == null) return;
            brakeSource.Stop();
            brakeSource.clip = brakeButton;
            brakeSource.loop = false;
            brakeSource.volume = brakeButtonVolume * EffectsLevel;
            brakeSource.Play();
        }
        public void StopBrakePress() => brakeSource?.Stop();

        public void EndRun(bool playBrake, bool preserveBrakePress = false)
        {
            StopSources(preserveBrakePress);
            musicSource.Stop();
            musicSource.clip = null;
            interfaceSource.Stop();
            if (playBrake) PlayInterface(brakeButton, brakeButtonVolume);
        }
        public void PlayBoostButton() => PlayInterface(boostButton, boostButtonVolume);

        private void PlayInterface(AudioClip clip, float volume)
        {
            if (!Muted && clip != null) interfaceSource.PlayOneShot(clip, volume);
        }
        public void PlayCollision(bool duringBoost)
        {
            Play(collision, collisionVolume);
            if (duringBoost) Play(boostCollision, boostCollisionVolume);
        }
        public void PlayLetterPickup() => Play(letterPickup, letterPickupVolume);
        public void PlayWordCompleted() => Play(wordCompleted, wordCompletedVolume);

        public void Stop()
        {
            StopSources(false);
        }

        private void StopSources(bool preserveBrakePress)
        {
            source.Stop();
            if (!preserveBrakePress) StopBrakePress();
            foreach (AudioSource voice in airborneSources)
                if (voice != null) voice.Stop();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (!Muted && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
