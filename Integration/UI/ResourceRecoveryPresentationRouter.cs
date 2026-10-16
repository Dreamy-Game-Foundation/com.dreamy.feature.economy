using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    /// <summary>Selects a game-owned presentation for the first mapped resource that is currently missing.</summary>
    public sealed class ResourceRecoveryPresentationRouter : IInsufficientResourcesPresentation
    {
        private readonly IResourceBalanceProvider balances;
        private readonly Dictionary<ResourceId, IInsufficientResourcesPresentation> presentations;
        private readonly IInsufficientResourcesPresentation fallback;
        private CancellationTokenSource activeSession;
        private UniTaskCompletionSource sessionFinished;

        public ResourceRecoveryPresentationRouter(IResourceBalanceProvider balances,
            IReadOnlyDictionary<ResourceId, IInsufficientResourcesPresentation> presentations,
            IInsufficientResourcesPresentation fallback = null)
        {
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
            if (presentations == null) throw new ArgumentNullException(nameof(presentations));
            this.presentations = new Dictionary<ResourceId, IInsufficientResourcesPresentation>();
            foreach (var pair in presentations)
            {
                if (!pair.Key.IsValid || pair.Value == null)
                    throw new ArgumentException("Each route requires a valid resource ID and presentation.", nameof(presentations));
                this.presentations.Add(pair.Key, pair.Value);
            }
            this.fallback = fallback;
        }

        // Like the wallet, calls belong to the owner thread. A new request supersedes the old panel,
        // including when the two requests select different game presentations.
        public async UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs,
            string contextId, CancellationToken token)
        {
            if (token.IsCancellationRequested) return ResourceRecoveryStatus.Cancelled;
            var normalized = Normalize(costs);
            var previous = sessionFinished;
            var completion = new UniTaskCompletionSource();
            sessionFinished = completion;
            using var session = CancellationTokenSource.CreateLinkedTokenSource(token);
            var previousSession = activeSession;
            activeSession = session;
            try
            {
                previousSession?.Cancel();
                if (previous != null) await previous.Task;
                session.Token.ThrowIfCancellationRequested();
                IInsufficientResourcesPresentation selected = null;
                bool hasDeficit = false;
                // Costs are normalized in first-occurrence order; funded resources never select a panel.
                foreach (var cost in normalized)
                {
                    if (balances.GetBalance(cost.ResourceId) >= cost.Amount) continue;
                    hasDeficit = true;
                    if (presentations.TryGetValue(cost.ResourceId, out selected)) break;
                }
                if (!hasDeficit) return ResourceRecoveryStatus.None;
                selected ??= fallback;
                if (selected == null) return ResourceRecoveryStatus.PresentationUnavailable;
                var result = await selected.ShowAsync(normalized, contextId, session.Token);
                return session.IsCancellationRequested ? ResourceRecoveryStatus.Cancelled : result;
            }
            catch (OperationCanceledException) when (session.IsCancellationRequested)
            { return ResourceRecoveryStatus.Cancelled; }
            finally
            {
                if (ReferenceEquals(activeSession, session)) activeSession = null;
                completion.TrySetResult();
            }
        }

        private static IReadOnlyList<ResourceAmount> Normalize(IReadOnlyList<ResourceAmount> costs)
        {
            if (costs == null || costs.Count == 0) throw new ArgumentException("Costs required.", nameof(costs));
            var result = new List<ResourceAmount>();
            var indices = new Dictionary<ResourceId, int>();
            foreach (var cost in costs)
            {
                if (!cost.ResourceId.IsValid || cost.Amount <= 0) throw new ArgumentException("Invalid cost.", nameof(costs));
                if (indices.TryGetValue(cost.ResourceId, out int index))
                    result[index] = new ResourceAmount(cost.ResourceId, checked(result[index].Amount + cost.Amount));
                else
                {
                    indices.Add(cost.ResourceId, result.Count);
                    result.Add(cost);
                }
            }
            return result.AsReadOnly();
        }
    }
}
