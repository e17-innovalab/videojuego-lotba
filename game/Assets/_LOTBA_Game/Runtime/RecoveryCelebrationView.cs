using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lotba.Game
{
    /// <summary>
    /// Cartel y efecto festivo de celebración al capturar la gema de recuperación.
    /// Prefab editable por diseñadores en el Editor.
    /// </summary>
    public sealed class RecoveryCelebrationView : MonoBehaviour
    {
        [Header("Referencias UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform dialogBox;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Image iconImage;

        [Header("Efectos")]
        [SerializeField] private ParticleSystem celebrationParticles;

        private Coroutine activeRoutine;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (dialogBox == null) dialogBox = transform as RectTransform;
            HideImmediate();
        }

        public void HideImmediate()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }
            if (celebrationParticles != null)
            {
                celebrationParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            gameObject.SetActive(false);
        }

        public void Show(long points, int gems, float duration = 3.2f)
        {
            gameObject.SetActive(true);
            if (rewardText != null)
            {
                if (points > 0 && gems > 0)
                    rewardText.text = $"+{points:N0} PUNTOS · +{gems} GEMAS";
                else if (points > 0)
                    rewardText.text = $"+{points:N0} PUNTOS";
                else
                    rewardText.text = $"+{gems} GEMAS";
            }

            if (celebrationParticles != null)
            {
                celebrationParticles.Play();
            }

            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(AnimateCelebration(duration));
        }

        private IEnumerator AnimateCelebration(float duration)
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
            }

            if (dialogBox != null)
            {
                dialogBox.localScale = new Vector3(0.5f, 0.5f, 1f);
            }

            // Punch in
            float elapsed = 0f;
            const float popDuration = 0.35f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                // Overshoot bounce curve
                float scale = 0.5f + 0.6f * Mathf.Sin(t * Mathf.PI * 0.75f);
                if (dialogBox != null) dialogBox.localScale = new Vector3(scale, scale, 1f);
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(t * 2.5f);
                yield return null;
            }

            if (dialogBox != null) dialogBox.localScale = Vector3.one;
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // Hold display
            float holdTime = Mathf.Max(0.5f, duration - popDuration - 0.4f);
            yield return new WaitForSecondsRealtime(holdTime);

            // Fade out
            elapsed = 0f;
            const float fadeDuration = 0.4f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                if (canvasGroup != null) canvasGroup.alpha = 1f - t;
                if (dialogBox != null)
                {
                    float scale = 1f + 0.15f * t;
                    dialogBox.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }

            HideImmediate();
        }
    }
}
