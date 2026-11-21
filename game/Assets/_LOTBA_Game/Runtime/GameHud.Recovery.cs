using System;
using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class GameHud
    {
        public event Action<bool> RecoveryChosen;
        private RectTransform recoveryCard, recoveryStatus;
        private TMP_Text recoveryDescription, recoveryStatusText;
        private RunSession recoveryViewSession;
        private string previousRecoveryOutcome;
        private float recoveryResultUntil;

        private void RenderRecovery(RunSession session, GameSettings settings)
        {
            if (recoveryCard == null) BuildRecoveryView(settings);
            if (recoveryViewSession != session)
            {
                recoveryViewSession = session;
                previousRecoveryOutcome = null;
                recoveryResultUntil = 0f;
            }
            bool running = session.State == RunState.Running && !SettingsOpen;
            recoveryCard.gameObject.SetActive(running && session.RecoveryOfferPending);
            recoveryDescription.text = $"Podés recuperar {session.RecoveryAmount} gemas.\n\n" +
                $"Vas {RunSession.RecoverySpeed * 3.6f:0.#} km/h más rápido durante {RunSession.RecoverySeconds:0} segundos.\n" +
                "Si chocás, no recuperás y podés perder más gemas. Podés frenar cuando quieras.";
            if (previousRecoveryOutcome != session.RecoveryOutcome)
            {
                previousRecoveryOutcome = session.RecoveryOutcome;
                recoveryResultUntil = Time.unscaledTime + 4f;
            }
            bool result = Time.unscaledTime < recoveryResultUntil &&
                (session.RecoveryOutcome == "succeeded" || session.RecoveryOutcome == "failed");
            recoveryStatus.gameObject.SetActive(running && !session.OfferPending && (session.RecoveryActive || result));
            recoveryStatusText.text = session.RecoveryActive
                ? $"RECUPERO · {Mathf.CeilToInt(session.RecoveryTimeLeft)} s\nEvitá chocar para recuperar {session.RecoveryAmount} gemas"
                : session.RecoveryOutcome == "succeeded" ? $"Recuperaste {session.RecoveryAmount} gemas"
                : "Chocaste. No recuperaste las gemas.";
            recoveryStatusText.color = session.RecoveryOutcome == "failed" && !session.RecoveryActive
                ? settings.Brand.White : settings.Brand.Green;
        }

        private void BuildRecoveryView(GameSettings settings)
        {
            recoveryCard = RecoveryRect("RecoveryOffer", speedOffer.parent, new Vector2(.05f, .32f), new Vector2(.95f, .70f));
            var background = recoveryCard.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color32(12, 33, 50, 255);
            var outline = recoveryCard.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = settings.Brand.Green; outline.effectDistance = new Vector2(2f, -2f);
            RecoveryText("Title", recoveryCard, "¿INTENTAR RECUPERAR?", 29, new Vector2(.04f, .80f), new Vector2(.96f, .96f));
            recoveryDescription = RecoveryText("Description", recoveryCard, "", 23, new Vector2(.05f, .29f), new Vector2(.95f, .79f));
            RecoveryButton(recoveryCard, "Seguir", false, settings.Brand.White, new Vector2(.05f, .05f), new Vector2(.47f, .24f));
            RecoveryButton(recoveryCard, "Intentar", true, settings.Brand.Green, new Vector2(.53f, .05f), new Vector2(.95f, .24f));
            recoveryStatus = RecoveryRect("RecoveryStatus", speedOffer.parent, new Vector2(.09f, .20f), new Vector2(.91f, .29f));
            var statusBackground = recoveryStatus.gameObject.AddComponent<UnityEngine.UI.Image>();
            statusBackground.color = new Color32(12, 33, 50, 240); statusBackground.raycastTarget = false;
            recoveryStatusText = RecoveryText("Status", recoveryStatus, "", 22, new Vector2(.03f, .05f), new Vector2(.97f, .95f));
            ReadablePanelWidth.Wrap(recoveryCard);
            ReadablePanelWidth.Wrap(recoveryStatus);
            recoveryCard.gameObject.SetActive(false); recoveryStatus.gameObject.SetActive(false);
        }

        private void RecoveryButton(Transform parent, string title, bool accept, Color color, Vector2 min, Vector2 max)
        {
            var rect = RecoveryRect(accept ? "AcceptRecovery" : "DeclineRecovery", parent, min, max);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = RecoveryText("Label", rect, title, 26, Vector2.zero, Vector2.one);
            label.color = new Color32(12, 33, 50, 255);
            button.onClick.AddListener(() => { UiClicked?.Invoke(); RecoveryChosen?.Invoke(accept); });
        }

        private TMP_Text RecoveryText(string name, Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            var rect = RecoveryRect(name, parent, min, max);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = score.font; label.fontSize = size; label.enableAutoSizing = true;
            label.fontSizeMin = size - 3; label.fontSizeMax = size;
            label.text = value; label.color = Color.white; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static RectTransform RecoveryRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
