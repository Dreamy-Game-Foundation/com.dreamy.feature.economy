using System;

namespace Dreamy.Economy
{
    public readonly struct ResourceDeficit
    {
        public ResourceDeficit(ResourceId resourceId, long required, long available)
        {
            if (!resourceId.IsValid) throw new ArgumentException("Invalid resource ID.", nameof(resourceId));
            if (required <= 0) throw new ArgumentOutOfRangeException(nameof(required));
            if (available < 0 || available >= required) throw new ArgumentOutOfRangeException(nameof(available));
            ResourceId = resourceId;
            Required = required;
            Available = available;
        }

        public ResourceId ResourceId { get; }
        public long Required { get; }
        public long Available { get; }
        public long Missing => Required - Available;
    }
}
