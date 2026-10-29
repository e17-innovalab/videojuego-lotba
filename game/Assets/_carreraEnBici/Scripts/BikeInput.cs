using UnityEngine;
using UnityEngine.InputSystem;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "BikeInput")]
public class BikeInput : MonoBehaviour
{
    [Header("Keyboard Test")]
    public bool useKeyboard = true;

    [Header("Accelerometer")]
    public float tiltThreshold = 0.3f;
    public float neutralThreshold = 0.1f;

    private bool readyForTilt = true;
    private Accelerometer sensor;
    private bool enabledSensor;

    private void OnEnable()
    {
        readyForTilt = true;
        sensor = Application.isMobilePlatform ? Accelerometer.current : null;
        if (sensor == null || sensor.enabled)
            return;

        InputSystem.EnableDevice(sensor);
        enabledSensor = true;
    }

    private void OnDisable()
    {
        // Release only the sensor this component enabled.
        if (enabledSensor && sensor != null && sensor.added)
            InputSystem.DisableDevice(sensor);

        enabledSensor = false;
        sensor = null;
    }

    public int GetLaneDirection()
    {
        // -------------------------
        // KEYBOARD TEST
        // -------------------------

        if (useKeyboard)
        {
            if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
                return -1;

            if (Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame)
                return 1;
        }

        // -------------------------
        // ACCELEROMETER
        // -------------------------

        if (sensor == null || !sensor.enabled)
            return 0;

        // Use gravity direction so device-specific acceleration units do not affect thresholds.
        Vector3 acceleration = sensor.acceleration.ReadValue();
        float tilt = acceleration.sqrMagnitude > 0.001f ? acceleration.normalized.x : 0f;

        if (readyForTilt)
        {
            if (tilt < -tiltThreshold)
            {
                readyForTilt = false;
                return -1;
            }

            if (tilt > tiltThreshold)
            {
                readyForTilt = false;
                return 1;
            }
        }

        // Return to neutral
        if (Mathf.Abs(tilt) < neutralThreshold)
        {
            readyForTilt = true;
        }

        return 0;
    }
}
