using UnityEngine;
using TMPro;

namespace Lotba.Game
{
    /// <summary>Optional invitation on an authored world marker. No game-rule dependencies.</summary>
    public sealed class ChoiceMarkerView : MonoBehaviour
    {
        [SerializeField] private GameObject invitation;
        [SerializeField] private TMP_Text welcomeLabel;
        [SerializeField] private Renderer welcomeBoard;

        public void SetWelcomeZone(string zoneName, Material boardMaterial)
        {
            if (welcomeLabel != null)
                welcomeLabel.text = $"BIENVENIDOS A\n{zoneName}";
            if (welcomeBoard != null && boardMaterial != null)
                welcomeBoard.sharedMaterial = boardMaterial;
        }

        public void ShowInvitation(bool visible)
        {
            if (invitation != null) invitation.SetActive(visible);
        }
    }
}
