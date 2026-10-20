using System;

namespace Dreamy.Economy.UI
{
    public sealed class RewardClaimCommand
    {
        public RewardClaimCommand(RewardClaimReference reference, string expectedFingerprint)
        {
            Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            if (string.IsNullOrWhiteSpace(expectedFingerprint)) throw new ArgumentException("Expected fingerprint is required.");
            ExpectedFingerprint = expectedFingerprint;
        }
        public RewardClaimReference Reference { get; }
        public string ExpectedFingerprint { get; }
    }
}
