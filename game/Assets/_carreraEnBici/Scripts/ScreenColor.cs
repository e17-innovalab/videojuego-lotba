
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ScreenColor")]
public class ScreenColor : MonoBehaviour
{
    [SerializeField] private Image overlay;
    [SerializeField] private float flashDuration = 0.3f;
    [SerializeField, Range(0f, 1f)]
    private float maxAlpha = 0.35f;

    private Coroutine flashRoutine;

    private void Awake()
    {
        if (overlay != null)
        {
            Color color = Color.red;
            color.a = 0f;
            overlay.color = color;
        }
    }

    public void Flash()
    {
        if (overlay == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            float t = elapsed / flashDuration;
            float alpha = Mathf.Lerp(maxAlpha, 0f, t);

            Color color = Color.red;
            color.a = alpha;
            overlay.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Color finalColor = Color.red;
        finalColor.a = 0f;
        overlay.color = finalColor;

        flashRoutine = null;
    }
}
