
using UnityEngine;
using System.Collections;

public class RoadManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private SpeedometerUI speedometer;

    [Header("Road")]
    public float speed = 10f;
    public float tileLength = 30f;
    public Transform[] tiles;

    [Header("Impact Slowdown")]
    public float normalSpeed = 15f;
    public float impactSpeed = 10f;
    public float recoveryTime = 0.4f;

    private AudioManager audioManager;

    private void Awake()
    {
        audioManager = FindFirstObjectByType<AudioManager>();
    }

    private void Update()
    {
        // Update the speedometer continuously.
        if (speedometer != null)
            speedometer.SetSpeed(speed * 0.10f);

        foreach (Transform tile in tiles)
        {
            tile.Translate(Vector3.back * speed * Time.deltaTime);

            if (tile.position.z < -30f)
                MoveToFront(tile);
        }
    }

    private void MoveToFront(Transform tile)
    {
        float furthestZ = float.MinValue;

        foreach (Transform other in tiles)
        {
            if (other.position.z > furthestZ)
                furthestZ = other.position.z;
        }

        tile.position = new Vector3(
            tile.position.x,
            tile.position.y,
            furthestZ + tileLength
        );
    }

    public void StartRunning()
    {
        speed = normalSpeed;
        Debug.Log("ROAD: RUNNING");
    }

    public void PlayerHit()
    {
        StopAllCoroutines();
        StartCoroutine(ImpactSlowdown());
    }

    private IEnumerator ImpactSlowdown()
    {
        speed = impactSpeed;

        if (audioManager != null)
            audioManager.ImpactPitch();

        yield return new WaitForSeconds(0.1f);

        float elapsed = 0f;

        while (elapsed < recoveryTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f, 1f, elapsed / recoveryTime
            );

            speed = Mathf.Lerp(impactSpeed, normalSpeed, t);

            if (audioManager != null)
                audioManager.SetPitch(t);

            yield return null;
        }

        speed = normalSpeed;

        if (audioManager != null)
            audioManager.SetNormalPitch();
    }

    public void IncreaseSpeed(float amount, float maximum)
    {
        normalSpeed = Mathf.Min(normalSpeed + amount, maximum);
        speed = Mathf.Min(speed + amount, normalSpeed);

        Debug.Log("Road speed increased to: " + speed);
    }

}
