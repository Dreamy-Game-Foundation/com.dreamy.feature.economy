using System;
using System.Collections.Generic;
using Dreamy.Datasave;

namespace Dreamy.Economy
{
    public sealed class DatasaveResourceWallet : IResourceWallet, IResourceBalanceSource
    {
        private const int DefaultTransactionHistoryLimit = 4096;

        private readonly IDatasaveService datasave;
        private readonly string saveKey;
        private readonly int transactionHistoryLimit;
        private readonly EconomySaveData data;
        private readonly Dictionary<ResourceId, long> balances = new();
        private readonly HashSet<string> transactionIds = new(StringComparer.Ordinal);
        private readonly List<string> transactionHistory = new();

        public DatasaveResourceWallet(
            IDatasaveService datasave,
            string saveKey = "economy",
            IReadOnlyList<ResourceAmount> initialBalances = null,
            int transactionHistoryLimit = DefaultTransactionHistoryLimit)
        {
            this.datasave = datasave ?? throw new ArgumentNullException(nameof(datasave));
            if (string.IsNullOrWhiteSpace(saveKey))
            {
                throw new ArgumentException("Save key cannot be empty.", nameof(saveKey));
            }

            if (transactionHistoryLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(transactionHistoryLimit));
            }

            this.saveKey = saveKey;
            this.transactionHistoryLimit = transactionHistoryLimit;
            bool hasExistingSave = datasave.Exists(saveKey);
            data = datasave.Load<EconomySaveData>(saveKey);
            LoadBalances();
            LoadTransactions();

            if (!hasExistingSave && initialBalances != null)
            {
                foreach (ResourceAmount balance in initialBalances)
                {
                    balances[balance.ResourceId] = balance.Amount;
                }

                Save();
            }
        }

        public event Action<ResourceBalanceChanged> BalanceChanged;

        public long GetBalance(ResourceId resourceId) =>
            balances.TryGetValue(resourceId, out long value) ? value : 0;

        public bool TryGrant(ResourceGrantRequest request)
        {
            if (!AddTransaction(request.TransactionId))
            {
                return true;
            }

            AddBalance(request.Resource);
            Save();
            NotifyBalanceChanged(request.Resource.ResourceId);
            return true;
        }

        public bool TryExchange(ResourceExchangeRequest request)
        {
            if (transactionIds.Contains(request.TransactionId))
            {
                return true;
            }

            if (GetBalance(request.Cost.ResourceId) < request.Cost.Amount)
            {
                return false;
            }

            balances[request.Cost.ResourceId] = GetBalance(request.Cost.ResourceId) - request.Cost.Amount;
            foreach (ResourceAmount reward in request.Rewards)
            {
                AddBalance(reward);
            }

            AddTransaction(request.TransactionId);
            Save();
            NotifyBalanceChanged(request.Cost.ResourceId);
            foreach (ResourceAmount reward in request.Rewards)
            {
                NotifyBalanceChanged(reward.ResourceId);
            }

            return true;
        }

        private void LoadBalances()
        {
            foreach (ResourceBalanceSaveEntry entry in data.Balances)
            {
                if (entry == null || entry.Amount < 0 || !ResourceId.TryParse(entry.ResourceId, out ResourceId resourceId))
                {
                    continue;
                }

                balances[resourceId] = entry.Amount;
            }
        }

        private void LoadTransactions()
        {
            foreach (string transactionId in data.TransactionIds)
            {
                if (!string.IsNullOrWhiteSpace(transactionId))
                {
                    AddTransaction(transactionId);
                }
            }
        }

        private void AddBalance(ResourceAmount amount)
        {
            balances[amount.ResourceId] = GetBalance(amount.ResourceId) + amount.Amount;
        }

        private bool AddTransaction(string transactionId)
        {
            if (!transactionIds.Add(transactionId))
            {
                return false;
            }

            transactionHistory.Add(transactionId);
            return true;
        }

        private void Save()
        {
            data.Balances.Clear();
            foreach (KeyValuePair<ResourceId, long> pair in balances)
            {
                data.Balances.Add(new ResourceBalanceSaveEntry
                {
                    ResourceId = pair.Key.Value,
                    Amount = pair.Value
                });
            }

            data.TransactionIds.Clear();
            int firstRetainedIndex = Math.Max(0, transactionHistory.Count - transactionHistoryLimit);
            for (int index = firstRetainedIndex; index < transactionHistory.Count; index++)
            {
                data.TransactionIds.Add(transactionHistory[index]);
            }

            if (firstRetainedIndex > 0)
            {
                transactionIds.Clear();
                transactionHistory.Clear();
                foreach (string transactionId in data.TransactionIds)
                {
                    AddTransaction(transactionId);
                }
            }

            datasave.Save(data, saveKey);
        }

        private void NotifyBalanceChanged(ResourceId resourceId) =>
            BalanceChanged?.Invoke(new ResourceBalanceChanged(resourceId, GetBalance(resourceId)));
    }
}
