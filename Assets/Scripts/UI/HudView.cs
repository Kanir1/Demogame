using SpinVillage.Core;
using TMPro;
using UnityEngine;

namespace SpinVillage.UI
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

        [Header("Optional: wait for reels before showing new balances")]
        [SerializeField] private ReelView reelView;
        [Tooltip("Optional. If set, the counters update when the flying coins land.")]
        [SerializeField] private CoinFlyPool coinFlyPool;

        private PlayerState playerState;
        private bool refreshPending;

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

            if (reelView != null) reelView.OnPresentationEnded += HandlePresentationEnded;
            if (coinFlyPool != null) coinFlyPool.OnAllCoinsLanded += HandlePresentationEnded;
        }

        private void OnDisable()
        {
            if (playerState != null) playerState.OnBalanceChanged -= Refresh;
            if (energyTimer != null) energyTimer.OnCountdownChanged -= RefreshCountdown;
            if (reelView != null) reelView.OnPresentationEnded -= HandlePresentationEnded;
            if (coinFlyPool != null) coinFlyPool.OnAllCoinsLanded -= HandlePresentationEnded;
        }

        private void Refresh(PlayerState state)
        {
            // PlayerState is already updated (it's the truth); only the *display* waits, so the coin
            // counter doesn't jump before the reels show why.
            // Reels stop -> coins launch (IsFlying) -> OnPresentationEnded lands here and keeps waiting
            // -> OnAllCoinsLanded -> draw. A win with no coins (shield, energy) draws when the reels stop.
            if ((reelView != null && reelView.IsPresenting) || (coinFlyPool != null && coinFlyPool.IsFlying))
            {
                refreshPending = true;
                return;
            }

            refreshPending = false;
            Draw(state);
        }

        private void HandlePresentationEnded()
        {
            if (refreshPending) Refresh(playerState);
        }

        private void Draw(PlayerState state)
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
