using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    /// <summary>One opening's immutable costs and one-shot choice; outlives the panel's presenter.</summary>
    public sealed class ResourceSuggestionRequest
    {
        private readonly UniTaskCompletionSource<bool> choice = new();
        public ResourceId ResourceId { get; }
        public IReadOnlyList<ResourceAmount> Costs { get; }
        public string ContextId { get; }
        public long Required { get; }
        public bool HasChoice { get; private set; }

        public ResourceSuggestionRequest(ResourceId resource, IReadOnlyList<ResourceAmount> costs, string contextId)
        {
            if (!resource.IsValid) throw new ArgumentException("A resource is required.", nameof(resource));
            if (costs == null || costs.Count == 0) throw new ArgumentException("Costs required.", nameof(costs));
            ResourceId = resource;
            ContextId = contextId;
            var normalized = new List<ResourceAmount>();
            var indices = new Dictionary<ResourceId, int>();
            foreach (var cost in costs)
            {
                if (!cost.ResourceId.IsValid || cost.Amount <= 0) throw new ArgumentException("Invalid cost.", nameof(costs));
                if (indices.TryGetValue(cost.ResourceId, out int index))
                    normalized[index] = new ResourceAmount(cost.ResourceId, checked(normalized[index].Amount + cost.Amount));
                else { indices.Add(cost.ResourceId, normalized.Count); normalized.Add(cost); }
            }
            Costs = normalized.AsReadOnly();
            if (indices.TryGetValue(resource, out int selected)) Required = normalized[selected].Amount;
        }

        public UniTask<bool> WaitForChoiceAsync(CancellationToken token) => choice.Task.AttachExternalCancellation(token);
        internal void Choose(bool acquire)
        {
            if (HasChoice) return;
            HasChoice = true;
            choice.TrySetResult(acquire);
        }
    }
}
