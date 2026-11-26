using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Optional visual switch for the tutorial capybara's helmet.</summary>
    public sealed class CapybaraHelmetView : MonoBehaviour
    {
        [SerializeField] private GameObject helmetRoot;

        public bool IsVisible => helmetRoot != null && helmetRoot.activeSelf;

        public void SetVisible(bool visible)
        {
            if (helmetRoot != null) helmetRoot.SetActive(visible);
        }
    }
}
