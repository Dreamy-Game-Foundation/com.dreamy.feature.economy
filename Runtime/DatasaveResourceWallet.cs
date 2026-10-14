using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;

namespace Dreamy.Economy
{
    public sealed class DatasaveResourceWallet : IResourceWallet, IResourceBalanceSource, IRewardDestination
    {
        private const int DefaultTransactionHistoryLimit = 4096;
        public const string CurrencyKind = "currency";

        private readonly IDatasaveService datasave;
        private readonly string saveKey;
        private readonly int transactionHistoryLimit;
        private EconomySaveData data;
        private Dictionary<ResourceId, long> balances = new();
        private readonly HashSet<string> transactionIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> legacyTombstones = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> receipts = new(StringComparer.Ordinal);
        private bool busy;

        public DatasaveResourceWallet(
            IDatasaveService datasave,
            string saveKey = "economy",
            IReadOnlyList<ResourceAmount> initialBalances = null,
            int transactionHistoryLimit = DefaultTransactionHistoryLimit)
        {
            this.datasave = datasave ?? throw new ArgumentNullException(nameof(datasave));
            if (string.IsNullOrWhiteSpace(saveKey)) throw new ArgumentException("Save key cannot be empty.", nameof(saveKey));
            if (transactionHistoryLimit <= 0) throw new ArgumentOutOfRangeException(nameof(transactionHistoryLimit));
            this.saveKey = saveKey;
            this.transactionHistoryLimit = transactionHistoryLimit;
            bool exists = datasave.Exists(saveKey);
            data = datasave.Load<EconomySaveData>(saveKey);
            data.OnAfterLoad();
            foreach (ResourceBalanceSaveEntry entry in data.Balances)
                if (entry != null && entry.Amount >= 0 && ResourceId.TryParse(entry.ResourceId, out ResourceId id))
                    balances[id] = entry.Amount;
            foreach (string id in data.TransactionIds)
                if (!string.IsNullOrWhiteSpace(id))
                {
                    transactionIds.Add(id);
                    legacyTombstones.Add(id);
                }
            foreach (string id in data.LegacyDeliveryTombstones)
                if (!string.IsNullOrWhiteSpace(id)) legacyTombstones.Add(id);
            foreach (RewardReceiptSaveEntry receipt in data.DeliveryReceipts)
            {
                // Corrupt strict ledgers must not be silently dropped and regranted.
                if (receipt == null || string.IsNullOrWhiteSpace(receipt.TransactionId) ||
                    string.IsNullOrWhiteSpace(receipt.Fingerprint) || receipts.ContainsKey(receipt.TransactionId))
                    throw new InvalidOperationException("Invalid durable reward receipt ledger.");
                receipts.Add(receipt.TransactionId, receipt.Fingerprint);
            }
            if (!exists && initialBalances != null)
            {
                var initial = new Dictionary<ResourceId, long>();
                foreach (ResourceAmount amount in initialBalances)
                {
                    if (!IsValid(amount)) throw new ArgumentException("Initial balances must be valid.", nameof(initialBalances));
                    initial[amount.ResourceId] = amount.Amount;
                }
                Commit(CreateCandidate(initial), initial);
            }
        }

        public event Action<ResourceBalanceChanged> BalanceChanged;
        public string KindId => CurrencyKind;
        public long GetBalance(ResourceId resourceId) => balances.TryGetValue(resourceId, out long amount) ? amount : 0;

        public bool TryGrant(ResourceGrantRequest request)
        {
            if (busy || string.IsNullOrWhiteSpace(request.TransactionId) || !IsValid(request.Resource)) return false;
            if (HasTransaction(request.TransactionId)) return true;
            busy = true;
            try
            {
                var next = new Dictionary<ResourceId, long>(balances);
                try { Add(next, request.Resource); }
                catch (OverflowException) { return false; }
                EconomySaveData candidate = CreateCandidate(next);
                AddLegacyTransaction(candidate, request.TransactionId);
                Commit(candidate, next);
                NotifyBalanceChanged(request.Resource.ResourceId);
                return true;
            }
            finally { busy = false; }
        }

        public bool TryExchange(ResourceExchangeRequest request)
        {
            if (busy || string.IsNullOrWhiteSpace(request.TransactionId) || !IsValid(request.Cost) ||
                request.Rewards == null || request.Rewards.Count == 0) return false;
            foreach (ResourceAmount reward in request.Rewards)
                if (!IsValid(reward)) return false;
            if (HasTransaction(request.TransactionId)) return true;
            if (GetBalance(request.Cost.ResourceId) < request.Cost.Amount) return false;
            busy = true;
            try
            {
                var next = new Dictionary<ResourceId, long>(balances);
                next[request.Cost.ResourceId] = checked(GetBalance(request.Cost.ResourceId) - request.Cost.Amount);
                try
                {
                    foreach (ResourceAmount reward in request.Rewards) Add(next, reward);
                }
                catch (OverflowException) { return false; }
                EconomySaveData candidate = CreateCandidate(next);
                AddLegacyTransaction(candidate, request.TransactionId);
                Commit(candidate, next);
                var changed = new HashSet<ResourceId> { request.Cost.ResourceId };
                foreach (ResourceAmount reward in request.Rewards) changed.Add(reward.ResourceId);
                foreach (ResourceId id in changed) NotifyBalanceChanged(id);
                return true;
            }
            finally { busy = false; }
        }

