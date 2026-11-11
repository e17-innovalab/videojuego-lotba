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
        private bool boostVibrating;
        private int vibrationLevel = 2;
        public string VibrationLevelLabel => vibrationLevel == 0 ? "NO" :
            vibrationLevel == 1 ? "BAJA" : vibrationLevel == 2 ? "MEDIA" : "ALTA";
        public bool VibrationAvailable { get; private set; }
        public bool VibrationEnabled => VibrationAvailable && vibrationLevel > 0;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private int androidSdk;
        private bool amplitudeControl;
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
                amplitudeControl = VibrationAvailable && androidSdk >= 26 && vibrator.Call<bool>("hasAmplitudeControl");
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
        public void Feedback(bool impact, bool doubleImpact = false)
        {
            if (!VibrationEnabled || (!impact && (boostVibrating || Time.unscaledTime < nextVibration))) return;
            boostVibrating = false;
            nextVibration = Time.unscaledTime + (doubleImpact ? .3f : impact ? .18f : .12f);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                long duration = impact ? 85L + (vibrationLevel - 1) * 10L : 8L + (vibrationLevel - 1) * 3L;
                int amplitude = impact ? 175 + (vibrationLevel - 1) * 40 : 65 + (vibrationLevel - 1) * 35;
                if (doubleImpact)
                {
                    var timings = new[] { 0L, duration, 55L, duration };
                    if (androidSdk >= 26)
                    {
                        using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = amplitudeControl
                            ? effects.CallStatic<AndroidJavaObject>("createWaveform", timings, new[] { 0, amplitude, 0, amplitude }, -1)
                            : effects.CallStatic<AndroidJavaObject>("createWaveform", timings, -1))
                            vibrator.Call("vibrate", effect);
                    }
                    else vibrator.Call("vibrate", timings, -1);
                }
                else if (androidSdk >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", duration, amplitudeControl ? amplitude : -1))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", duration);
            }
            catch (AndroidJavaException) { VibrationAvailable = false; }
#endif
        }
        public void UpdateBoostFeedback(float remaining)
        {
            if (remaining <= 0f || !VibrationEnabled)
            {
                if (boostVibrating) CancelFeedback();
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // A motor without amplitude control cannot produce a soft sustained vibration.
            if (!amplitudeControl || boostVibrating || Time.unscaledTime < nextVibration) return;
            try
            {
                long duration = Mathf.Max(1, Mathf.CeilToInt(remaining * 1000f));
                int amplitude = 22 + (vibrationLevel - 1) * 14;
                using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", duration, amplitude))
                    vibrator.Call("vibrate", effect);
                boostVibrating = true;
            }
            catch (AndroidJavaException) { VibrationAvailable = false; }
#endif
        }

        public void CancelFeedback()
        {
            boostVibrating = false;
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
