using System.Threading;
using UnityEngine;

namespace SpinVillage.Server
{
    /// <summary>
    /// Everything the client may ask the server. The only way balances change.
    /// MockServer implements it today; a real HTTP backend could implement it later
    /// without touching controllers or views.
    /// Failures come back as an Error code, not an exception. Only cancellation throws.
    /// </summary>
    public interface IServerApi
    {
        /// <summary>Current balances (initial sync, and when the energy timer ticks).</summary>
        Awaitable<PlayerSnapshot> GetStateAsync(CancellationToken ct);

        /// <summary>Spend energy (cost x bet), roll an outcome, apply the reward.</summary>
        Awaitable<SpinResult> SpinAsync(int bet, CancellationToken ct);

        /// <summary>Spend coins to raise the village building by one level.</summary>
        Awaitable<UpgradeResult> UpgradeAsync(CancellationToken ct);
    }
}
