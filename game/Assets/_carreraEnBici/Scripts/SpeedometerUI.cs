
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "SpeedometerUI")]
public class SpeedometerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Slider speedBar;

    [Header("Speed Settings")]
    [SerializeField] private float maxSpeed = 100f;

    public void SetSpeed(float speedKmh)
    {
        speedKmh = Mathf.Max(0f, speedKmh);

        if (speedText != null)
            speedText.text = $"{speedKmh:0} KM/H";

        if (speedBar != null)
            speedBar.value = Mathf.Clamp(speedKmh, 0f, maxSpeed);
    }
}
