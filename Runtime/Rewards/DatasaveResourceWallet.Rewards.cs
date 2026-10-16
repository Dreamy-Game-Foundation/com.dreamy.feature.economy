using System;
using System.Collections.Generic;
using System.Globalization;
using Dreamy.Economy.Rewards;

namespace Dreamy.Economy
{
    public sealed partial class DatasaveResourceWallet
    {
        // Adapters share this owner, state, writer guard and receipt ledger. Never create a wallet per kind.
        public IRewardDestination CreateRewardDestination(string kindId)
        {
            if (string.IsNullOrWhiteSpace(kindId)) throw new ArgumentException("Kind is required.", nameof(kindId));
            bool known = kindId == CurrencyKind;
            foreach (var definition in rewardDefinitions.Values) known |= definition.KindId == kindId;
            if (!known) throw new ArgumentException("Register target definitions for this kind first.", nameof(kindId));
            return kindId == CurrencyKind ? (IRewardDestination)this : new SharedRewardDestination(this, kindId);
        }

        // Reconcile every service using this instance after an ambiguous write, preserving subscriptions.
        public void Reload()
        {
            if (busy) throw new InvalidOperationException("Economy is busy.");
            busy = true;
            persistenceUncertain = true;
            try
            {
                var restored = new DatasaveResourceWallet(datasave, saveKey, transactionHistoryLimit: transactionHistoryLimit,
                    rewardDefinitions: new List<ResourceRewardDefinition>(rewardDefinitions.Values));
                var previous = balances;
                Publish(restored.data, restored.balances);
                var changed = new HashSet<ResourceId>(previous.Keys);
                changed.UnionWith(balances.Keys);
                foreach (var id in changed)
                    if (!previous.TryGetValue(id, out long old) || old != GetBalance(id)) NotifyCommittedBalanceChanged(id);
            }
            finally { busy = false; }
        }

        // Host gathers an immutable legacy snapshot before retiring old writers. Import and marker are one save.
        // Old files remain intact for rollback; subsequent game mutations must use this owner exclusively.
        public RewardDeliveryResult ImportInventory(string migrationId, IReadOnlyList<ResourceAmount> amounts,
            IReadOnlyList<RewardReceiptSaveEntry> legacyReceipts)
        {
            if (persistenceUncertain) return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed);
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (string.IsNullOrWhiteSpace(migrationId) || amounts == null || legacyReceipts == null)
                return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
            busy = true;
            try
            {
                var normalized = new SortedDictionary<string, long>(StringComparer.Ordinal);
                var incoming = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var amount in amounts)
                {
                    if (!IsValid(amount)) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
                    if (!rewardDefinitions.ContainsKey(amount.ResourceId)) return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownTarget);
                    if (normalized.ContainsKey(amount.ResourceId.Value)) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
                    normalized.Add(amount.ResourceId.Value, amount.Amount);
                }
                foreach (var receipt in legacyReceipts)
                {
                    if (receipt == null || string.IsNullOrWhiteSpace(receipt.TransactionId) || string.IsNullOrWhiteSpace(receipt.Fingerprint) ||
                        incoming.ContainsKey(receipt.TransactionId)) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
                    incoming.Add(receipt.TransactionId, receipt.Fingerprint);
                }
                var fields = new List<string> { "inventory-import-v1", migrationId };
                fields.Add("amounts"); fields.Add(normalized.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var pair in normalized) { fields.Add(pair.Key); fields.Add(pair.Value.ToString(CultureInfo.InvariantCulture)); }
                fields.Add("receipts"); fields.Add(incoming.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var pair in incoming) { fields.Add(pair.Key); fields.Add(pair.Value); }
                string fingerprint = RewardIdentity.Fingerprint(fields.ToArray());
                foreach (var imported in data.InventoryImports)
                    if (imported.TransactionId == migrationId)
                        return imported.Fingerprint == fingerprint ? RewardDeliveryResult.AlreadyDelivered()
                            : RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                // Overlapping legacy sources cannot safely be added even if individual receipts match.
                foreach (var pair in incoming)
                    if (HasTransaction(pair.Key)) return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                var next = new Dictionary<ResourceId, long>(balances);
                try { foreach (var pair in normalized) Add(next, new ResourceAmount(new ResourceId(pair.Key), pair.Value)); }
                catch (OverflowException) { return RewardDeliveryResult.Failed(RewardDeliveryReason.Overflow); }
                var candidate = CreateCandidate(next);
                foreach (var pair in incoming) candidate.DeliveryReceipts.Add(new RewardReceiptSaveEntry { TransactionId = pair.Key, Fingerprint = pair.Value });
                candidate.InventoryImports.Add(new RewardReceiptSaveEntry { TransactionId = migrationId, Fingerprint = fingerprint });
                try { Commit(candidate, next); }
                catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed); }
                foreach (var pair in normalized) NotifyCommittedBalanceChanged(new ResourceId(pair.Key));
                return RewardDeliveryResult.Delivered();
            }
            finally { busy = false; }
        }

        private static void ValidateInventoryImports(EconomySaveData candidate)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var marker in candidate.InventoryImports)
                if (marker == null || string.IsNullOrWhiteSpace(marker.TransactionId) || string.IsNullOrWhiteSpace(marker.Fingerprint) || !ids.Add(marker.TransactionId))
                    throw new InvalidOperationException("Invalid inventory import ledger.");
        }

        private sealed class SharedRewardDestination : IRewardDestination
        {
            private readonly DatasaveResourceWallet owner;
            public SharedRewardDestination(DatasaveResourceWallet owner, string kindId) { this.owner = owner; KindId = kindId; }
            public string KindId { get; }
            public RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests)
            {
                if (requests == null) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
                foreach (var request in requests)
                    if (request.Entry.KindId != KindId) return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownKind);
                return owner.Validate(requests);
            }
            public RewardDeliveryResult Deliver(RewardDeliveryRequest request) => request.Entry.KindId == KindId
                ? owner.Deliver(request) : RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownKind);
        }
    }
}
