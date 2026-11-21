using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Uses the full camera surface and keeps UI inside the device's physical safe area.</summary>
    public sealed class PortraitViewport : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform safeArea;
        private int lastWidth;
        private int lastHeight;
        private Rect lastSafeArea;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight && Screen.safeArea == lastSafeArea) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
            if (worldCamera != null) worldCamera.rect = new Rect(0f, 0f, 1f, 1f);
            // Art preview shares the camera framing without requiring a Canvas.
            if (safeArea == null) return;
            Rect physicalSafeArea = Screen.safeArea;
            float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            safeArea.anchorMin = new Vector2(physicalSafeArea.xMin / width, physicalSafeArea.yMin / height);
            safeArea.anchorMax = new Vector2(physicalSafeArea.xMax / width, physicalSafeArea.yMax / height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
    }
}
