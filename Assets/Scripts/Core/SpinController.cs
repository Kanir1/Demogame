using System;
using System.Collections.Generic;
using DemoCoinMaster.Server;
using UnityEngine;

namespace DemoCoinMaster.Core
{
    /// <summary>
    /// Owns the spin flow: current bet, double-tap protection, the server call, and the events
    /// every view reacts to. It does not know any UI type. The Spin button calls RequestSpin()
    /// (wired in the Inspector), and views subscribe to the events below.
    ///
    /// Flow: RequestSpin -> OnSpinStarted -> IServerApi.SpinAsync -> PlayerState.ApplySnapshot
    ///       -> OnSpinResult (success) or OnSpinFailed (error).
    /// </summary>
    // After GameBootstrap (-1000), before views (0), so views can read CurrentBet in OnEnable.
    [DefaultExecutionOrder(-500)]
    public class SpinController : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [Tooltip("Log every spin to the Console. Handy until the UI exists.")]
        [SerializeField] private bool logToConsole = true;

        private IServerApi server;
        private PlayerState playerState;
        private IReadOnlyList<int> betOptions;
        private int betIndex;

        public bool IsSpinning { get; private set; }
        public int CurrentBet => betOptions != null && betOptions.Count > 0 ? betOptions[betIndex] : 1;

        /// <summary>Bet that was sent to the server.</summary>
        public event Action<int> OnSpinStarted;
        /// <summary>A successful spin. PlayerState is already updated when this fires.</summary>
        public event Action<SpinResult> OnSpinResult;
        /// <summary>The spin was rejected or failed. Nothing was charged or rewarded.</summary>
        public event Action<ServerError> OnSpinFailed;
        /// <summary>The new bet multiplier.</summary>
        public event Action<int> OnBetChanged;

        private void Awake()
        {
            // GameBootstrap runs first (DefaultExecutionOrder), so its services exist here.
            if (bootstrap == null || bootstrap.Server == null)
            {
                Debug.LogError("[SpinController] GameBootstrap is not assigned or failed to start.", this);
                enabled = false;
                return;
            }

            server = bootstrap.Server;
            playerState = bootstrap.PlayerState;
            betOptions = bootstrap.Config.BetMultipliers;
            betIndex = 0;
        }

        /// <summary>Hook this to the Spin button's OnClick.</summary>
        public void RequestSpin()
        {
            // Double-tap guard. Set synchronously before the first await, so a second tap
            // in the same frame (or while waiting for the server) is ignored.
            if (!enabled || IsSpinning)
            {
                if (logToConsole && IsSpinning) Debug.Log("[SpinController] Spin ignored: already spinning.");
                return;
            }

            SpinAsync(CurrentBet);
        }

        /// <summary>Hook this to the Bet button's OnClick. Cycles x1 -> x2 -> x3 -> x1.</summary>
        public void CycleBet()
        {
            if (betOptions == null || betOptions.Count == 0) return;
            betIndex = (betIndex + 1) % betOptions.Count;
            OnBetChanged?.Invoke(CurrentBet);
        }

        // async void is acceptable only here, at the top of a UI callback chain,
        // and only because every exception is caught inside.
        private async void SpinAsync(int bet)
        {
            IsSpinning = true;
            OnSpinStarted?.Invoke(bet);

            try
            {
                SpinResult result = await server.SpinAsync(bet, destroyCancellationToken);

                // A NetworkError means no real response arrived, so there's nothing to apply.
                // Any other result (success or a rule rejection) carries the server's real balances.
                if (result.Error != ServerError.NetworkError)
                    playerState.ApplySnapshot(result.Snapshot);

                if (result.Success)
                {
                    if (logToConsole) LogResult(result);
                    OnSpinResult?.Invoke(result);
                }
                else
                {
                    if (logToConsole) Debug.LogWarning($"[SpinController] Spin failed: {result.Error}");
                    OnSpinFailed?.Invoke(result.Error);
                }
            }
            catch (OperationCanceledException)
            {
                // Destroyed mid-spin (scene unload / leaving Play mode). Don't touch anything.
                return;
            }
            catch (Exception e)
            {
                // A bug in the server code must not leave the Spin button locked forever.
                Debug.LogException(e, this);
                OnSpinFailed?.Invoke(ServerError.NetworkError);
            }
            finally
            {
                IsSpinning = false;
            }
        }

        private void LogResult(SpinResult r)
        {
            Debug.Log($"[SpinController] x{r.Bet}: {string.Join(" | ", r.Symbols)} -> {r.Outcome} " +
                      $"+{r.CoinsWon} coins +{r.EnergyWon} energy +{r.ShieldsWon} shield" +
                      (r.ShieldOverflow ? " (overflow -> coins)" : "") +
                      $" | coins={playerState.Coins} energy={playerState.Energy} shields={playerState.Shields}");
        }

        // ---------- Editor test hooks (right-click the component header in Play mode) ----------

        [ContextMenu("Debug: Spin")]
        private void DebugSpin() => RequestSpin();

        [ContextMenu("Debug: Double Tap Spin")]
        private void DebugDoubleTap()
        {
            RequestSpin();
            RequestSpin(); // should log "Spin ignored: already spinning."
        }

        [ContextMenu("Debug: Cycle Bet")]
        private void DebugCycleBet()
        {
            CycleBet();
            Debug.Log($"[SpinController] Bet is now x{CurrentBet}");
        }
    }
}
