using System;

namespace Dreamy.Economy
{
    public interface IResourceBalanceSource : IResourceBalanceProvider
    {
        event Action<ResourceBalanceChanged> BalanceChanged;
    }

    public readonly struct ResourceBalanceChanged
    {
        public ResourceBalanceChanged(ResourceId resourceId, long balance)
        {
            ResourceId = resourceId;
            Balance = balance;
        }

        public ResourceId ResourceId { get; }
        public long Balance { get; }
    }
}
