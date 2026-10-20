using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    public interface IRewardClaimSource
    {
        // Read only. Must query this exact original claim, never "the current reward".
        UniTask<RewardClaimSnapshot> QueryAsync(RewardClaimReference reference, CancellationToken cancellationToken);
        // The source owns operation lifetime. UI close/cancellation cannot cancel a durable dispatch.
        // Claimed/AlreadyClaimed means final feature commit, not just successful owner delivery.
        UniTask<RewardClaimSnapshot> ClaimAsync(RewardClaimCommand command);
    }
}
