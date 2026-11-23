using System;
using System.Diagnostics;
using System.Threading;
using SpinVillage.Config;
using UnityEngine;

namespace SpinVillage.Server
{
    /// <summary>
    /// In-process fake backend. Owns the balances and every rule about them.
    /// Plain C# (not a MonoBehaviour) so it can be unit-tested and swapped for a real server.
    ///
    /// Order inside every request: network delay -> simulated failure -> validate -> change state.
    /// A simulated failure happens before any change, so a failed spin never charges or rewards.
    /// </summary>
    public class MockServer : IServerApi
    {
        private readonly GameConfigData config;
        private readonly System.Random random;
        private readonly Func<double> now;

        // Server-side player data: the source of truth.
        private int coins;
        private int energy;
        private int shields;
        private int buildingLevel;
        private double lastEnergyRefillTime;

        // Defense in depth: the client blocks double taps, but the server must not trust that.
        private bool requestInProgress;

        /// <param name="seed">Fixed seed for repeatable tests; null = random.</param>
        /// <param name="clock">Server clock in seconds; defaults to a Stopwatch (not the device date/time).</param>
        public MockServer(GameConfigData config, int? seed = null, Func<double> clock = null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            if (clock == null)
            {
                var stopwatch = Stopwatch.StartNew();
                clock = () => stopwatch.Elapsed.TotalSeconds;
            }
            now = clock;

            coins = config.StartingCoins;
            energy = config.StartingEnergy;
            shields = 0;
            buildingLevel = 0;
            lastEnergyRefillTime = now();
        }

        public async Awaitable<PlayerSnapshot> GetStateAsync(CancellationToken ct)
        {
            await SimulateLatency(ct);
            ApplyEnergyRefill();
            return Snapshot();
        }

        public async Awaitable<SpinResult> SpinAsync(int bet, CancellationToken ct)
        {
            if (requestInProgress)
                return SpinResult.Failed(ServerError.RequestInProgress, bet, Snapshot());

            requestInProgress = true;
            try
            {
                await SimulateLatency(ct);

                if (SimulateFailure())
                    return SpinResult.Failed(ServerError.NetworkError, bet, Snapshot());

                ApplyEnergyRefill();

                if (!IsValidBet(bet))
                    return SpinResult.Failed(ServerError.InvalidBet, bet, Snapshot());

                int cost = config.EnergyCostPerSpin * bet;
                if (energy < cost)
                    return SpinResult.Failed(ServerError.NotEnoughEnergy, bet, Snapshot());

                energy -= cost;

                SpinOutcome outcome = RollOutcome(out SymbolType matched);
                SymbolType[] symbols = outcome == SpinOutcome.Mixed ? BuildMixedSymbols() : BuildMatchingSymbols(matched);

                int coinsWon = 0, energyWon = 0, shieldsWon = 0;
                bool shieldOverflow = false;

                switch (outcome)
                {
                    case SpinOutcome.Mixed:   coinsWon = config.MixedCoinReward * bet; break;
                    case SpinOutcome.Coin:    coinsWon = config.CoinReward * bet; break;
                    case SpinOutcome.CoinBag: coinsWon = config.CoinBagReward * bet; break;
                    case SpinOutcome.Attack:  coinsWon = config.AttackCoinReward * bet; break;
                    case SpinOutcome.Raid:    coinsWon = config.RaidCoinReward * bet; break;
                    case SpinOutcome.Energy:  energyWon = config.EnergySpinReward * bet; break;
                    case SpinOutcome.Shield:
                        if (shields < config.MaxShields) shieldsWon = 1;
                        else
                        {
                            shieldOverflow = true;
                            coinsWon = config.ShieldOverflowCoins * bet;
                        }
                        break;
                }

                coins += coinsWon;
                energy += energyWon; // may exceed MaxEnergy on purpose; only the timed refill is capped
                shields += shieldsWon;

                return new SpinResult(ServerError.None, bet, outcome, symbols,
                                      coinsWon, energyWon, shieldsWon, shieldOverflow, Snapshot());
            }
            finally
            {
                requestInProgress = false;
            }
        }

