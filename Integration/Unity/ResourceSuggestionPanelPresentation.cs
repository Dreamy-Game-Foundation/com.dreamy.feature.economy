using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.UI;

namespace Dreamy.Economy.UI
{
    /// <summary>Owns the recovery session across popup disposal and acquisition navigation.</summary>
    public sealed class ResourceSuggestionPanelPresentation<TPanel> : IInsufficientResourcesPresentation
        where TPanel : ResourceSuggestionPanel
    {
        private readonly ResourceId resource;
        private readonly string address;
        private readonly IResourceBalanceSource balances;
        private readonly ResourceAcquisitionCoordinator acquisition;
        private bool busy;

        public ResourceSuggestionPanelPresentation(ResourceId resource, string address,
            IResourceBalanceSource balances, ResourceAcquisitionCoordinator acquisition)
        {
            if (!resource.IsValid) throw new ArgumentException("A resource is required.", nameof(resource));
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("A panel address is required.", nameof(address));
            this.resource = resource;
            this.address = address;
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
            this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
        }

        public async UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs, string contextId, CancellationToken token)
        {
            if (token.IsCancellationRequested) return ResourceRecoveryStatus.Cancelled;
            if (busy) return ResourceRecoveryStatus.Busy;
            var request = new ResourceSuggestionRequest(resource, costs, contextId);
            if (request.Required == 0 || balances.GetBalance(resource) >= request.Required) return ResourceRecoveryStatus.None;
            if (!PanelManager.HasInstance) return ResourceRecoveryStatus.PresentationUnavailable;
            busy = true;
            TPanel panel = null;
            try
            {
                panel = await PanelManager.Instance.Create<TPanel>(address);
                token.ThrowIfCancellationRequested();
                panel.Prepare(request);
                bool acquire;
                // Destruction cancels only the UI phase. Deliberate close must not cancel navigation.
                using (var ui = CancellationTokenSource.CreateLinkedTokenSource(token, panel.GetCancellationTokenOnDestroy()))
                {
                    await panel.Show();
                    acquire = await request.WaitForChoiceAsync(ui.Token);
                }
                await panel.CloseAsync();
                token.ThrowIfCancellationRequested();
                if (!acquire) return ResourceRecoveryStatus.Cancelled;
                long missing = request.Required - balances.GetBalance(resource);
                if (missing <= 0) return ResourceRecoveryStatus.None;
                var deficits = new List<ResourceDeficit>();
                foreach (var cost in request.Costs)
                {
                    long available = balances.GetBalance(cost.ResourceId);
                    if (available < cost.Amount) deficits.Add(new ResourceDeficit(cost.ResourceId, cost.Amount, available));
                }
                var result = await acquisition.OpenAsync(
                    new ResourceAcquisitionRequest(resource, missing, request.ContextId, deficits), token);
                return result switch
                {
                    ResourceAcquisitionResult.Returned => ResourceRecoveryStatus.Returned,
                    ResourceAcquisitionResult.Cancelled => ResourceRecoveryStatus.Cancelled,
                    ResourceAcquisitionResult.Busy => ResourceRecoveryStatus.Busy,
                    _ => ResourceRecoveryStatus.Failed
                };
            }
            catch (OperationCanceledException) { return ResourceRecoveryStatus.Cancelled; }
            finally
            {
                try { if (panel != null) await panel.CloseAsync(); }
                finally { busy = false; }
            }
        }
    }
}
