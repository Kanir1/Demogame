namespace SpinVillage.Server
{
    /// <summary>
    /// Read-only copy of the player's balances as the server sees them.
    /// A struct, so sending it to the client allocates nothing.
    /// Times are in server seconds, never the device clock.
    /// </summary>
    public readonly struct PlayerSnapshot
    {
        public readonly int Coins;
        public readonly int Energy;
        public readonly int Shields;
        public readonly int BuildingLevel;
        public readonly double LastEnergyRefillTime;
        public readonly double ServerTime;

        public PlayerSnapshot(int coins, int energy, int shields, int buildingLevel,
                              double lastEnergyRefillTime, double serverTime)
        {
            Coins = coins;
            Energy = energy;
            Shields = shields;
            BuildingLevel = buildingLevel;
            LastEnergyRefillTime = lastEnergyRefillTime;
            ServerTime = serverTime;
        }
    }
}
