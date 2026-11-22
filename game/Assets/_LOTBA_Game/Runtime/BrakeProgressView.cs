using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Prefab bindings only. Receives visibility/progress without knowing gameplay or input.</summary>
    public sealed class BrakeProgressView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image fill;
        public void Render(bool visible, float progress)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (fill != null)
            {
                // Rect width works with an untextured Image too; Image.fillAmount requires a sprite.
                Vector2 maximum = fill.rectTransform.anchorMax;
                maximum.x = Mathf.Clamp01(progress);
                fill.rectTransform.anchorMax = maximum;
            }
        }
    }
}
