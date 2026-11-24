using System;
using SpinVillage.Config;
using SpinVillage.Server;
using UnityEngine;

namespace SpinVillage.Core
{
    /// <summary>
    /// The upgrade flow, mirroring SpinController: double-tap guard, server call, snapshot, events.
    /// No UI references. The Upgrade button calls RequestUpgrade() (wired in BuildingView).
    ///
    /// No client-side "can afford?" check before calling: the view may show the cost in red, but the
    /// server decides, and a rejection comes back as NotEnoughCoins.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class VillageController : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;

        private IServerApi server;
        private PlayerState playerState;
        private GameConfigData config;

        public bool IsUpgrading { get; private set; }
        public int Level => playerState != null ? playerState.BuildingLevel : 0;
        public int MaxLevel => config != null ? config.MaxBuildingLevel : 0;
        public bool IsMaxLevel => Level >= MaxLevel;
        /// <summary>Cost of the next level, or 0 at max level. Display only; the server checks the real cost.</summary>
        public int NextUpgradeCost => IsMaxLevel ? 0 : config.BuildingUpgradeCosts[Level];

        public event Action OnUpgradeStarted;
        /// <summary>Upgrade succeeded. PlayerState already shows the new level and coins.</summary>
        public event Action<UpgradeResult> OnUpgradeSucceeded;
        /// <summary>Upgrade rejected or failed. Nothing was charged.</summary>
        public event Action<ServerError> OnUpgradeFailed;

        private void Awake()
        {
            if (bootstrap == null || bootstrap.Server == null)
            {
                Debug.LogError("[VillageController] GameBootstrap is not assigned or failed to start.", this);
                enabled = false;
                return;
            }

            server = bootstrap.Server;
            playerState = bootstrap.PlayerState;
            config = bootstrap.Config;
        }

        /// <summary>Hook this to the Upgrade button.</summary>
        public void RequestUpgrade()
        {
            // Double-tap guard, set before the first await. The server has its own as well.
            if (!enabled || IsUpgrading) return;
            UpgradeAsync();
        }

        // async void only here, at the top of a UI callback chain, with every exception caught.
        private async void UpgradeAsync()
        {
            IsUpgrading = true;
            OnUpgradeStarted?.Invoke();

            try
            {
                UpgradeResult result = await server.UpgradeAsync(destroyCancellationToken);

                // Same rule as spins: apply real balances unless the response never really arrived.
                if (result.Error != ServerError.NetworkError)
                    playerState.ApplySnapshot(result.Snapshot);

                if (result.Success) OnUpgradeSucceeded?.Invoke(result);
                else OnUpgradeFailed?.Invoke(result.Error);
            }
            catch (OperationCanceledException)
            {
                return; // destroyed mid-request
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                OnUpgradeFailed?.Invoke(ServerError.NetworkError);
            }
            finally
            {
                IsUpgrading = false;
            }
        }

        [ContextMenu("Debug: Double Tap Upgrade")]
        private void DebugDoubleTap()
        {
            RequestUpgrade();
            RequestUpgrade(); // ignored by the IsUpgrading guard
        }
    }
}
