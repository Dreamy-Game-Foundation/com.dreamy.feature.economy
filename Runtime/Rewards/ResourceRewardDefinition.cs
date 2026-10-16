using System;

namespace Dreamy.Economy.Rewards
{
    // Game-authored mapping; every target is a stable ResourceId in the shared economy document.
    public sealed class ResourceRewardDefinition
    {
        public ResourceRewardDefinition(string kindId, ResourceId targetId, bool unique = false)
        {
            if (string.IsNullOrWhiteSpace(kindId) || !targetId.IsValid)
                throw new ArgumentException("Reward kind and target are required.");
            if (kindId == DatasaveResourceWallet.CurrencyKind && unique)
                throw new ArgumentException("Currency must be stackable.");
            KindId = kindId; TargetId = targetId; Unique = unique;
        }
        public string KindId { get; }
        public ResourceId TargetId { get; }
        public bool Unique { get; }
    }
}
