using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Presentation-only banner shown briefly when the recovery challenge begins.</summary>
    public sealed class RecoveryAnnouncementView : MonoBehaviour
    {
        public void Show()
        {
            if (gameObject.activeSelf) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
        public void Hide() => gameObject.SetActive(false);
    }
}
