using DemoCoinMaster.Core;
using TMPro;
using UnityEngine;

namespace DemoCoinMaster.UI
{
    /// <summary>
    /// Shows coins, energy, shields and the energy refill countdown. Pure view: it redraws only
    /// when PlayerState.OnBalanceChanged or EnergyTimer.OnCountdownChanged fires. No polling in Update.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text shieldsText;

        [Header("Energy refill countdown (optional)")]
        [SerializeField] private EnergyTimer energyTimer;
        [SerializeField] private TMP_Text energyTimerText;

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

            if (energyTimer != null && energyTimerText != null)
            {
                energyTimer.OnCountdownChanged += RefreshCountdown;
                RefreshCountdown(energyTimer.SecondsUntilNextRefill);
            }
        }

        private void OnDisable()
        {
            if (playerState != null) playerState.OnBalanceChanged -= Refresh;
            if (energyTimer != null) energyTimer.OnCountdownChanged -= RefreshCountdown;
        }

        private void Refresh(PlayerState state)
        {
            // Events fire only when balances change, so this string formatting is not per-frame.
            coinsText.text = $"Coins {state.Coins:N0}";
            energyText.text = $"Energy {state.Energy}/{bootstrap.Config.MaxEnergy}";
            shieldsText.text = $"Shields {state.Shields}/{bootstrap.Config.MaxShields}";
        }

        private void RefreshCountdown(int seconds)
        {
            // Fires at most once per second (EnergyTimer only raises it when the number changes).
            energyTimerText.text = seconds == EnergyTimer.NotRefilling
                ? "Full"
                : $"+1 in {seconds / 60}:{seconds % 60:00}";
        }

        private void SetLoading()
        {
            coinsText.text = "Coins ...";
            energyText.text = "Energy ...";
            shieldsText.text = "Shields ...";
        }
    }
}
