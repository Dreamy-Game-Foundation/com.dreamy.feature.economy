using System;

namespace Dreamy.Economy
{
    public readonly struct ResourceAmount
    {
        public ResourceAmount(ResourceId resourceId, long amount)
        {
            if (!resourceId.IsValid)
            {
                throw new ArgumentException("Resource ID must be valid.", nameof(resourceId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
            }

            ResourceId = resourceId;
            Amount = amount;
        }

        public ResourceId ResourceId { get; }
        public long Amount { get; }
    }
}
