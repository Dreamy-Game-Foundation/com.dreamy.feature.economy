using System;
using Dreamy.Economy.Rewards;

namespace Dreamy.Economy.UI
{
    public enum RewardPresentationMode { ConfirmClaim, GrantedNotice }

    public sealed class RewardPresentationRequest
    {
        private RewardPresentationRequest(RewardPresentationMode mode, RewardClaimReference reference,
            RewardClaimSnapshot granted, string title, string expectedFingerprint)
        {
            Mode = mode; Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            Granted = granted; Title = title ?? string.Empty; ExpectedFingerprint = expectedFingerprint;
            Key = RewardIdentity.Encode("reward-presentation", reference.Key, mode.ToString());
            Fingerprint = RewardIdentity.Fingerprint(Key, expectedFingerprint ?? string.Empty,
                granted?.Bundle?.Fingerprint ?? string.Empty, Title);
        }
        public static RewardPresentationRequest Confirm(RewardClaimReference reference, string title = "Rewards", string expectedFingerprint = null) =>
            new(RewardPresentationMode.ConfirmClaim, reference, null, title, expectedFingerprint);
        public static RewardPresentationRequest Notice(RewardClaimSnapshot completed, string title = "Rewards received")
        {
            if (completed == null || !completed.IsCompleted || completed.Bundle == null)
                throw new ArgumentException("A final committed feature snapshot is required for a notice.");
            return new RewardPresentationRequest(RewardPresentationMode.GrantedNotice, completed.Reference, completed, title, completed.Fingerprint);
        }
        public RewardPresentationMode Mode { get; }
        public RewardClaimReference Reference { get; }
        public RewardClaimSnapshot Granted { get; }
        public string Title { get; }
        public string ExpectedFingerprint { get; }
        public string Key { get; }
        public string Fingerprint { get; }
    }
}
