using System.Collections.Generic;

namespace Dreamy.Economy
{
    public readonly struct ResourceSpendRequest
    {
        public ResourceSpendRequest(string transactionId, IReadOnlyList<ResourceAmount> costs, string contextId = null)
        {
            TransactionId = transactionId;
            Costs = ResourceTransactionPayload.Copy(costs);
            ContextId = contextId;
        }

        public string TransactionId { get; }
        public IReadOnlyList<ResourceAmount> Costs { get; }
        public string ContextId { get; }
    }
}
