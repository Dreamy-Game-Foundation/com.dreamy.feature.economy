using System;
using System.Collections.Generic;

namespace Dreamy.Economy
{
    public readonly struct ResourceExchangeRequest
    {
        public ResourceExchangeRequest(
            string transactionId,
            ResourceAmount cost,
            IReadOnlyList<ResourceAmount> rewards)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                throw new ArgumentException("Transaction ID cannot be empty.", nameof(transactionId));
            }

            if (rewards == null || rewards.Count == 0)
            {
                throw new ArgumentException("At least one reward is required.", nameof(rewards));
            }

            TransactionId = transactionId;
            Cost = cost;
            Rewards = Copy(rewards);
        }

        public string TransactionId { get; }
        public ResourceAmount Cost { get; }
        public IReadOnlyList<ResourceAmount> Rewards { get; }

        private static IReadOnlyList<ResourceAmount> Copy(IReadOnlyList<ResourceAmount> rewards)
        {
            ResourceAmount[] copy = new ResourceAmount[rewards.Count];
            for (int index = 0; index < rewards.Count; index++)
            {
                copy[index] = rewards[index];
            }

            return copy;
        }
    }
}
