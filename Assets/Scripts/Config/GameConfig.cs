using UnityEngine;

namespace DemoCoinMaster.Config
{
    /// <summary>
    /// Designer-editable config asset. Acts as the fallback when remote (JSON) config
    /// is missing or invalid. Runtime code should read GameConfigData, never edit this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "DemoCoinMaster/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] private GameConfigData data = new GameConfigData();

        public GameConfigData Data => data;

        /// <summary>
        /// A deep copy, so runtime code (or a JSON overwrite) can never modify the asset on disk.
        /// In the Editor, changes to a ScriptableObject in Play mode are saved to the asset.
        /// </summary>
        public GameConfigData CreateRuntimeCopy()
        {
            return JsonUtility.FromJson<GameConfigData>(JsonUtility.ToJson(data));
        }

        private void OnValidate()
        {
            foreach (var error in data.Validate())
                Debug.LogWarning($"[GameConfig] {error}", this);
        }
    }
}
