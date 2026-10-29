using UnityEngine;

namespace Lotba.Game
{
    /// <summary>One bus model, configurable line and colour bands; no material instances per bus.</summary>
    public sealed class BusSkin : MonoBehaviour
    {
        [SerializeField] private Renderer[] bodyPanels;
        [SerializeField] private Renderer[] stripePanels;
        [SerializeField] private TMPro.TMP_Text[] numbers;
        private static readonly int Colour = Shader.PropertyToID("_Color");
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        public void Apply(BusLivery livery)
        {
            var block = new MaterialPropertyBlock();
            Tint(bodyPanels, livery.BodyColour, block); Tint(stripePanels, livery.StripeColour, block);
            foreach (var number in numbers) number.text = livery.LineNumber;
        }
        private static void Tint(Renderer[] panels, Color colour, MaterialPropertyBlock block)
        {
            foreach (var panel in panels)
            {
                panel.GetPropertyBlock(block); block.SetColor(Colour, colour); block.SetColor(BaseColour, colour); panel.SetPropertyBlock(block);
            }
        }
    }
}
