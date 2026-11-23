using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpinVillage.Config
{
    /// <summary>Weight for "all reels land on this symbol". Higher = more likely.</summary>
    [Serializable]
    public class SymbolWeight
    {
        [SerializeField] private SymbolType symbol;
        [SerializeField, Min(0)] private int weight;

        public SymbolType Symbol => symbol;
        public int Weight => weight;

        public SymbolWeight() { }

        public SymbolWeight(SymbolType symbol, int weight)
        {
            this.symbol = symbol;
            this.weight = weight;
        }
    }

    /// <summary>
    /// All tuning values as a plain serializable class.
    /// Kept separate from the ScriptableObject so the same shape can be filled from
    /// remote JSON (JsonUtility) without touching the asset on disk.
    /// The defaults below only seed a newly created asset; the asset or JSON is the real source.
    /// </summary>
    [Serializable]
    public class GameConfigData
    {
        [Header("Reels")]
        [SerializeField, Min(1)] private int reelCount = 3;
        [Tooltip("Weight of a spin where the reels do NOT match (tiny reward).")]
        [SerializeField, Min(0)] private int mixedWeight = 40;
        [Tooltip("Weight of each 'all reels match' outcome.")]
        [SerializeField] private List<SymbolWeight> symbolWeights = new List<SymbolWeight>
        {
            new SymbolWeight(SymbolType.Coin, 20),
            new SymbolWeight(SymbolType.CoinBag, 8),
            new SymbolWeight(SymbolType.Hammer, 10),
            new SymbolWeight(SymbolType.Pig, 6),
            new SymbolWeight(SymbolType.Shield, 8),
            new SymbolWeight(SymbolType.Energy, 8),
        };

        [Header("Rewards (base values, multiplied by bet)")]
        [SerializeField, Min(0)] private int mixedCoinReward = 10;
        [SerializeField, Min(0)] private int coinReward = 100;
        [SerializeField, Min(0)] private int coinBagReward = 500;
        [SerializeField, Min(0)] private int attackCoinReward = 300;
        [SerializeField, Min(0)] private int raidCoinReward = 1000;
        [SerializeField, Min(0)] private int energySpinReward = 10;
        [Tooltip("Coins given instead of a shield when shields are already at max.")]
        [SerializeField, Min(0)] private int shieldOverflowCoins = 200;

        [Header("Shields")]
        [SerializeField, Min(0)] private int maxShields = 3;

        [Header("Bet")]
        [SerializeField] private int[] betMultipliers = { 1, 2, 3 };

        [Header("Energy")]
        [SerializeField, Min(1)] private int energyCostPerSpin = 1;
        [Tooltip("Refill stops at this cap. Energy won from spins may go above it.")]
        [SerializeField, Min(1)] private int maxEnergy = 50;
        [SerializeField, Min(1)] private float energyRefillSeconds = 10f;
        [Tooltip("If a refill sync comes back early (clock estimate ahead of the server), wait this long before asking again.")]
        [SerializeField, Min(0.1f)] private float energySyncRetrySeconds = 1f;

        [Header("Starting balances")]
        [SerializeField, Min(0)] private int startingCoins = 1000;
        [SerializeField, Min(0)] private int startingEnergy = 50;

        [Header("Village")]
        [Tooltip("Cost to reach level 1, 2, 3... Max level = array length.")]
        [SerializeField] private int[] buildingUpgradeCosts = { 500, 1500, 4000, 10000, 25000 };

        [Header("Reel presentation")]
        [Tooltip("Reels spin at least this long, even if the server answers faster.")]
        [SerializeField, Min(0f)] private float reelMinSpinSeconds = 0.8f;
        [Tooltip("Delay between one reel stopping and the next.")]
        [SerializeField, Min(0f)] private float reelStopIntervalSeconds = 0.25f;
        [Tooltip("How often a spinning reel shows a new random symbol.")]
        [SerializeField, Min(0.01f)] private float reelSymbolChangeSeconds = 0.05f;
        [Tooltip("Pause after the last reel lands before rewards/popups appear, so the player sees the match first.")]
        [SerializeField, Min(0f)] private float reelResultHoldSeconds = 0.6f;

        [Header("UI")]
        [Tooltip("A new popup ignores taps for this long, so a double tap can't close two popups at once.")]
        [SerializeField, Min(0f)] private float popupInputLockSeconds = 0.3f;

        [Header("Mock server")]
        [SerializeField, Min(0)] private float serverMinDelaySeconds = 0.2f;
        [SerializeField, Min(0)] private float serverMaxDelaySeconds = 0.6f;
        [SerializeField, Range(0f, 1f)] private float serverFailureChance = 0.05f;

        public int ReelCount => reelCount;
        public int MixedWeight => mixedWeight;
        public IReadOnlyList<SymbolWeight> SymbolWeights => symbolWeights;

        public int MixedCoinReward => mixedCoinReward;
        public int CoinReward => coinReward;
        public int CoinBagReward => coinBagReward;
        public int AttackCoinReward => attackCoinReward;
        public int RaidCoinReward => raidCoinReward;
        public int EnergySpinReward => energySpinReward;
        public int ShieldOverflowCoins => shieldOverflowCoins;
        public int MaxShields => maxShields;

        public IReadOnlyList<int> BetMultipliers => betMultipliers;

        public int EnergyCostPerSpin => energyCostPerSpin;
        public int MaxEnergy => maxEnergy;
        public float EnergyRefillSeconds => energyRefillSeconds;
        public float EnergySyncRetrySeconds => energySyncRetrySeconds;

        public int StartingCoins => startingCoins;
        public int StartingEnergy => startingEnergy;

        public IReadOnlyList<int> BuildingUpgradeCosts => buildingUpgradeCosts;
        public int MaxBuildingLevel => buildingUpgradeCosts?.Length ?? 0;

        public float ReelMinSpinSeconds => reelMinSpinSeconds;
        public float ReelStopIntervalSeconds => reelStopIntervalSeconds;
        public float ReelSymbolChangeSeconds => reelSymbolChangeSeconds;
        public float ReelResultHoldSeconds => reelResultHoldSeconds;

        public float PopupInputLockSeconds => popupInputLockSeconds;

        public float ServerMinDelaySeconds => serverMinDelaySeconds;
        public float ServerMaxDelaySeconds => serverMaxDelaySeconds;
        public float ServerFailureChance => serverFailureChance;

        /// <summary>Returns a list of problems. Empty list = config is usable.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (reelCount < 1) errors.Add("reelCount must be >= 1");
            if (symbolWeights == null || symbolWeights.Count == 0) errors.Add("symbolWeights is empty");

            int total = mixedWeight;
            if (symbolWeights != null)
                foreach (var w in symbolWeights) total += Mathf.Max(0, w.Weight);
            if (total <= 0) errors.Add("total outcome weight must be > 0");

            if (betMultipliers == null || betMultipliers.Length == 0) errors.Add("betMultipliers is empty");
            else foreach (var b in betMultipliers) if (b < 1) errors.Add("bet multipliers must be >= 1");

            if (buildingUpgradeCosts == null || buildingUpgradeCosts.Length == 0) errors.Add("buildingUpgradeCosts is empty");
            if (serverMaxDelaySeconds < serverMinDelaySeconds) errors.Add("serverMaxDelaySeconds < serverMinDelaySeconds");
            return errors;
        }
    }
}
