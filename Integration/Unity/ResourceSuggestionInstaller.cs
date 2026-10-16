using System;
using Dreamy.UI;

namespace Dreamy.Economy.UI
{
    public static class ResourceSuggestionInstaller
    {
        /// <summary>Register one concrete view type per resource, as required by PanelManager's type identity.</summary>
        public static IInsufficientResourcesPresentation Install<TPanel>(PanelPresenterFactory factory,
            ResourceId resource, string address, IResourceBalanceSource balances, ResourceAcquisitionCoordinator acquisition)
            where TPanel : ResourceSuggestionPanel
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            var presentation = new ResourceSuggestionPanelPresentation<TPanel>(resource, address, balances, acquisition);
            factory.Register<TPanel>(view => new ResourceSuggestionPresenter(view.Request, balances, view, acquisition));
            return presentation;
        }
    }
}
