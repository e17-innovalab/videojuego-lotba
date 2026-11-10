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

        public void SetSpeed(float speed, float maximumSpeed)
        {
            speedLabel.SetText("{0:0}", speed * 3.6f);
            float progress = Mathf.InverseLerp(8f, maximumSpeed, speed);
            activeArc.fillAmount = progress;
            needlePivot.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Lerp(minimumNeedleAngle, maximumNeedleAngle, progress));
        }
    }
}
