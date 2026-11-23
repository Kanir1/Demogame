namespace SpinVillage.Server
{
    /// <summary>What a spin resolved to. Views map this to popups/effects.</summary>
    public enum SpinOutcome
    {
        Mixed,
        Coin,
        CoinBag,
        Attack, // 3x Hammer
        Raid,   // 3x Pig
        Shield,
        Energy
    }
}
