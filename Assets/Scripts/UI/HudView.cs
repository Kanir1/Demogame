using DemoCoinMaster.Core;
using TMPro;
using UnityEngine;

namespace DemoCoinMaster.UI
{
    /// <summary>
    /// Shows coins, energy and shields. Pure view: it only reads PlayerState and redraws
    /// when OnBalanceChanged fires. No polling in Update.
    /// The energy refill countdown is added on Day 2 (EnergyTimer).
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text shieldsText;

        private PlayerState playerState;

        private void OnEnable()
        {
            playerState = bootstrap != null ? bootstrap.PlayerState : null;
            if (playerState == null)
            {
                Debug.LogError("[HudView] GameBootstrap is not assigned or failed to start.", this);
                return;
            }

            playerState.OnBalanceChanged += Refresh;

            // If the first sync already happened before this view was enabled, draw it now.
            if (playerState.HasSynced) Refresh(playerState);
            else SetLoading();
        }

        private void OnDisable()
        {
            if (playerState != null) playerState.OnBalanceChanged -= Refresh;
        }

        private void Refresh(PlayerState state)
        {
            // Events fire only when balances change, so this string formatting is not per-frame.
            coinsText.text = $"Coins {state.Coins:N0}";
            energyText.text = $"Energy {state.Energy}/{bootstrap.Config.MaxEnergy}";
            shieldsText.text = $"Shields {state.Shields}/{bootstrap.Config.MaxShields}";
        }

        private void SetLoading()
        {
            coinsText.text = "Coins ...";
            energyText.text = "Energy ...";
            shieldsText.text = "Shields ...";
        }
    }
}
