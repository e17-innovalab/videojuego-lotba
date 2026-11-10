using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Drives the authored dial without owning its artwork or layout.</summary>
    public sealed class SpeedGauge : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text speedLabel;
        [SerializeField] private UnityEngine.UI.Image activeArc;
        [SerializeField] private RectTransform needlePivot;
        [SerializeField] private float minimumNeedleAngle = 180f;
        [SerializeField] private float maximumNeedleAngle;

        // The arcade simulation runs faster than the speed shown to the rider.
        public static float DisplaySpeed(float simulationSpeed) => simulationSpeed * 1.5f;

        public void SetSpeed(float speed, float initialSpeed, float maximumSpeed)
        {
            speedLabel.SetText("{0:0}", DisplaySpeed(speed));
            float progress = Mathf.InverseLerp(initialSpeed - 3f, maximumSpeed + RunSession.BoostSpeed, speed);
            activeArc.fillAmount = progress;
            needlePivot.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Lerp(minimumNeedleAngle, maximumNeedleAngle, progress));
        }
    }
}
