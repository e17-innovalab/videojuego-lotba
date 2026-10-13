
using UnityEngine;

public class RewardPickup : MonoBehaviour
{
    private RewardManager rewardManager;
    private Transform bicycleRoot;
    private bool collected;

    public void Initialize(
        RewardManager manager,
        Transform bicycle)
    {
        rewardManager = manager;
        bicycleRoot = bicycle;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || bicycleRoot == null)
            return;

        if (other.transform != bicycleRoot &&
            !other.transform.IsChildOf(bicycleRoot))
            return;

        collected = true;

        if (rewardManager != null)
            rewardManager.CollectReward();

        Destroy(gameObject);
    }
}
