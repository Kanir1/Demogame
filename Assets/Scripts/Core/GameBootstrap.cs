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
        [Tooltip("Designer defaults. Always used as the base, and as the fallback if the remote config is bad.")]
        [SerializeField] private GameConfig configAsset;
        [Tooltip("Untick to ignore the remote JSON and run on the asset only (e.g. to compare).")]
        [SerializeField] private bool useRemoteConfig = true;
        [Tooltip("Path inside a Resources folder, without extension. Stands in for a download URL.")]
        [SerializeField] private string remoteConfigPath = "Config/game_config";

        public GameConfigData Config { get; private set; }
        public ConfigSource ConfigSource { get; private set; }
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

            Config = ConfigLoader.Load(configAsset, useRemoteConfig ? remoteConfigPath : null, out ConfigSource source);
            ConfigSource = source;
            Debug.Log($"[GameBootstrap] Config: {Config.ConfigVersion} (source: {source})", this);

            // The loader already rejects a bad remote config. This catches a broken asset itself.
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
