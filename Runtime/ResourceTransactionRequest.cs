using System.Collections.Generic;

namespace Dreamy.Economy
{
    public readonly struct ResourceTransactionRequest
    {
        public ResourceTransactionRequest(string transactionId, IReadOnlyList<ResourceAmount> costs,
            IReadOnlyList<ResourceAmount> rewards, string contextId = null)
        {
            TransactionId = transactionId;
            Costs = ResourceTransactionPayload.Copy(costs);
            Rewards = ResourceTransactionPayload.Copy(rewards);
            ContextId = contextId;
        }

        public string TransactionId { get; }
        public IReadOnlyList<ResourceAmount> Costs { get; }
        public IReadOnlyList<ResourceAmount> Rewards { get; }
        public string ContextId { get; }
    }
}
