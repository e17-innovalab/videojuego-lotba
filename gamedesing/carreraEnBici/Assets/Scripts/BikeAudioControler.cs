using UnityEngine;

public class BikeAudioController : MonoBehaviour
{
    public RoadManager roadManager;
    public AudioSource audioSource;

    [Header("Speed")]
    public float maxSpeed = 20f;

    [Header("Audio")]
    public float minPitch = 0.85f;
    public float maxPitch = 1.25f;

    private void Update()
    {
        float speed = roadManager.speed;

        // Stop when speed reaches zero
        if (speed <= 0.01f)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            return;
        }

        // Start sound if necessary
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        // Convert speed to 0-1
        float speed01 = Mathf.InverseLerp(0f, maxSpeed, speed);

        // Change bicycle sound according to speed
        audioSource.pitch = Mathf.Lerp(
            minPitch,
            maxPitch,
            speed01
        );
    }
}