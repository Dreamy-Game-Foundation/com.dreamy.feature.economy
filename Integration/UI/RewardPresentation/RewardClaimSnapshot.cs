using System;
using Dreamy.Economy.Rewards;

namespace Dreamy.Economy.UI
{
    public enum RewardClaimStatus { Available, Executing, Pending, Claimed, AlreadyClaimed, Expired, Blocked, Unavailable, Busy, RequiresReload }

    // This is an observation of the feature owner, never permission to grant in the view.
    public sealed class RewardClaimSnapshot
    {
        public RewardClaimSnapshot(RewardClaimReference reference, string fingerprint, ResolvedRewardBundle bundle,
            RewardClaimStatus status, string reason = null, bool canRetry = false)
        {
            Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            if (string.IsNullOrWhiteSpace(fingerprint)) throw new ArgumentException("An authoritative snapshot fingerprint is required.");
            if (!Enum.IsDefined(typeof(RewardClaimStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if ((status == RewardClaimStatus.Available || status == RewardClaimStatus.Claimed || status == RewardClaimStatus.AlreadyClaimed) && bundle == null)
                throw new ArgumentException("Available and completed claims require the original bundle.");
            Fingerprint = fingerprint; Bundle = bundle; Status = status; Reason = reason;
            CanRetry = canRetry && (status == RewardClaimStatus.Pending || status == RewardClaimStatus.Busy);
        }
        public RewardClaimReference Reference { get; }
        public string Fingerprint { get; }
        public ResolvedRewardBundle Bundle { get; }
        public RewardClaimStatus Status { get; }
        public string Reason { get; }
        public bool CanRetry { get; }
        public bool IsCompleted => Status == RewardClaimStatus.Claimed || Status == RewardClaimStatus.AlreadyClaimed;
    }
}
