using System;

namespace Dreamy.Economy.UI
{
    public interface IRewardPresentationView
    {
        event Action ClaimRequested;
        event Action CloseRequested;
        // Raised for external Back/disable/destroy, not an instruction to cancel the source.
        event Action Detached;
        void Render(RewardPresentationState state);
    }
}
