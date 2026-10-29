
using System.Collections.Generic;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "SpawnManager")]
public class SpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoadManager roadManager;
    [SerializeField] private Transform bicycleRoot;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private ScreenShake screenShake;
    [SerializeField] private ScreenColor screenColor;
    [SerializeField] private AudioManager audioManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField] private GameObject rewardPrefab;

    [Header("Lane Positions - World X")]
    [SerializeField] private float[] lanePositions = { -3f, 0f, 3f };

    [Header("Spawn Settings")]
    [SerializeField] private float spawnZ = -80f;
    [SerializeField] private float despawnZ = 100f;
    [SerializeField] private float initialInterval = 3f;
    [SerializeField] private float minimumInterval = 1.5f;
    [SerializeField] private float difficultyRampSeconds = 60f;

    private readonly List<Transform> spawnedObjects = new();

    private float spawnTimer;
    private float elapsedTime;

    private void Update()
    {
        if (roadManager == null)
            return;

        elapsedTime += Time.deltaTime;
        spawnTimer += Time.deltaTime;

        MoveSpawnedObjects();

        float interval = Mathf.Lerp(
            initialInterval,
            minimumInterval,
            Mathf.Clamp01(elapsedTime / difficultyRampSeconds)
        );

        if (spawnTimer >= interval)
        {
            spawnTimer = 0f;
            SpawnWave();
        }
    }

    private void SpawnWave()
    {
        if (lanePositions == null || lanePositions.Length < 2)
            return;

        // Start with one object. After 30 seconds, use two.
        int count = elapsedTime >= 30f ? 2 : 1;
        count = Mathf.Min(count, lanePositions.Length - 1);

        // Choose different lanes for this wave.
        List<int> availableLanes = new();

        for (int i = 0; i < lanePositions.Length; i++)
            availableLanes.Add(i);

        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(0, availableLanes.Count);
            int laneIndex = availableLanes[pick];
            availableLanes.RemoveAt(pick);

            SpawnObject(lanePositions[laneIndex]);
        }
    }

    private void SpawnObject(float laneX)
    {
        GameObject prefab = ChoosePrefab();

        if (prefab == null)
            return;

        Vector3 position = new Vector3(laneX,transform.position.y,spawnZ);

        GameObject instance = Instantiate(
            prefab, position, prefab.transform.rotation, transform
        );

        spawnedObjects.Add(instance.transform);

        // Supply scene references to scripts on spawned prefabs.
        RewardPickup pickup = instance.GetComponentInChildren<RewardPickup>();

        if (pickup != null)
            pickup.Initialize(rewardManager, bicycleRoot);

        CamionController truck =
            instance.GetComponentInChildren<CamionController>();

        if (truck != null)
        {
            truck.Initialize(
                bicycleRoot, screenShake, screenColor, audioManager
            );
        }
    }

    private GameObject ChoosePrefab()
    {
        // Give rewards a 35% chance to appear.
        if (rewardPrefab != null && Random.value < 0.35f)
            return rewardPrefab;

        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
            return rewardPrefab;

        return obstaclePrefabs[
            Random.Range(0, obstaclePrefabs.Length)
        ];
    }

    private void MoveSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            Transform obj = spawnedObjects[i];

            if (obj == null)
            {
                spawnedObjects.RemoveAt(i);
                continue;
            }

            obj.position += Vector3.back
                * roadManager.speed * Time.deltaTime;

            if (obj.position.z < despawnZ)
            {
                Destroy(obj.gameObject);
                spawnedObjects.RemoveAt(i);
            }
        }
    }

    internal void StopSpawning()
    {
        throw new System.NotImplementedException();
    }
}
    