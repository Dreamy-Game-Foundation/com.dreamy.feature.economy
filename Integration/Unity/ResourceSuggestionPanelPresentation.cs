using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.UI;
using UnityEngine;

namespace Dreamy.Economy.UI
{
    public sealed class ResourceSuggestionPanelPresentation : IInsufficientResourcesPresentation
    {
        private readonly ResourceSuggestionPanel prefab;
        private readonly IResourceBalanceSource balances;
        private readonly ResourceAcquisitionCoordinator acquisition;
        private bool busy;
        public ResourceSuggestionPanelPresentation(ResourceSuggestionPanel prefab,
            IResourceBalanceSource balances, ResourceAcquisitionCoordinator acquisition)
        {
            this.prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
            this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
            _ = prefab.ResourceId; // Fail invalid authoring at composition time.
        }

        public async UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs, string contextId, CancellationToken token)
        {
            if (token.IsCancellationRequested) return ResourceRecoveryStatus.Cancelled;
            if (busy) return ResourceRecoveryStatus.Busy;
            if (prefab == null || !PanelManager.HasInstance) return ResourceRecoveryStatus.PresentationUnavailable;
            busy = true;
            ResourceSuggestionPanel panel = null;
            try
            {
                // Registration/layers/Back remain managed by PanelManager. Creation is per prefab,
                // because Create<T> caches only by type and would otherwise reuse the Coin view for Gem.
                panel = UnityEngine.Object.Instantiate(prefab, PanelManager.Instance.GetLayerRoot(UILayer.Popup));
                await panel.Init();
                await panel.PostInit();
                token.ThrowIfCancellationRequested();
                using var presenter = new ResourceSuggestionPresenter(prefab.ResourceId, balances, panel, acquisition);
                return await presenter.ShowAsync(costs, contextId, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { return ResourceRecoveryStatus.Cancelled; }
            finally
            {
                try { if (panel != null) await panel.CloseAsync(); }
                finally
                {
                    if (panel != null) { if (PanelManager.HasInstance) PanelManager.Instance.Unregister(panel); UnityEngine.Object.Destroy(panel.gameObject); }
                    busy = false;
                }
            }
        }
    }
}
