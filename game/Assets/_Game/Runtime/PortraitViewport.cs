using UnityEngine;

namespace Lotba.CityRush
{
    /// <summary>Keeps a portrait play area and safe touch bounds on phones and wide desktop windows.</summary>
    public sealed class PortraitViewport : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform safeArea;
        private int lastWidth;
        private int lastHeight;
        private Rect lastSafeArea;

        private void LateUpdate()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight && Screen.safeArea == lastSafeArea) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
            float screenAspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            const float targetAspect = 9f / 16f;
            Rect viewport = screenAspect > targetAspect
                ? new Rect((1f - targetAspect / screenAspect) * .5f, 0f, targetAspect / screenAspect, 1f)
                : new Rect(0f, 0f, 1f, 1f);
            worldCamera.rect = viewport;
            Rect physicalSafeArea = Screen.safeArea;
            safeArea.anchorMin = new Vector2(Mathf.Max(viewport.xMin, physicalSafeArea.xMin / Screen.width), physicalSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(Mathf.Min(viewport.xMax, physicalSafeArea.xMax / Screen.width), physicalSafeArea.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
    }
}
