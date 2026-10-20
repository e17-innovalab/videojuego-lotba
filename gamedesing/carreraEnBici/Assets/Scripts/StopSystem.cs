
using UnityEngine;

public class StopSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoadManager roadManager;
    [SerializeField] private BikeController bikeController;
    [SerializeField] private BikeInput bikeInput;
    [SerializeField] private SpawnManager spawnManager;

    public bool IsStopped { get; private set; }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            StopGame();
        }
    }

    public void StopGame()
    {
        if (IsStopped)
            return;

        IsStopped = true;

        if (roadManager != null)
            roadManager.StopRunning();

        if (bikeController != null)
            bikeController.enabled = false;

        if (bikeInput != null)
            bikeInput.enabled = false;

        if(spawnManager != null)
            spawnManager.StopSpawning();    

        Debug.Log("STOP SYSTEM: Game stopped.");
    }

    void StopSpawning()
    {
        if (spawnManager != null)
        {
            spawnManager.enabled = false;
            Debug.Log("STOP SYSTEM: Spawning stopped.");
        }
    }
}
