namespace SpinVillage.Server
{
    public class UpgradeResult
    {
        public ServerError Error { get; }
        public bool Success => Error == ServerError.None;
        public int CoinsSpent { get; }
        public PlayerSnapshot Snapshot { get; }

        public UpgradeResult(ServerError error, int coinsSpent, PlayerSnapshot snapshot)
        {
            Error = error;
            CoinsSpent = coinsSpent;
            Snapshot = snapshot;
        }
    }
}
