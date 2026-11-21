using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Constrains card content independently from the full-width camera, HUD and shades.</summary>
    [DisallowMultipleComponent]
    public sealed class ReadablePanelWidth : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float maximumWidthToHeight = 9f / 16f;
        private bool applying;

        public static void Wrap(RectTransform content)
        {
            if (content == null || content.parent == null ||
                content.parent.GetComponent<ReadablePanelWidth>() != null) return;
            int index = content.GetSiblingIndex();
            var wrapper = (RectTransform)new GameObject("Readable " + content.name, typeof(RectTransform)).transform;
            wrapper.gameObject.layer = content.gameObject.layer;
            wrapper.SetParent(content.parent, false);
            wrapper.SetSiblingIndex(index);
            // Preserve the existing anchors/offsets inside a narrower, full-height parent.
            content.SetParent(wrapper, false);
            wrapper.gameObject.AddComponent<ReadablePanelWidth>().Refresh();
        }

        private void OnEnable() => Refresh();
        private void OnRectTransformDimensionsChange() => Refresh();

        private void Refresh()
        {
            if (applying) return;
            var rect = transform as RectTransform;
            var parent = rect != null ? rect.parent as RectTransform : null;
            if (parent == null) return;
            applying = true;
            rect.anchorMin = new Vector2(.5f, 0f);
            rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Mathf.Min(parent.rect.width,
                parent.rect.height * maximumWidthToHeight), 0f);
            applying = false;
        }
    }
}
