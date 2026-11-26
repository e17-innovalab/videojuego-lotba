using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>
    /// Decoupled world-space portal presentation for route forks and recovery gates.
    /// Has no knowledge of session rules, game controller or currency accounting.
    /// </summary>
    public sealed class RecoveryPortalView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