        public RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            return ValidateCore(requests);
        }

        private RewardDeliveryResult ValidateCore(IReadOnlyList<RewardDeliveryRequest> requests)
        {
            if (requests == null || requests.Count == 0) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
            var next = new Dictionary<ResourceId, long>(balances);
            var batchIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RewardDeliveryRequest request in requests)
            {
                if (!request.IsValid || !batchIds.Add(request.TransactionId))
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
                if (request.Entry.KindId != CurrencyKind) return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownKind);
                if (!ResourceId.TryParse(request.Entry.TargetId, out ResourceId id))
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownTarget);
                if (receipts.TryGetValue(request.TransactionId, out string fingerprint))
                {
                    if (!string.Equals(fingerprint, request.Fingerprint, StringComparison.Ordinal))
                        return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                    continue;
                }
                if (legacyTombstones.Contains(request.TransactionId))
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.LegacyReceiptUnverifiable);
                try { Add(next, new ResourceAmount(id, request.Entry.Amount)); }
                catch (OverflowException) { return RewardDeliveryResult.Failed(RewardDeliveryReason.Overflow); }
            }
            return RewardDeliveryResult.Delivered();
        }

        public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            busy = true;
            try
            {
                RewardDeliveryResult validation = ValidateCore(new[] { request });
                if (!validation.IsSuccess) return validation;
                if (receipts.ContainsKey(request.TransactionId)) return RewardDeliveryResult.AlreadyDelivered();
                var next = new Dictionary<ResourceId, long>(balances);
                var id = new ResourceId(request.Entry.TargetId);
                Add(next, new ResourceAmount(id, request.Entry.Amount));
                EconomySaveData candidate = CreateCandidate(next);
                candidate.DeliveryReceipts.Add(new RewardReceiptSaveEntry
                {
                    TransactionId = request.TransactionId,
                    Fingerprint = request.Fingerprint
                });
                try { Commit(candidate, next); }
                catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed); }
                NotifyBalanceChanged(id);
                return RewardDeliveryResult.Delivered();
            }
            finally { busy = false; }
        }

        private static bool IsValid(ResourceAmount amount) => amount.ResourceId.IsValid && amount.Amount > 0;
        private bool HasTransaction(string id) => transactionIds.Contains(id) || receipts.ContainsKey(id);

        private static void Add(Dictionary<ResourceId, long> target, ResourceAmount amount)
        {
            target.TryGetValue(amount.ResourceId, out long current);
            target[amount.ResourceId] = checked(current + amount.Amount);
        }

        private EconomySaveData CreateCandidate(Dictionary<ResourceId, long> next)
        {
            var candidate = new EconomySaveData();
            foreach (KeyValuePair<ResourceId, long> pair in next)
                candidate.Balances.Add(new ResourceBalanceSaveEntry { ResourceId = pair.Key.Value, Amount = pair.Value });
            candidate.TransactionIds.AddRange(data.TransactionIds);
            candidate.LegacyDeliveryTombstones.AddRange(legacyTombstones);
            foreach (RewardReceiptSaveEntry receipt in data.DeliveryReceipts)
                candidate.DeliveryReceipts.Add(new RewardReceiptSaveEntry { TransactionId = receipt.TransactionId, Fingerprint = receipt.Fingerprint });
            return candidate;
        }

        private void AddLegacyTransaction(EconomySaveData candidate, string id)
        {
            candidate.TransactionIds.Add(id);
            if (!legacyTombstones.Contains(id)) candidate.LegacyDeliveryTombstones.Add(id);
            int remove = candidate.TransactionIds.Count - transactionHistoryLimit;
            if (remove > 0) candidate.TransactionIds.RemoveRange(0, remove);
        }

        private void Commit(EconomySaveData candidate, Dictionary<ResourceId, long> next)
        {
            datasave.Save(candidate, saveKey);
            data = candidate;
            balances = next;
            transactionIds.Clear();
            foreach (string id in data.TransactionIds) transactionIds.Add(id);
            legacyTombstones.Clear();
            foreach (string id in data.LegacyDeliveryTombstones) legacyTombstones.Add(id);
            receipts.Clear();
            foreach (RewardReceiptSaveEntry receipt in data.DeliveryReceipts)
                receipts.Add(receipt.TransactionId, receipt.Fingerprint);
        }

        private void NotifyBalanceChanged(ResourceId resourceId) =>
            BalanceChanged?.Invoke(new ResourceBalanceChanged(resourceId, GetBalance(resourceId)));
    }
}
