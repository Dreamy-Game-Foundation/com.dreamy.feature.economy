namespace Dreamy.Economy.Rewards
{
    public enum RewardDeliveryStatus { Failed, Delivered, AlreadyDelivered, Pending }

    public enum RewardDeliveryReason
    {
        None, InvalidRequest, UnknownKind, UnknownTarget, Overflow, PayloadMismatch,
        LegacyReceiptUnverifiable, PersistenceFailed, DestinationUnavailable, Busy,
        ResolutionRequired, InvalidJournal, InvalidTransition
    }

    public readonly struct RewardDeliveryResult
    {
        private RewardDeliveryResult(RewardDeliveryStatus status, RewardDeliveryReason reason, bool retryable)
        {
            Status = status;
            Reason = reason;
            Retryable = retryable;
        }

        public RewardDeliveryStatus Status { get; }
        public RewardDeliveryReason Reason { get; }
        public bool Retryable { get; }
        public bool IsSuccess => Status == RewardDeliveryStatus.Delivered || Status == RewardDeliveryStatus.AlreadyDelivered;

        public static RewardDeliveryResult Delivered() => new(RewardDeliveryStatus.Delivered, RewardDeliveryReason.None, false);
        public static RewardDeliveryResult AlreadyDelivered() => new(RewardDeliveryStatus.AlreadyDelivered, RewardDeliveryReason.None, false);
        public static RewardDeliveryResult Failed(RewardDeliveryReason reason) => new(RewardDeliveryStatus.Failed, reason, false);
        public static RewardDeliveryResult Pending(RewardDeliveryReason reason) => new(RewardDeliveryStatus.Pending, reason, true);
    }
}
