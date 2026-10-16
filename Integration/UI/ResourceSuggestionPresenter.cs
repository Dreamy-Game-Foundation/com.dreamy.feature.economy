using System;
using Dreamy.UI;

namespace Dreamy.Economy.UI
{
    /// <summary>Render/intent owner for one panel opening. Navigation belongs to the presentation session.</summary>
    public sealed class ResourceSuggestionPresenter : IPanelPresenter
    {
        private readonly ResourceSuggestionRequest request;
        private readonly IResourceBalanceSource balances;
        private readonly IResourceSuggestionView view;
        private readonly ResourceAcquisitionCoordinator acquisition;
        private bool bound;

        public ResourceSuggestionPresenter(ResourceSuggestionRequest request, IResourceBalanceSource balances,
            IResourceSuggestionView view, ResourceAcquisitionCoordinator acquisition)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
        }

        public void Show()
        {
            if (bound) return;
            bound = true;
            view.AcquireRequested += Acquire;
            view.LaterRequested += Later;
            balances.BalanceChanged += BalanceChanged;
            view.Render(State());
        }

        public void Dispose()
        {
            if (!bound) return;
            bound = false;
            view.AcquireRequested -= Acquire;
            view.LaterRequested -= Later;
            balances.BalanceChanged -= BalanceChanged;
            // Back/disable/destroy ends an unanswered request, but cannot overwrite Acquire.
            request.Choose(false);
        }

        private ResourceSuggestionState State() => new(request.ResourceId, request.Required,
            balances.GetBalance(request.ResourceId), acquisition.GetAvailability(request.ResourceId));
        private void Acquire() { if (!request.HasChoice && State().CanAcquire) request.Choose(true); }
        private void Later() => request.Choose(false);
        private void BalanceChanged(ResourceBalanceChanged change)
        {
            if (!request.HasChoice) view.Render(State());
        }
    }
}
