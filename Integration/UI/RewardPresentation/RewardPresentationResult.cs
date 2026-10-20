namespace Dreamy.Economy.UI
{
    public enum RewardPresentationOutcome
    {
        Dismissed, ClosedAfterCompletion, PresentationUnavailable, CancelledBeforeOpen,
        DetachedWhileExecuting, AlreadyQueued, QueueFull, PayloadMismatch, Cancelled
    }
    public enum RewardClaimObservation { NotDispatched, Completed, Pending, Unknown, RequiresReload }

    public readonly struct RewardPresentationResult
    {
        public RewardPresentationResult(RewardPresentationOutcome outcome, RewardClaimObservation observation = RewardClaimObservation.NotDispatched,
            RewardClaimSnapshot snapshot = null)
        { Outcome = outcome; Observation = observation; Snapshot = snapshot; }
        public RewardPresentationOutcome Outcome { get; }
        public RewardClaimObservation Observation { get; }
        public RewardClaimSnapshot Snapshot { get; }
    }
}
