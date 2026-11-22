using System;
using DemoCoinMaster.Server;

namespace DemoCoinMaster.Core
{
    /// <summary>
    /// The client's read-only copy of the player's balances.
    /// It is only ever overwritten by a server snapshot. The client never adds or subtracts,
    /// so the UI can never drift from the server, and the client can't cheat.
    /// </summary>
    public class PlayerState
    {
        public int Coins { get; private set; }
        public int Energy { get; private set; }
        public int Shields { get; private set; }
        public int BuildingLevel { get; private set; }

        /// <summary>Server time (seconds) of the last energy refill. Used by the energy timer.</summary>
        public double LastEnergyRefillTime { get; private set; }
        /// <summary>Server time (seconds) when the latest snapshot was taken.</summary>
        public double ServerTime { get; private set; }

        /// <summary>False until the first server sync arrives.</summary>
        public bool HasSynced { get; private set; }

        /// <summary>Raised after any snapshot is applied.</summary>
        public event Action<PlayerState> OnBalanceChanged;

        public void ApplySnapshot(in PlayerSnapshot snapshot)
        {
            Coins = snapshot.Coins;
            Energy = snapshot.Energy;
            Shields = snapshot.Shields;
            BuildingLevel = snapshot.BuildingLevel;
            LastEnergyRefillTime = snapshot.LastEnergyRefillTime;
            ServerTime = snapshot.ServerTime;
            HasSynced = true;

            OnBalanceChanged?.Invoke(this);
        }
    }
}
