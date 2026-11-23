using System;
using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class GameHud
    {
        private TransitionDecisionView transitionView;
        private RecoveryCelebrationView celebrationPopup;
        private RecoveryMissView missPopup;
        private RunSession recoveryViewSession;
        private string previousRecoveryOutcome;
        private float recoveryResultUntil;
        private int previousCollisions;
        private float collisionLossUntil;
        private string collisionCopy;

        public void ResetRecoveryPopups()
        {
            if (celebrationPopup != null) celebrationPopup.HideImmediate();
            if (missPopup != null) missPopup.HideImmediate();
        }
        private void RenderRecovery(RunSession session, GameSettings settings)
        {
            if (transitionView == null)
            {
                if (settings.TransitionViewPrefab == null) return;
                transitionView = Instantiate(settings.TransitionViewPrefab, speedOffer.parent, false);
                transitionView.Continue += () => { UiClicked?.Invoke(); SpeedChosen?.Invoke(false); };
                transitionView.Purchase += () => { UiClicked?.Invoke(); SpeedChosen?.Invoke(true); };
                transitionView.Recover += () => { UiClicked?.Invoke(); RecoveryChosen?.Invoke(); };
            }
            if (recoveryViewSession != session)
            {
                recoveryViewSession = session; previousRecoveryOutcome = null; recoveryResultUntil = 0f;
                previousCollisions = 0; collisionLossUntil = 0f;
            }
            bool running = session.State == RunState.Running && !SettingsOpen;
            bool canAfford = session.UpgradeCost == 0 || session.CanBuyUpgrade;
            bool showAlmost = !canAfford && session.UpgradeCost > 0;
            string continueCopy = "<b>SEGUIR</b>\n<size=75%>Continuar recorrido</size>";
            string purchaseCopy = canAfford
                ? $"<b>MULTIPLICADOR</b>\n<size=75%>Gemas rosas ×2</size>\n<color=#FF73CD>{session.UpgradeCost} gemas</color>\n<size=65%><color=#FFAAAA>Racha activa · Se pierde al chocar</color></size>"
                : $"<b>MULTIPLICADOR</b>\n<size=75%>Gemas rosas ×2</size>\n<color=#FFAAAA>{session.GemBalance}/{session.UpgradeCost} gemas</color>\n<size=65%><color=#FFAAAA>Te faltan {session.UpgradeCost - session.GemBalance}</color></size>";
            bool canRecover = session.HasRecoveryLoss && (session.PendingRecoveryPoints > 0 || session.PendingRecoveryGems > 0);
            string recoveryCopy = $"<b>RECUPERAR</b>\n<size=75%>Atajo nocturno</size>\n<color=#FFD500>+{Resources(session.PendingRecoveryPoints, session.PendingRecoveryGems)}</color>";

            transitionView.Setup(running && session.OfferPending,
                "ELEGÍ TU RITMO",
                continueCopy, purchaseCopy, canAfford, showAlmost, "¡CASI!", canRecover, recoveryCopy,
                session.UpgradeCost, session.GemBalance, session.PendingRecoveryPoints, session.PendingRecoveryGems);

            transitionView.Notice(false, null);
            if (previousRecoveryOutcome != session.RecoveryOutcome)
            {
                previousRecoveryOutcome = session.RecoveryOutcome;
                recoveryResultUntil = Time.unscaledTime + 4f;

                if (session.RecoveryOutcome == "succeeded")
                {
                    if (celebrationPopup == null && settings.RecoveryCelebrationPrefab != null)
                        celebrationPopup = Instantiate(settings.RecoveryCelebrationPrefab, speedOffer.parent, false);
                    if (celebrationPopup != null)
                        celebrationPopup.Show(session.RecoveryPointAmount, session.RecoveryAmount, 1.8f);
                }
                else if (session.RecoveryOutcome == "failed")
                {
                    if (missPopup == null && settings.RecoveryMissPrefab != null)
                        missPopup = Instantiate(settings.RecoveryMissPrefab, speedOffer.parent, false);
                    if (missPopup != null)
                        missPopup.Show("Te faltó poco... La próxima seguro lo conseguís", 1.8f);
                }
            }
            bool result = Time.unscaledTime < recoveryResultUntil &&
                (session.RecoveryOutcome == "succeeded" || session.RecoveryOutcome == "failed");
            if (previousCollisions != session.Collisions)
            {
                previousCollisions = session.Collisions; collisionLossUntil = Time.unscaledTime + 2.5f;
                collisionCopy = "Perdiste " + Resources(session.LastCollisionLoss.Points, session.LastCollisionLoss.Gems);
            }
            bool showLoss = Time.unscaledTime < collisionLossUntil && session.LastCollisionLoss.Any;
            string status = session.RecoveryActive ? "RECUPERO · Esquivá obstáculos y agarrá la gema dorada al final"
                : session.RecoveryOutcome == "succeeded" ? "¡Gema dorada capturada! Recuperaste lo perdido"
                : session.RecoveryOutcome == "failed" ? "No alcanzaste la gema dorada. Podés seguir o frenar."
                : null;
            if (showLoss && !result) status = collisionCopy;
            transitionView.Status(running && !session.OfferPending && (session.RecoveryActive || result || showLoss), status);
        }
        private static string Resources(long points, int gems) => points > 0 && gems > 0 ? $"{points} puntos y {gems} gemas"
            : points > 0 ? $"{points} puntos" : $"{gems} gemas";
    }
}
