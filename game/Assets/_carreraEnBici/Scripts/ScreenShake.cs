
using System.Collections;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ScreenShake")]
public class ScreenShake : MonoBehaviour
{
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private float magnitude = 0.15f;

    private Vector3 originalLocalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;
    }

    public void Shake()
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 offset = Random.insideUnitSphere * magnitude;
            transform.localPosition = originalLocalPosition + offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        shakeRoutine = null;
    }
}
