using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Optional invitation on an authored world marker. No game-rule dependencies.</summary>
    public sealed class ChoiceMarkerView : MonoBehaviour
    {
        [SerializeField] private GameObject invitation;
        public void ShowInvitation(bool visible)
        {
            if (invitation != null) invitation.SetActive(visible);
        }
    }
}
