using UnityEngine;
using UnityEngine.EventSystems;

namespace Lotba.Game
{
    /// <summary>Generic pointer hold signal, independent of the action controlled by the button.</summary>
    [DisallowMultipleComponent]
    public sealed class HeldButtonInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private int pointerId;
        public bool IsHeld { get; private set; }
        public void OnPointerDown(PointerEventData data)
        {
            if (IsHeld || data.button != PointerEventData.InputButton.Left) return;
            pointerId = data.pointerId;
            IsHeld = true;
        }
        public void OnPointerUp(PointerEventData data) { if (data.pointerId == pointerId) Clear(); }
        public void OnPointerExit(PointerEventData data) { if (data.pointerId == pointerId) Clear(); }
        public void Clear() => IsHeld = false;
        private void OnDisable() => Clear();
        private void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
        private void OnApplicationPause(bool paused) { if (paused) Clear(); }
    }
}
