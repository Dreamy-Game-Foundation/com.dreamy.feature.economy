using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    /// <summary>Game-owned shortage UI. Completion includes closing the UI and releasing its input ownership.</summary>
    public interface IInsufficientResourcesPresentation
    {
        /// <summary>Receives the complete costs; re-read balances to display current deficits. Never retries the action.</summary>
        UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs, string contextId, CancellationToken token);
    }
}
