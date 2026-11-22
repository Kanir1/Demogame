using DemoCoinMaster.Config;

namespace DemoCoinMaster.Server
{
    public class SpinResult
    {
        public ServerError Error { get; }
        public bool Success => Error == ServerError.None;

        public int Bet { get; }
        public SpinOutcome Outcome { get; }
        public SymbolType[] Symbols { get; }

        public int CoinsWon { get; }
        public int EnergyWon { get; }
        public int ShieldsWon { get; }
        /// <summary>True when a shield was won at the cap and converted to coins.</summary>
        public bool ShieldOverflow { get; }

        /// <summary>Balances after the spin. On failure, the balances the server holds now.</summary>
        public PlayerSnapshot Snapshot { get; }

        public SpinResult(ServerError error, int bet, SpinOutcome outcome, SymbolType[] symbols,
                          int coinsWon, int energyWon, int shieldsWon, bool shieldOverflow,
                          PlayerSnapshot snapshot)
        {
            Error = error;
            Bet = bet;
            Outcome = outcome;
            Symbols = symbols;
            CoinsWon = coinsWon;
            EnergyWon = energyWon;
            ShieldsWon = shieldsWon;
            ShieldOverflow = shieldOverflow;
            Snapshot = snapshot;
        }

        public static SpinResult Failed(ServerError error, int bet, PlayerSnapshot snapshot) =>
            new SpinResult(error, bet, SpinOutcome.Mixed, null, 0, 0, 0, false, snapshot);
    }
}
