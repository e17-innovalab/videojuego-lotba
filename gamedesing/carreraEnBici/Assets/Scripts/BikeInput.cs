using UnityEngine;

public class BikeInput : MonoBehaviour
{
    [Header("Keyboard Test")]
    public bool useKeyboard = true;

    [Header("Accelerometer")]
    public float tiltThreshold = 0.3f;
    public float neutralThreshold = 0.1f;

    private bool readyForTilt = true;

    public int GetLaneDirection()
    {
        // -------------------------
        // KEYBOARD TEST
        // -------------------------

        if (useKeyboard)
        {
            if (Input.GetKeyDown(KeyCode.A))
                return -1;

            if (Input.GetKeyDown(KeyCode.D))
                return 1;
        }

        // -------------------------
        // ACCELEROMETER
        // -------------------------

        float tilt = Input.acceleration.x;

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