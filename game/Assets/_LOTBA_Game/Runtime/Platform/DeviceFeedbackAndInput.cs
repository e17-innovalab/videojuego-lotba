using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Capability-gated Android vibration feedback.</summary>
    public sealed class DeviceFeedbackAndInput : MonoBehaviour
    {
        // Persistent keys are independent of type names and namespaces.
        private const string Prefix = "Lotba.CityRush.";
        private bool initialized;
        private float nextVibration;
        private int vibrationLevel = 2;
        public string VibrationLevelLabel => vibrationLevel == 0 ? "NO" :
            vibrationLevel == 1 ? "BAJA" : vibrationLevel == 2 ? "MEDIA" : "ALTA";
        public bool VibrationAvailable { get; private set; }
        public bool VibrationEnabled => VibrationAvailable && vibrationLevel > 0;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private int androidSdk;
#endif
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            int defaultLevel = PlayerPrefs.GetInt(Prefix + "Vibration", 1) == 0 ? 0 : 2;
            vibrationLevel = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "VibrationLevel", defaultLevel), 0, 3);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                VibrationAvailable = vibrator != null && vibrator.Call<bool>("hasVibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) androidSdk = version.GetStatic<int>("SDK_INT");
            }
            catch (AndroidJavaException) { VibrationAvailable = false; }
#endif
        }
        public void CycleVibrationLevel()
        {
            if (!VibrationAvailable) return;
            vibrationLevel = (vibrationLevel + 1) % 4;
            PlayerPrefs.SetInt(Prefix + "VibrationLevel", vibrationLevel);
            PlayerPrefs.Save();
            CancelFeedback();
            nextVibration = 0;
            Feedback(false);
        }
        public void Feedback(bool impact)
        {
            if (!VibrationEnabled || (!impact && Time.unscaledTime < nextVibration)) return;
            nextVibration = Time.unscaledTime + (impact ? .18f : .12f);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                long duration = impact ? 85L + (vibrationLevel - 1) * 10L : 8L + (vibrationLevel - 1) * 3L;
                if (androidSdk >= 26)
                {
                    int amplitude = vibrator.Call<bool>("hasAmplitudeControl")
                        ? (impact ? 175 + (vibrationLevel - 1) * 40 : 65 + (vibrationLevel - 1) * 35)
                        : -1;
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", duration, amplitude))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", duration);
            }
            catch (AndroidJavaException) { VibrationAvailable = false; }
#endif
        }
        public void CancelFeedback()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (vibrator != null && VibrationAvailable)
                try { vibrator.Call("cancel"); } catch (AndroidJavaException) { VibrationAvailable = false; }
#endif
        }
        private void OnDisable() { CancelFeedback(); }
        private void OnDestroy()
        {
            CancelFeedback();
#if UNITY_ANDROID && !UNITY_EDITOR
            vibrator?.Dispose(); vibrator = null;
#endif
        }
    }
}
