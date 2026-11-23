using System;
using UnityEngine;

namespace SpinVillage.Config
{
    public enum ConfigSource
    {
        /// <summary>Only the ScriptableObject values (no JSON, or the JSON was rejected).</summary>
        Asset,
        /// <summary>The ScriptableObject values with the JSON overrides applied on top.</summary>
        RemoteOverrides
    }

    /// <summary>
    /// Builds the runtime config in layers, the way remote config usually works:
    ///   1. Start from a copy of the ScriptableObject (designer defaults, always valid, ships with the build).
    ///   2. Apply the JSON on top with FromJsonOverwrite: only the keys present in the JSON change.
    ///   3. Validate. If the JSON is missing, malformed or produces invalid values, discard it and use step 1.
    ///
    /// Here the JSON is a TextAsset in Resources, standing in for a download. In production this would be
    /// fetched (UnityWebRequest or a remote config service), cached on disk, and the last good cached copy
    /// used when offline, with the bundled asset as the final fallback.
    /// </summary>
    public static class ConfigLoader
    {
        public static GameConfigData Load(GameConfig fallbackAsset, string jsonResourcePath, out ConfigSource source)
        {
            if (fallbackAsset == null) throw new ArgumentNullException(nameof(fallbackAsset));

            source = ConfigSource.Asset;

            if (string.IsNullOrEmpty(jsonResourcePath))
                return fallbackAsset.CreateRuntimeCopy();

            TextAsset json = Resources.Load<TextAsset>(jsonResourcePath);
            if (json == null)
            {
                Debug.LogWarning($"[ConfigLoader] No remote config at Resources/{jsonResourcePath}. Using the asset.");
                return fallbackAsset.CreateRuntimeCopy();
            }

            GameConfigData merged = fallbackAsset.CreateRuntimeCopy();
            try
            {
                JsonUtility.FromJsonOverwrite(json.text, merged);
            }
            catch (Exception e)
            {
                // Malformed JSON. Never let a bad remote push break startup.
                Debug.LogWarning($"[ConfigLoader] Remote config could not be parsed ({e.Message}). Using the asset.");
                return fallbackAsset.CreateRuntimeCopy();
            }
            finally
            {
                Resources.UnloadAsset(json); // the text is no longer needed in memory
            }

            var errors = merged.Validate();
            if (errors.Count > 0)
            {
                Debug.LogWarning($"[ConfigLoader] Remote config rejected: {string.Join("; ", errors)}. Using the asset.");
                return fallbackAsset.CreateRuntimeCopy();
            }

            source = ConfigSource.RemoteOverrides;
            return merged;
        }
    }
}
