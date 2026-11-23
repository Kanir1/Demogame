using System;
using SpinVillage.Config;
using SpinVillage.Server;
using UnityEngine;

namespace SpinVillage.Core
{
    /// <summary>
    /// Shows when the next energy refill happens, using server time only.
    ///
    /// - Never reads the device date/time (a player could change it to farm energy).
    /// - Estimates "server now" as: snapshot.ServerTime + (local monotonic time since that snapshot arrived).
    ///   Time.realtimeSinceStartupAsDouble counts up from app start and can't be changed by the user.
    /// - When the countdown reaches 0 it asks the server for the real state. It never adds energy itself:
    ///   only the server changes balances.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class EnergyTimer : MonoBehaviour
    {
        /// <summary>Value of OnCountdownChanged when energy is at or above the cap (no refill running).</summary>
        public const int NotRefilling = -1;

        [SerializeField] private GameBootstrap bootstrap;

        private GameConfigData config;
        private IServerApi server;
        private PlayerState playerState;

        private double serverTimeAtSnapshot;
        private double localTimeAtSnapshot;
        private bool syncInFlight;
        private double nextSyncAllowedLocalTime;
        private int lastReportedSeconds = int.MinValue;

        /// <summary>Whole seconds until the next +1 energy, or NotRefilling. Fires only when the value changes.</summary>
        public event Action<int> OnCountdownChanged;

        public int SecondsUntilNextRefill { get; private set; } = NotRefilling;

        private void Awake()
        {
            if (bootstrap == null || bootstrap.Server == null)
            {
                Debug.LogError("[EnergyTimer] GameBootstrap is not assigned or failed to start.", this);
                enabled = false;
                return;
            }

            config = bootstrap.Config;
            server = bootstrap.Server;
            playerState = bootstrap.PlayerState;
        }

        private void OnEnable()
        {
            if (playerState == null) return;
            playerState.OnBalanceChanged += HandleBalanceChanged;
            if (playerState.HasSynced) HandleBalanceChanged(playerState);
        }

        private void OnDisable()
        {
            if (playerState != null) playerState.OnBalanceChanged -= HandleBalanceChanged;
        }

        private void HandleBalanceChanged(PlayerState state)
        {
            // Every snapshot re-anchors the clock estimate, so drift never builds up.
            serverTimeAtSnapshot = state.ServerTime;
            localTimeAtSnapshot = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            if (!playerState.HasSynced) return;

            if (playerState.Energy >= config.MaxEnergy)
            {
                Report(NotRefilling);
                return;
            }

            double nextRefillServerTime = playerState.LastEnergyRefillTime + config.EnergyRefillSeconds;
            double remaining = nextRefillServerTime - EstimatedServerNow();

            Report(Math.Max(0, (int)Math.Ceiling(remaining)));

            if (remaining <= 0) TrySync();
        }

        private double EstimatedServerNow() =>
            serverTimeAtSnapshot + (Time.realtimeSinceStartupAsDouble - localTimeAtSnapshot);

        private void Report(int seconds)
        {
            SecondsUntilNextRefill = seconds;
            if (seconds == lastReportedSeconds) return; // no event (and no UI string) unless the number changes
            lastReportedSeconds = seconds;
            OnCountdownChanged?.Invoke(seconds);
        }

        // async void only at this top level, with every exception caught.
        private async void TrySync()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (syncInFlight || now < nextSyncAllowedLocalTime) return;

            syncInFlight = true;
            // If our estimate ran slightly ahead of the server, the reply won't contain a refill yet.
            // This delay stops us from asking again every frame.
            nextSyncAllowedLocalTime = now + config.EnergySyncRetrySeconds;
            try
            {
                PlayerSnapshot snapshot = await server.GetStateAsync(destroyCancellationToken);
                playerState.ApplySnapshot(snapshot);
            }
            catch (OperationCanceledException)
            {
                // Destroyed while waiting. Nothing to do.
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                syncInFlight = false;
            }
        }
    }
}
