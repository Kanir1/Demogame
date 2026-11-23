namespace SpinVillage.Server
{
    /// <summary>Why a request was rejected. None = success.</summary>
    public enum ServerError
    {
        None,
        NetworkError,      // simulated failure: nothing was charged or rewarded
        RequestInProgress, // a second request arrived while one was still running
        InvalidBet,
        NotEnoughEnergy,
        NotEnoughCoins,
        MaxLevelReached
    }
}
