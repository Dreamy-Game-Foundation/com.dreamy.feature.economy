using System;

namespace Dreamy.Economy
{
    public readonly struct ResourceGrantRequest
    {
        public ResourceGrantRequest(string transactionId, ResourceAmount resource)
        {
            TransactionId = string.IsNullOrWhiteSpace(transactionId)
                ? throw new ArgumentException("Transaction ID cannot be empty.", nameof(transactionId))
                : transactionId;
            Resource = resource;
        }

        public string TransactionId { get; }
        public ResourceAmount Resource { get; }
    }
}
