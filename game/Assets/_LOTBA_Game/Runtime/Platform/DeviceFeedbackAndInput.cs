using UnityEngine;
using UnityEngine.InputSystem;

namespace Lotba.Game
{
    /// <summary>Capability-gated Android feedback and optional, calibrated tilt input.</summary>
    public sealed class DeviceFeedbackAndInput : MonoBehaviour
    {
        // Persistent keys are independent of type names and namespaces.
        private const string Prefix = "Lotba.CityRush.";
        private bool initialized, tiltRequested;
        private Accelerometer sensor;
        private bool sensorOwned;
        private float neutralX, filteredX, nextVibration, calibrationUntil;
        private int vibrationLevel = 2;
        private bool tiltArmed;
        private float neutralSince = -1f;
        public string VibrationLevelLabel => vibrationLevel == 0 ? "NO" :
            vibrationLevel == 1 ? "BAJA" : vibrationLevel == 2 ? "MEDIA" : "ALTA";
        public bool VibrationAvailable { get; private set; }
        public bool VibrationEnabled => VibrationAvailable && vibrationLevel > 0;
        public bool TiltAvailable => Application.isMobilePlatform && Accelerometer.current != null;
        public bool TiltEnabled => TiltAvailable && tiltRequested;
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
            tiltRequested = PlayerPrefs.GetInt(Prefix + "Tilt", 0) != 0;
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
        public void ToggleTilt()
        {
            if (!TiltAvailable) return;
            tiltRequested = !tiltRequested;
            PlayerPrefs.SetInt(Prefix + "Tilt", tiltRequested ? 1 : 0); PlayerPrefs.Save();
            SetSampling(tiltRequested);
            if (tiltRequested) Calibrate();
        }
        public void SetSampling(bool active)
        {
            if (!active || !TiltEnabled)
            {
                if (sensorOwned && sensor != null && sensor.added) InputSystem.DisableDevice(sensor);
                sensorOwned = false; return;
            }
            sensor = Accelerometer.current;
            if (sensor == null) return;
            if (!sensor.enabled) { InputSystem.EnableDevice(sensor); sensorOwned = true; }
        }
        public void Calibrate()
        {
            if (!TiltEnabled) return;
            SetSampling(true);
            neutralX = filteredX = ReadHorizontalGravity();
            calibrationUntil = Time.unscaledTime + .35f;
            tiltArmed = false;
            neutralSince = -1f;
        }
        public int ReadLaneDirection()
        {
            if (!TiltEnabled || sensor == null || !sensor.added || !sensor.enabled) return 0;
            float now = Time.unscaledTime;
            filteredX = Mathf.Lerp(filteredX, ReadHorizontalGravity(), 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
            if (now < calibrationUntil) { neutralX = filteredX; return 0; }

            float tilt = filteredX - neutralX;
            if (!tiltArmed)
            {
                if (Mathf.Abs(tilt) >= .11f) neutralSince = -1f;
                else if (neutralSince < 0f) neutralSince = now;
                else if (now - neutralSince >= .09f) tiltArmed = true;
                return 0;
            }
            if (Mathf.Abs(tilt) < .17f) return 0;
            tiltArmed = false;
            neutralSince = -1f;
            return tilt < 0f ? -1 : 1;
        }
        private float ReadHorizontalGravity()
        {
            Vector3 acceleration = sensor.acceleration.ReadValue();
            // Normalization uses gravity as a reference and removes backend unit differences.
            return acceleration.sqrMagnitude > .001f ? acceleration.normalized.x : 0;
        }
        public void Feedback(bool impact)
        {
            if (!VibrationEnabled || Time.unscaledTime < nextVibration) return;
            nextVibration = Time.unscaledTime + (impact ? .18f : .12f);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                long duration = (impact ? 35L : 12L) + (vibrationLevel - 1) * 8L;
                if (androidSdk >= 26)
                {
                    int amplitude = vibrator.Call<bool>("hasAmplitudeControl")
                        ? 120 + (vibrationLevel - 1) * 55 + (impact ? 20 : 0)
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
        private void OnDisable() { SetSampling(false); CancelFeedback(); }
        private void OnDestroy()
        {
            SetSampling(false); CancelFeedback();
#if UNITY_ANDROID && !UNITY_EDITOR
            vibrator?.Dispose(); vibrator = null;
#endif
        }
    }
}
