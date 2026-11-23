using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lotba.Game
{
    /// <summary>
    /// Decoupled transition presentation. Displays the decision cards styled after
    /// the brand's 'Tarjetas de velocidad' (Elegí tu ritmo) with 2 or 3 responsive options:
    /// - Seguir (Mantener ritmo)
    /// - Mejora (Multiplicador de gemas rosas)
    /// - Recuperar (Atajo con niebla, solo si hubo pérdidas)
    /// </summary>
    public sealed class TransitionDecisionView : MonoBehaviour
    {
        [Header("Containers")]
        [SerializeField] private GameObject decision, notice, status;

        [Header("General Copy")]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text noticeLabel, statusLabel;

        [Header("Card 1: Seguir")]
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueTitle;
        [SerializeField] private TMP_Text continuePrice;

        [Header("Card 2: Mejora (Gemas Rosas / Multiplicador)")]
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TMP_Text purchaseTitle;
        [SerializeField] private TMP_Text purchasePrice;
        [SerializeField] private GameObject almostBadge;
        [SerializeField] private TMP_Text almostBadgeLabel;
        [SerializeField] private GameObject shortfallBadge;
        [SerializeField] private TMP_Text shortfallLabel;

        [Header("Card 3: Recuperar (Solo si hubo pérdidas)")]
        [SerializeField] private Button recoveryButton;
        [SerializeField] private TMP_Text recoveryTitle;
        [SerializeField] private TMP_Text recoveryPrice;

        public event Action Continue, Purchase, Recover;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => Continue?.Invoke());
            if (purchaseButton != null) purchaseButton.onClick.AddListener(() => Purchase?.Invoke());
            if (recoveryButton != null) recoveryButton.onClick.AddListener(() => Recover?.Invoke());
        }

        public void Setup(
            bool visible,
            string headerTitle,
            string continueCopy,
            string purchaseCopy,
            bool canAffordPurchase,
            bool showAlmost,
            string almostCopy,
            bool canRecover,
            string recoveryCopy,
            int purchaseCost = 0,
            int playerGems = 0,
            long recoveryPoints = 0,
            int recoveryGems = 0)
        {
            if (decision != null) decision.SetActive(visible);
            if (!visible) return;

            if (title != null && !string.IsNullOrEmpty(headerTitle))
                title.text = headerTitle;

            if (description != null)
                description.gameObject.SetActive(false);

            var rt1 = continueButton != null ? continueButton.transform as RectTransform : null;
            var rt2 = purchaseButton != null ? purchaseButton.transform as RectTransform : null;
            var rt3 = recoveryButton != null ? recoveryButton.transform as RectTransform : null;

            if (canRecover && rt3 != null)
            {
                if (rt1 != null) { rt1.anchorMin = new Vector2(0.03f, 0.06f); rt1.anchorMax = new Vector2(0.33f, 0.80f); }
                if (rt2 != null) { rt2.anchorMin = new Vector2(0.35f, 0.06f); rt2.anchorMax = new Vector2(0.65f, 0.80f); }
                rt3.anchorMin = new Vector2(0.67f, 0.06f); rt3.anchorMax = new Vector2(0.97f, 0.80f);
            }
            else
            {
                if (rt1 != null) { rt1.anchorMin = new Vector2(0.04f, 0.06f); rt1.anchorMax = new Vector2(0.48f, 0.80f); }
                if (rt2 != null) { rt2.anchorMin = new Vector2(0.52f, 0.06f); rt2.anchorMax = new Vector2(0.96f, 0.80f); }
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                if (continueTitle != null) continueTitle.text = "SEGUIR";
                if (continuePrice != null) continuePrice.text = "GRATIS";
            }

            if (purchaseButton != null)
            {
                purchaseButton.gameObject.SetActive(true);
                purchaseButton.interactable = canAffordPurchase;
                if (purchaseTitle != null) purchaseTitle.text = "MULTIPLICADOR";
                if (purchasePrice != null) purchasePrice.text = purchaseCost > 0 ? purchaseCost.ToString() : "0";

                if (almostBadge != null)
                {
                    almostBadge.SetActive(showAlmost);
                    if (showAlmost && almostBadgeLabel != null && !string.IsNullOrEmpty(almostCopy))
                        almostBadgeLabel.text = almostCopy;
                }

                if (shortfallBadge != null)
                {
                    shortfallBadge.SetActive(showAlmost);
                    if (showAlmost && shortfallLabel != null)
                        shortfallLabel.text = $"Te faltan {Math.Max(0, purchaseCost - playerGems)}";
                }
            }

            if (recoveryButton != null)
            {
                recoveryButton.gameObject.SetActive(canRecover);
                if (canRecover)
                {
                    if (recoveryTitle != null) recoveryTitle.text = "RECUPERAR";
                    if (recoveryPrice != null)
                    {
                        if (recoveryPoints > 0 && recoveryGems > 0)
                            recoveryPrice.text = $"+{recoveryPoints} pts · +{recoveryGems}";
                        else if (recoveryPoints > 0)
                            recoveryPrice.text = $"+{recoveryPoints} pts";
                        else
                            recoveryPrice.text = $"+{recoveryGems} gemas";
                    }
                }
            }
        }

        public void Notice(bool visible, string text)
        {
            if (notice != null) notice.SetActive(visible);
            if (visible && noticeLabel != null) noticeLabel.text = text;
        }

        public void Status(bool visible, string text)
        {
            if (status != null) status.SetActive(visible);
            if (visible && statusLabel != null) statusLabel.text = text;
        }
    }
}
