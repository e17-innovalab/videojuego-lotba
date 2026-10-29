using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lotba.Game
{
    /// <summary>Capability-gated Android feedback and optional, calibrated tilt input.</summary>
    public sealed class DeviceFeedbackAndInput : MonoBehaviour
    {
        // Persistent keys are independent of type names and namespaces.
        private const string Prefix = "Lotba.CityRush.";
        private bool initialized, vibrationRequested = true, tiltRequested;
        private Accelerometer sensor;
        private bool sensorOwned;
        private float neutralX, filteredX, nextTiltCommand, nextVibration, calibrationUntil;
        private int tiltLane = 1;
        public bool VibrationAvailable { get; private set; }
        public bool VibrationEnabled => VibrationAvailable && vibrationRequested;
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
            vibrationRequested = PlayerPrefs.GetInt(Prefix + "Vibration", 1) != 0;
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
        public void ToggleVibration()
        {
            if (!VibrationAvailable) return;
            vibrationRequested = !vibrationRequested;
            PlayerPrefs.SetInt(Prefix + "Vibration", vibrationRequested ? 1 : 0); PlayerPrefs.Save();
            if (!vibrationRequested) CancelFeedback();
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
            neutralX = filteredX = ReadHorizontalGravity(); tiltLane = 1; calibrationUntil = Time.unscaledTime + .35f; nextTiltCommand = calibrationUntil;
        }
        public int ReadLaneDirection(int currentLane)
        {
            if (!TiltEnabled || sensor == null || !sensor.added || !sensor.enabled) return 0;
            filteredX = Mathf.Lerp(filteredX, ReadHorizontalGravity(), 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
            if (Time.unscaledTime < calibrationUntil) { neutralX = filteredX; return 0; }
            float tilt = filteredX - neutralX;
            // Hysteresis prevents jitter around lane boundaries; neutral returns to the middle.
            if (tilt > .2f) tiltLane = 2;
            else if (tilt < -.2f) tiltLane = 0;
            else if (Mathf.Abs(tilt) < .1f) tiltLane = 1;
            if (Time.unscaledTime < nextTiltCommand || currentLane == tiltLane) return 0;
            nextTiltCommand = Time.unscaledTime + .16f;
            return Math.Sign(tiltLane - currentLane);
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
                if (androidSdk >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = androidSdk >= 29
                        ? effects.CallStatic<AndroidJavaObject>("createPredefined", impact ? 0 : 2)
                        : effects.CallStatic<AndroidJavaObject>("createOneShot", impact ? 35L : 12L, -1))
                        vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", impact ? 35L : 12L);
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
