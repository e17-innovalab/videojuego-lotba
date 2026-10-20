
using UnityEngine;
using TMPro;

public class RewardManager : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI rewardText;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip rewardSFX;

    [Header("Speed Reward")]
    [SerializeField] private RoadManager roadManager;
    [SerializeField] private float speedIncrease = 1f;
    [SerializeField] private float maximumSpeed = 25f;

    private int rewardCount = 0;

    private void Start()
    {
        UpdateCounter();
    }

    public void CollectReward()
    {
        rewardCount++;

        UpdateCounter();

        if (sfxSource != null && rewardSFX != null)
            sfxSource.PlayOneShot(rewardSFX);

        if (roadManager != null)
            roadManager.IncreaseSpeed(speedIncrease, maximumSpeed);

        Debug.Log("Rewards collected: " + rewardCount);
    }

    private void UpdateCounter()
    {
        if (rewardText != null)
            rewardText.text = "REWARDS: " + rewardCount;
    }
}
