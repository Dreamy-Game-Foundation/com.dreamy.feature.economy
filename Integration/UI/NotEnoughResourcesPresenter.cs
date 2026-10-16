using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    // One instance per modal owner. All callbacks and balance events run on the owner thread.
    public sealed class NotEnoughResourcesPresenter : IInsufficientResourcesPresentation, IDisposable
    {
        private readonly IResourceBalanceSource balances;
        private readonly INotEnoughResourcesView view;
        private readonly ResourceAcquisitionCoordinator acquisition;
        private CancellationTokenSource session;
        private UniTaskCompletionSource<ResourceId> choice;
        private IReadOnlyList<ResourceAmount> costs;
        private bool opening;
        private bool disposed;
        private ResourceAcquisitionResult? lastNavigation;

        public NotEnoughResourcesPresenter(IResourceBalanceSource balances, INotEnoughResourcesView view,
            ResourceAcquisitionCoordinator acquisition)
        {
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
        }

        public async UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs, string contextId, CancellationToken token)
        {
            if (disposed) return ResourceRecoveryStatus.PresentationUnavailable;
            if (session != null) return ResourceRecoveryStatus.Busy;
            if (token.IsCancellationRequested) return ResourceRecoveryStatus.Cancelled;
            this.costs = CopyCosts(costs);
            session = CancellationTokenSource.CreateLinkedTokenSource(token);
            var sessionToken = session.Token;
            lastNavigation = null;
            view.AcquireRequested += Acquire;
            view.CancelRequested += Cancel;
            balances.BalanceChanged += BalanceChanged;
            try
            {
                view.Open();
                while (true)
                {
                    sessionToken.ThrowIfCancellationRequested();
                    choice = new UniTaskCompletionSource<ResourceId>();
                    opening = false;
                    Refresh();
                    ResourceId target = await choice.Task.AttachExternalCancellation(sessionToken);
                    sessionToken.ThrowIfCancellationRequested();
                    var deficits = GetDeficits();
                    foreach (var deficit in deficits)
                    {
                        if (deficit.ResourceId != target) continue;
                        opening = true;
                        Refresh();
                        lastNavigation = await acquisition.OpenAsync(
                            new ResourceAcquisitionRequest(target, deficit.Missing, contextId, deficits), sessionToken);
                        break;
                    }
                    // Re-read balances; the host must re-quote and confirm the action after closing this view.
                }
            }
            catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
            {
                if (token.IsCancellationRequested || disposed) return ResourceRecoveryStatus.Cancelled;
                return lastNavigation switch
                {
                    ResourceAcquisitionResult.Returned => ResourceRecoveryStatus.Returned,
                    ResourceAcquisitionResult.Busy => ResourceRecoveryStatus.Busy,
                    ResourceAcquisitionResult.Failed or ResourceAcquisitionResult.NoMatchingDestination or ResourceAcquisitionResult.Unsupported
                        => ResourceRecoveryStatus.Failed,
                    _ => ResourceRecoveryStatus.Cancelled
                };
            }
            finally
            {
                view.AcquireRequested -= Acquire;
                view.CancelRequested -= Cancel;
                balances.BalanceChanged -= BalanceChanged;
                choice = null;
                opening = false;
                session.Dispose();
                session = null;
                view.Close();
            }
        }

        public void Dispose() { disposed = true; session?.Cancel(); }
        private void Cancel() => session?.Cancel();
        private void Acquire(ResourceId id) { if (!opening) choice?.TrySetResult(id); }
        private void BalanceChanged(ResourceBalanceChanged _) { if (session != null && !session.IsCancellationRequested) Refresh(); }
        private void Refresh() => view.Render(GetDeficits(), acquisition.GetAvailability, opening, lastNavigation);

        private List<ResourceDeficit> GetDeficits()
        {
            var result = new List<ResourceDeficit>();
            foreach (var cost in costs)
            {
                long available = balances.GetBalance(cost.ResourceId);
                if (available < cost.Amount) result.Add(new ResourceDeficit(cost.ResourceId, cost.Amount, available));
            }
            return result;
        }

        private static IReadOnlyList<ResourceAmount> CopyCosts(IReadOnlyList<ResourceAmount> source)
        {
            // Reuse normalization semantics without exposing mutable internal dictionaries.
            if (source == null || source.Count == 0) throw new ArgumentException("Costs required.", nameof(source));
            var totals = new Dictionary<ResourceId, long>();
            foreach (var item in source)
            {
                if (!item.ResourceId.IsValid || item.Amount <= 0) throw new ArgumentException("Invalid cost.", nameof(source));
                totals.TryGetValue(item.ResourceId, out long current);
                totals[item.ResourceId] = checked(current + item.Amount);
            }
            var result = new List<ResourceAmount>();
            foreach (var pair in totals) result.Add(new ResourceAmount(pair.Key, pair.Value));
            return result.AsReadOnly();
        }
    }
}
