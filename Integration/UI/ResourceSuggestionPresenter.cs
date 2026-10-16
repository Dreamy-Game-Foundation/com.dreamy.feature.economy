using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    public sealed class ResourceSuggestionPresenter : IInsufficientResourcesPresentation, IDisposable
    {
        private readonly ResourceId resource;
        private readonly IResourceBalanceSource balances;
        private readonly IResourceSuggestionView view;
        private readonly ResourceAcquisitionCoordinator acquisition;
        private CancellationTokenSource session;
        private UniTaskCompletionSource<bool> choice;
        private long required;
        private bool disposed;

        public ResourceSuggestionPresenter(ResourceId resource, IResourceBalanceSource balances,
            IResourceSuggestionView view, ResourceAcquisitionCoordinator acquisition)
        {
            if (!resource.IsValid) throw new ArgumentException("A resource is required.", nameof(resource));
            this.resource = resource;
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
        }

        public async UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs,
            string contextId, CancellationToken token)
        {
            if (disposed) return ResourceRecoveryStatus.PresentationUnavailable;
            if (token.IsCancellationRequested) return ResourceRecoveryStatus.Cancelled;
            if (session != null) return ResourceRecoveryStatus.Busy;
            var normalized = Normalize(costs);
            normalized.TryGetValue(resource, out required);
            if (required == 0 || balances.GetBalance(resource) >= required) return ResourceRecoveryStatus.None;
            using var owner = CancellationTokenSource.CreateLinkedTokenSource(token);
            session = owner;
            choice = new UniTaskCompletionSource<bool>();
            view.AcquireRequested += Acquire;
            view.LaterRequested += Later;
            balances.BalanceChanged += BalanceChanged;
            bool listening = true;
            try
            {
                view.Render(State());
                view.Open();
                bool acquire = await choice.Task.AttachExternalCancellation(owner.Token);
                // Hide/Back/Destroy callbacks during the deliberate close cannot cancel navigation.
                Detach(); listening = false;
                await view.CloseAsync();
                owner.Token.ThrowIfCancellationRequested();
                if (!acquire) return ResourceRecoveryStatus.Cancelled;
                var deficits = new List<ResourceDeficit>();
                foreach (var cost in normalized)
                {
                    long available = balances.GetBalance(cost.Key);
                    if (available < cost.Value) deficits.Add(new ResourceDeficit(cost.Key, cost.Value, available));
                }
                long missing = required - balances.GetBalance(resource);
                if (missing <= 0) return ResourceRecoveryStatus.None;
                var result = await acquisition.OpenAsync(
                    new ResourceAcquisitionRequest(resource, missing, contextId, deficits), owner.Token);
                return result switch
                {
                    ResourceAcquisitionResult.Returned => ResourceRecoveryStatus.Returned,
                    ResourceAcquisitionResult.Cancelled => ResourceRecoveryStatus.Cancelled,
                    ResourceAcquisitionResult.Busy => ResourceRecoveryStatus.Busy,
                    _ => ResourceRecoveryStatus.Failed
                };
            }
            catch (OperationCanceledException) when (owner.IsCancellationRequested)
            { return ResourceRecoveryStatus.Cancelled; }
            finally
            {
                if (listening) Detach();
                choice = null;
                try { await view.CloseAsync(); }
                finally { session = null; }
            }
        }

        public void Dispose() { disposed = true; session?.Cancel(); }
        private ResourceSuggestionState State() => new(resource, required, balances.GetBalance(resource), acquisition.GetAvailability(resource));
        private void Acquire() { if (State().CanAcquire) choice?.TrySetResult(true); }
        private void Later() => choice?.TrySetResult(false);
        private void BalanceChanged(ResourceBalanceChanged _) { if (choice != null) view.Render(State()); }
        private void Detach()
        {
            view.AcquireRequested -= Acquire;
            view.LaterRequested -= Later;
            balances.BalanceChanged -= BalanceChanged;
        }
        private static Dictionary<ResourceId, long> Normalize(IReadOnlyList<ResourceAmount> costs)
        {
            if (costs == null || costs.Count == 0) throw new ArgumentException("Costs required.", nameof(costs));
            var totals = new Dictionary<ResourceId, long>();
            foreach (var cost in costs)
            {
                if (!cost.ResourceId.IsValid || cost.Amount <= 0) throw new ArgumentException("Invalid cost.", nameof(costs));
                totals.TryGetValue(cost.ResourceId, out long amount);
                totals[cost.ResourceId] = checked(amount + cost.Amount);
            }
            return totals;
        }
    }
}
