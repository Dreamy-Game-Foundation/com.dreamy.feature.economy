using System;
using Dreamy.Economy.Rewards;

namespace Dreamy.Economy.UI
{
    public sealed class RewardClaimReference : IEquatable<RewardClaimReference>
    {
        public RewardClaimReference(string scope, string sourceId, string claimId)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(claimId))
                throw new ArgumentException("Stable scope, source and claim IDs are required.");
            Scope = scope; SourceId = sourceId; ClaimId = claimId;
            Key = RewardIdentity.Encode("claim-reference", scope, sourceId, claimId);
        }
        public string Scope { get; }
        public string SourceId { get; }
        public string ClaimId { get; }
        public string Key { get; }
        public bool Equals(RewardClaimReference other) => other != null && Key == other.Key;
        public override bool Equals(object obj) => obj is RewardClaimReference other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);
    }
}
