using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Presentation-only countdown panel authored as a reusable uGUI prefab.</summary>
    public sealed class DecisionCountdownView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        public void Show(string message, int secondsRemaining)
        {
            if (label != null)
                label.text = $"{message}\n<size=170%>{Mathf.Max(1, secondsRemaining)}</size>";
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
