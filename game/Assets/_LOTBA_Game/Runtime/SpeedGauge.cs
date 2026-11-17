using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Drives the authored dial without owning its artwork or layout.</summary>
    public sealed class SpeedGauge : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text speedLabel;
        [SerializeField] private TMPro.TMP_Text speedUnits;
        [SerializeField] private RectTransform needlePivot;
        [SerializeField] private float minimumNeedleAngle = 180f;
        [SerializeField] private float maximumNeedleAngle;

        private const float OverloadKmh = 100f;
        private const float ArcadeSpeedScale = 1.5f;
        private Color normalLabelColor;
        private Color normalUnitsColor;

        private void Awake()
        {
            normalLabelColor = speedLabel.color;
            normalUnitsColor = speedUnits.color;
        }

        public void SetSpeed(float speed, float initialSpeed, float purchasedSpeed, float elapsed,
            LotbaBrandSettings brand, bool animate)
        {
            // Compress the simulated pace for the HUD while showing the full bonus from each purchase.
            float upgrade = Mathf.Min(speed, purchasedSpeed);
            float kmh = (speed - upgrade) * ArcadeSpeedScale + upgrade * 3.6f;
            bool overloaded = kmh > OverloadKmh;
            speedLabel.SetText("{0:0}", kmh);
            speedLabel.color = overloaded ? brand.Red : normalLabelColor;
            speedUnits.color = overloaded ? brand.Red : normalUnitsColor;

            float progress = Mathf.InverseLerp((initialSpeed - 3f) * ArcadeSpeedScale, OverloadKmh, kmh);
            progress *= .94f;
            if (overloaded) progress += .04f * Mathf.InverseLerp(OverloadKmh, OverloadKmh + 10f, kmh);
            float needleAngle = Mathf.Lerp(minimumNeedleAngle, maximumNeedleAngle, progress);
            if (overloaded && animate) needleAngle += Mathf.Sin(elapsed * 47f) * 2.5f;
            needlePivot.localRotation = Quaternion.Euler(0f, 0f, needleAngle);
        }
    }
}
