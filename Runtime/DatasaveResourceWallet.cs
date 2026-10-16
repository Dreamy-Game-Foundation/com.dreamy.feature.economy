using System;
using System.Collections.Generic;
using Dreamy.Datasave;
using Dreamy.Economy.Rewards;

namespace Dreamy.Economy
{
    public sealed partial class DatasaveResourceWallet : IResourceWallet, IResourceBalanceSource, IRewardDestination, IResourceTransactionWallet
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
        private readonly Dictionary<string, ResourceTransactionReceipt> transactionReceipts = new(StringComparer.Ordinal);
        private readonly Dictionary<ResourceId, ResourceRewardDefinition> rewardDefinitions = new();
        private bool persistenceUncertain;
        private bool busy;

        // Optional diagnostics boundary; diagnostic listeners cannot invalidate a committed transaction.
        public event Action<Exception> NotificationFailed;

        public DatasaveResourceWallet(
            IDatasaveService datasave,
            string saveKey = "economy",
            IReadOnlyList<ResourceAmount> initialBalances = null,
            int transactionHistoryLimit = DefaultTransactionHistoryLimit,
            IReadOnlyList<ResourceRewardDefinition> rewardDefinitions = null)
        {
            this.datasave = datasave ?? throw new ArgumentNullException(nameof(datasave));
            if (string.IsNullOrWhiteSpace(saveKey)) throw new ArgumentException("Save key cannot be empty.", nameof(saveKey));
            if (transactionHistoryLimit <= 0) throw new ArgumentOutOfRangeException(nameof(transactionHistoryLimit));
            this.saveKey = saveKey;
            this.transactionHistoryLimit = transactionHistoryLimit;
            if (rewardDefinitions != null)
                foreach (var definition in rewardDefinitions)
                {
                    if (definition == null || this.rewardDefinitions.ContainsKey(definition.TargetId))
                        throw new ArgumentException("Reward target definitions must be non-null and unique.", nameof(rewardDefinitions));
                    this.rewardDefinitions.Add(definition.TargetId, definition);
                }
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
            foreach (ResourceTransactionReceipt receipt in data.TransactionReceipts)
            {
                if (receipt == null || receipt.SchemaVersion != 1 || string.IsNullOrWhiteSpace(receipt.TransactionId) ||
                    (receipt.Kind != "spend" && receipt.Kind != "exchange") ||
                    !ResourceTransactionPayload.IsValidFingerprint(receipt.Kind, receipt.Fingerprint) ||
                    transactionReceipts.ContainsKey(receipt.TransactionId) || receipts.ContainsKey(receipt.TransactionId) ||
                    legacyTombstones.Contains(receipt.TransactionId))
                    throw new InvalidOperationException("Invalid durable transaction receipt ledger.");
                transactionReceipts.Add(receipt.TransactionId, receipt);
            }
            ValidateInventoryImports(data);
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
            if (busy || persistenceUncertain || string.IsNullOrWhiteSpace(request.TransactionId) || !IsValid(request.Resource)) return false;
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
            if (busy || persistenceUncertain || string.IsNullOrWhiteSpace(request.TransactionId) || !IsValid(request.Cost) ||
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
            if (persistenceUncertain) return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed);
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
                if (!ResourceId.TryParse(request.Entry.TargetId, out ResourceId id))
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownTarget);
                if (rewardDefinitions.TryGetValue(id, out var definition))
                {
                    if (definition.KindId != request.Entry.KindId) return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownKind);
                }
                else if (request.Entry.KindId != CurrencyKind)
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownTarget);
                if (receipts.TryGetValue(request.TransactionId, out string fingerprint))
                {
                    if (!string.Equals(fingerprint, request.Fingerprint, StringComparison.Ordinal))
                        return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                    continue;
                }
                if (transactionReceipts.ContainsKey(request.TransactionId))
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                if (legacyTombstones.Contains(request.TransactionId))
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.LegacyReceiptUnverifiable);
                try { Add(next, new ResourceAmount(id, request.Entry.Amount)); }
                catch (OverflowException) { return RewardDeliveryResult.Failed(RewardDeliveryReason.Overflow); }
            }
            return RewardDeliveryResult.Delivered();
        }

        public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
        {
            if (persistenceUncertain) return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed);
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

        public ResourceTransactionResult Spend(ResourceSpendRequest request) =>
            Transact(request.TransactionId, request.ContextId, request.Costs, null, "spend");

        public ResourceTransactionResult Exchange(ResourceTransactionRequest request) =>
            Transact(request.TransactionId, request.ContextId, request.Costs, request.Rewards, "exchange");

        private ResourceTransactionResult Transact(string id, string context, IReadOnlyList<ResourceAmount> costs,
            IReadOnlyList<ResourceAmount> rewards, string kind)
        {
            ResourceTransactionResult Result(ResourceTransactionStatus status, IReadOnlyList<ResourceDeficit> deficits = null,
                string error = null) => new(status, id, deficits, error);
            if (persistenceUncertain) return Result(ResourceTransactionStatus.PersistenceUncertain);
            if (busy) return Result(ResourceTransactionStatus.Busy);
            if (string.IsNullOrWhiteSpace(id)) return Result(ResourceTransactionStatus.InvalidRequest);
            SortedDictionary<string, long> normalizedCosts;
            SortedDictionary<string, long> normalizedRewards;
            string fingerprint;
            try
            {
                normalizedCosts = ResourceTransactionPayload.Normalize(costs);
                normalizedRewards = kind == "spend" ? null : ResourceTransactionPayload.Normalize(rewards);
                fingerprint = ResourceTransactionPayload.Fingerprint(kind, context, normalizedCosts, normalizedRewards);
            }
            catch (OverflowException) { return Result(ResourceTransactionStatus.Overflow); }
            catch (ArgumentException) { return Result(ResourceTransactionStatus.InvalidRequest); }
            busy = true;
            try
            {
                if (transactionReceipts.TryGetValue(id, out ResourceTransactionReceipt receipt))
                    return Result(receipt.Fingerprint == fingerprint ? ResourceTransactionStatus.AlreadyCommitted : ResourceTransactionStatus.PayloadMismatch);
                if (receipts.ContainsKey(id)) return Result(ResourceTransactionStatus.PayloadMismatch);
                if (legacyTombstones.Contains(id)) return Result(ResourceTransactionStatus.LegacyReceiptUnverifiable);
                var deficits = new List<ResourceDeficit>();
                foreach (var cost in normalizedCosts)
                {
                    var resource = new ResourceId(cost.Key);
                    long available = GetBalance(resource);
                    if (available < cost.Value) deficits.Add(new ResourceDeficit(resource, cost.Value, available));
                }
                if (deficits.Count > 0) return Result(ResourceTransactionStatus.InsufficientResources, deficits);
                var previous = balances;
                var next = new Dictionary<ResourceId, long>(balances);
                foreach (var cost in normalizedCosts)
                {
                    var resource = new ResourceId(cost.Key);
                    next[resource] -= cost.Value;
                }
                try
                {
                    if (normalizedRewards != null)
                        foreach (var reward in normalizedRewards) Add(next, new ResourceAmount(new ResourceId(reward.Key), reward.Value));
                }
                catch (OverflowException) { return Result(ResourceTransactionStatus.Overflow); }
                var candidate = CreateCandidate(next);
                candidate.TransactionReceipts.Add(new ResourceTransactionReceipt
                { TransactionId = id, Kind = kind, Fingerprint = fingerprint });
                try { Commit(candidate, next); }
                catch (Exception exception)
                {
                    // IDatasaveService alone cannot prove that a thrown Save did not commit.
                    // Reload durable state before accepting another mutation. Load must bypass stale caches.
                    try
                    {
                        var restored = new DatasaveResourceWallet(datasave, saveKey, transactionHistoryLimit: transactionHistoryLimit, rewardDefinitions: new List<ResourceRewardDefinition>(rewardDefinitions.Values));
                        Publish(restored.data, restored.balances);
                        if (!transactionReceipts.TryGetValue(id, out var recovered))
                            return Result(ResourceTransactionStatus.PersistenceFailed, error: exception.GetType().Name);
                        if (recovered.Fingerprint != fingerprint)
                        {
                            persistenceUncertain = true;
                            return Result(ResourceTransactionStatus.PersistenceUncertain, error: "ReconciliationMismatch");
                        }
                    }
                    catch (Exception reloadException)
                    {
                        persistenceUncertain = true;
                        return Result(ResourceTransactionStatus.PersistenceUncertain, error: reloadException.GetType().Name);
                    }
                }
                foreach (var pair in balances)
                    if (!previous.TryGetValue(pair.Key, out long old) || old != pair.Value)
                        NotifyCommittedBalanceChanged(pair.Key);
                return Result(ResourceTransactionStatus.Committed);
            }
            finally { busy = false; }
        }

        private void NotifyCommittedBalanceChanged(ResourceId id)
        {
            var handlers = BalanceChanged;
            if (handlers == null) return;
            foreach (Action<ResourceBalanceChanged> handler in handlers.GetInvocationList())
            {
                try { handler(new ResourceBalanceChanged(id, GetBalance(id))); }
                catch (Exception exception)
                {
                    try { NotificationFailed?.Invoke(exception); }
                    catch (Exception) { /* Diagnostics cannot change commit outcome. */ }
                }
            }
        }

        private static bool IsValid(ResourceAmount amount) => amount.ResourceId.IsValid && amount.Amount > 0;
        private bool HasTransaction(string id) => transactionIds.Contains(id) || receipts.ContainsKey(id) || transactionReceipts.ContainsKey(id);

        private void Add(Dictionary<ResourceId, long> target, ResourceAmount amount)
        {
            target.TryGetValue(amount.ResourceId, out long current);
            target[amount.ResourceId] = rewardDefinitions.TryGetValue(amount.ResourceId, out var definition) && definition.Unique
                ? 1 : checked(current + amount.Amount);
        }

        private EconomySaveData CreateCandidate(Dictionary<ResourceId, long> next)
        {
            var candidate = new EconomySaveData();
            foreach (KeyValuePair<ResourceId, long> pair in next)
                candidate.Balances.Add(new ResourceBalanceSaveEntry { ResourceId = pair.Key.Value, Amount = pair.Value });
            foreach (var import in data.InventoryImports)
                candidate.InventoryImports.Add(new RewardReceiptSaveEntry { TransactionId = import.TransactionId, Fingerprint = import.Fingerprint });
            candidate.TransactionIds.AddRange(data.TransactionIds);
            candidate.LegacyDeliveryTombstones.AddRange(legacyTombstones);
            foreach (RewardReceiptSaveEntry receipt in data.DeliveryReceipts)
                candidate.DeliveryReceipts.Add(new RewardReceiptSaveEntry { TransactionId = receipt.TransactionId, Fingerprint = receipt.Fingerprint });
            foreach (ResourceTransactionReceipt receipt in data.TransactionReceipts)
                candidate.TransactionReceipts.Add(new ResourceTransactionReceipt
                {
                    SchemaVersion = receipt.SchemaVersion, TransactionId = receipt.TransactionId,
                    Kind = receipt.Kind, Fingerprint = receipt.Fingerprint
                });
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
            try { datasave.Save(candidate, saveKey); }
            catch (Exception) { persistenceUncertain = true; throw; }
            Publish(candidate, next);
        }

        private void Publish(EconomySaveData candidate, Dictionary<ResourceId, long> next)
        {
            data = candidate;
            balances = next;
            persistenceUncertain = false;
            transactionReceipts.Clear();
            foreach (ResourceTransactionReceipt receipt in data.TransactionReceipts)
                transactionReceipts.Add(receipt.TransactionId, receipt);
            transactionIds.Clear();
            foreach (string id in data.TransactionIds) transactionIds.Add(id);
            legacyTombstones.Clear();
            foreach (string id in data.LegacyDeliveryTombstones) legacyTombstones.Add(id);
            receipts.Clear();
            foreach (RewardReceiptSaveEntry receipt in data.DeliveryReceipts)
                receipts.Add(receipt.TransactionId, receipt.Fingerprint);
        }

        private void NotifyBalanceChanged(ResourceId resourceId) => NotifyCommittedBalanceChanged(resourceId);
    }
}
