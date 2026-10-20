namespace Dreamy.Economy.UI
{
    public sealed class RewardPresentationState
    {
        public RewardPresentationState(RewardPresentationRequest request, RewardClaimSnapshot snapshot = null,
            bool loading = false, bool executing = false, string error = null)
        { Request = request; Snapshot = snapshot; IsLoading = loading; IsExecuting = executing; Error = error; }
        public RewardPresentationRequest Request { get; }
        public RewardClaimSnapshot Snapshot { get; }
        public bool IsLoading { get; }
        public bool IsExecuting { get; }
        public string Error { get; }
        public bool CanClaim => Request.Mode == RewardPresentationMode.ConfirmClaim && !IsLoading && !IsExecuting && Error == null &&
            Snapshot != null && (Snapshot.Status == RewardClaimStatus.Available || Snapshot.CanRetry);
    }
}