        public async Awaitable<UpgradeResult> UpgradeAsync(CancellationToken ct)
        {
            if (requestInProgress)
                return new UpgradeResult(ServerError.RequestInProgress, 0, Snapshot());

            requestInProgress = true;
            try
            {
                await SimulateLatency(ct);

                if (SimulateFailure())
                    return new UpgradeResult(ServerError.NetworkError, 0, Snapshot());

                ApplyEnergyRefill();

                if (buildingLevel >= config.MaxBuildingLevel)
                    return new UpgradeResult(ServerError.MaxLevelReached, 0, Snapshot());

                int cost = config.BuildingUpgradeCosts[buildingLevel];
                if (coins < cost)
                    return new UpgradeResult(ServerError.NotEnoughCoins, 0, Snapshot());

                coins -= cost;
                buildingLevel++;
                return new UpgradeResult(ServerError.None, cost, Snapshot());
            }
            finally
            {
                requestInProgress = false;
            }
        }

        // ---------- helpers ----------

        private PlayerSnapshot Snapshot() =>
            new PlayerSnapshot(coins, energy, shields, buildingLevel, lastEnergyRefillTime, now());

        /// <summary>
        /// Lazy refill: no ticking timer on the server. On each request, add the energy
        /// earned since the last refill. This is how real backends do it (no per-player timers).
        /// </summary>
        private void ApplyEnergyRefill()
        {
            double t = now();
            if (energy >= config.MaxEnergy)
            {
                // Timer is paused while full; it starts counting from the moment energy drops below the cap.
                lastEnergyRefillTime = t;
                return;
            }

            int ticks = (int)Math.Floor((t - lastEnergyRefillTime) / config.EnergyRefillSeconds);
            if (ticks <= 0) return;

            energy = Math.Min(config.MaxEnergy, energy + ticks);
            lastEnergyRefillTime = energy >= config.MaxEnergy ? t : lastEnergyRefillTime + ticks * config.EnergyRefillSeconds;
        }

        private bool IsValidBet(int bet)
        {
            foreach (int option in config.BetMultipliers)
                if (option == bet) return true;
            return false;
        }

        /// <summary>Outcome-first: pick the outcome by weight, then build reels to match it.</summary>
        private SpinOutcome RollOutcome(out SymbolType matched)
        {
            matched = default;
            int total = config.MixedWeight;
            foreach (var w in config.SymbolWeights) total += Math.Max(0, w.Weight);

            int roll = random.Next(total);
            if (roll < config.MixedWeight) return SpinOutcome.Mixed;
            roll -= config.MixedWeight;

            foreach (var w in config.SymbolWeights)
            {
                int weight = Math.Max(0, w.Weight);
                if (roll < weight)
                {
                    matched = w.Symbol;
                    return ToOutcome(w.Symbol);
                }
                roll -= weight;
            }
            return SpinOutcome.Mixed; // unreachable when weights are valid
        }

        private static SpinOutcome ToOutcome(SymbolType symbol)
        {
            switch (symbol)
            {
                case SymbolType.Coin:    return SpinOutcome.Coin;
                case SymbolType.CoinBag: return SpinOutcome.CoinBag;
                case SymbolType.Hammer:  return SpinOutcome.Attack;
                case SymbolType.Pig:     return SpinOutcome.Raid;
                case SymbolType.Shield:  return SpinOutcome.Shield;
                case SymbolType.Energy:  return SpinOutcome.Energy;
                default: throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null);
            }
        }

        private SymbolType[] BuildMatchingSymbols(SymbolType symbol)
        {
            var symbols = new SymbolType[config.ReelCount];
            for (int i = 0; i < symbols.Length; i++) symbols[i] = symbol;
            return symbols;
        }

        /// <summary>Random symbols that are guaranteed not to all match.</summary>
        private SymbolType[] BuildMixedSymbols()
        {
            int symbolCount = Enum.GetValues(typeof(SymbolType)).Length;
            var symbols = new SymbolType[config.ReelCount];
            for (int i = 0; i < symbols.Length; i++)
                symbols[i] = (SymbolType)random.Next(symbolCount);

            if (symbols.Length > 1 && AllSame(symbols))
            {
                int last = symbols.Length - 1;
                symbols[last] = (SymbolType)(((int)symbols[last] + 1 + random.Next(symbolCount - 1)) % symbolCount);
            }
            return symbols;
        }

        private static bool AllSame(SymbolType[] symbols)
        {
            for (int i = 1; i < symbols.Length; i++)
                if (symbols[i] != symbols[0]) return false;
            return true;
        }

        private async Awaitable SimulateLatency(CancellationToken ct)
        {
            float delay = Mathf.Lerp(config.ServerMinDelaySeconds, config.ServerMaxDelaySeconds, (float)random.NextDouble());
            if (delay > 0f)
                await Awaitable.WaitForSecondsAsync(delay, ct);
        }

        private bool SimulateFailure() => random.NextDouble() < config.ServerFailureChance;
    }
}
