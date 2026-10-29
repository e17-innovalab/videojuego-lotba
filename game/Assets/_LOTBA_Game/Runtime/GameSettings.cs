using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Shared gameplay tuning values. All speeds use metres per second.</summary>
    [CreateAssetMenu(menuName = "LOTBA/Game settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [SerializeField, Min(30f)] private float duration = 120f;
        [SerializeField, Min(1f)] private float initialSpeed = 18f;
        [SerializeField, Min(1f)] private float maximumSpeed = 28f;
        [SerializeField, Min(1f)] private float accelerationTime = 35f;
        [SerializeField, Range(.07f, .25f)] private float laneChangeTime = .11f;
        [SerializeField, Range(.65f, 2f)] private float initialPatternInterval = 1.3f;
        [SerializeField, Range(.65f, 2f)] private float finalPatternInterval = .85f;
        [SerializeField, Min(2f)] private float previewTime = 3.3f;
        [SerializeField] private int seed = 2710;
        [Header("Camera follow")]
        [SerializeField, Range(.05f, .3f)] private float cameraFollowTime = .13f;
        [SerializeField, Range(.5f, 1f)] private float cameraFollowStrength = .95f;
        public float CameraFollowTime => cameraFollowTime;
        public float CameraFollowStrength => cameraFollowStrength;
        [Header("Streak charge and decay")]
        [SerializeField, Min(2f)] private float firstLevelCoins = 8f;
        [SerializeField, Min(.1f)] private float extraCoinsPerLevel = 3f;
        [SerializeField, Range(.005f, .15f)] private float streakDrainPerSecond = .025f;
        [SerializeField, Range(.1f, .5f)] private float levelUpReserve = .3f;
        public float FirstLevelCoins => firstLevelCoins;
        public float ExtraCoinsPerLevel => extraCoinsPerLevel;
        public float StreakDrainPerSecond => streakDrainPerSecond;
        public float LevelUpReserve => levelUpReserve;
        public float Duration => duration;
        public float InitialSpeed => initialSpeed;
        public float MaximumSpeed => Mathf.Max(initialSpeed, maximumSpeed);
        public float AccelerationTime => accelerationTime;
        public float LaneChangeTime => laneChangeTime;
        public float InitialPatternInterval => initialPatternInterval;
        public float FinalPatternInterval => finalPatternInterval;
        public float PreviewTime => previewTime;
        public int Seed => seed;
    }
}
