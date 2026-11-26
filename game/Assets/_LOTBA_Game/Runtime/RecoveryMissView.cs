using System.Collections;
using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Cartel de "¡CASI!" cuando el jugador no logra capturar la gema de recuperación.
    /// Prefab editable por diseñadores en el Editor.
    /// </summary>
    public sealed class RecoveryMissView : MonoBehaviour
    {
        [Header("Referencias UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform dialogBox;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

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
            gameObject.SetActive(false);
        }

        public void Show(string customMessage = null, float duration = 3.2f)
        {
            gameObject.SetActive(true);
            if (subtitleText != null && !string.IsNullOrEmpty(customMessage))
            {
                subtitleText.text = customMessage;
            }

            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(AnimateMiss(duration));
        }

        private IEnumerator AnimateMiss(float duration)
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
            }

            if (dialogBox != null)
            {
                dialogBox.localScale = new Vector3(0.6f, 0.6f, 1f);
            }

            // Punch in
            float elapsed = 0f;
            const float popDuration = 0.3f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float scale = 0.6f + 0.45f * Mathf.Sin(t * Mathf.PI * 0.75f);
                if (dialogBox != null) dialogBox.localScale = new Vector3(scale, scale, 1f);
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(t * 3f);
                yield return null;
            }

            if (dialogBox != null) dialogBox.localScale = Vector3.one;
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // Hold
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
                yield return null;
            }

            HideImmediate();
        }
    }
}
