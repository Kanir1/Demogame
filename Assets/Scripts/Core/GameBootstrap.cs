using System;
using SpinVillage.Config;
using SpinVillage.Server;
using UnityEngine;

namespace SpinVillage.Core
{
    /// <summary>
    /// Composition root: builds the config, server and player state once, and hands them out.
    /// Runs before every other script (DefaultExecutionOrder), so other components can read
    /// these services in their Awake/OnEnable.
    /// To use a real backend, swap MockServer here. Nothing else changes.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig configAsset;

        public GameConfigData Config { get; private set; }
        public IServerApi Server { get; private set; }
        public PlayerState PlayerState { get; private set; }

        private void Awake()
        {
            if (configAsset == null)
            {
                Debug.LogError("[GameBootstrap] Config asset is not assigned.", this);
                enabled = false;
                return;
            }

            // Day 2: try the remote JSON config first, fall back to this asset.
            Config = configAsset.CreateRuntimeCopy();
            foreach (var error in Config.Validate())
                Debug.LogError($"[GameBootstrap] Config error: {error}", this);

            Server = new MockServer(Config);
            PlayerState = new PlayerState();
        }

        private async void Start()
        {
            // async void is only OK at the top of the Unity call chain, and it must catch its own exceptions.
            try
            {
                PlayerSnapshot snapshot = await Server.GetStateAsync(destroyCancellationToken);
                PlayerState.ApplySnapshot(snapshot);
                Debug.Log($"[GameBootstrap] Synced: coins={PlayerState.Coins} energy={PlayerState.Energy}");
            }
            catch (OperationCanceledException)
            {
                // Object destroyed while waiting (scene unload / exit Play mode). Nothing to do.
            }
        }
    }
}
