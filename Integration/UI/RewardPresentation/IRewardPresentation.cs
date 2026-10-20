using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    public interface IRewardPresentation
    {
        UniTask<RewardPresentationResult> OpenAsync(RewardPresentationRequest request,
            IRewardClaimSource source = null, CancellationToken uiCancellationToken = default);
    }

    public interface IRewardPresentationHost
    {
        // The host owns a single view lease. CloseAsync must also release a partly failed open.
        UniTask<IRewardPresentationView> OpenAsync(RewardPresentationRequest request, CancellationToken cancellationToken);
        UniTask CloseAsync(IRewardPresentationView view);
    }
}
