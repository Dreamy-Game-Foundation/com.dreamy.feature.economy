using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.UI;
using UnityEngine;

namespace Dreamy.Economy.UI
{
    // Application boundary: creates/reuses the UIPanel; domain presenter still owns deficit/recovery logic.
    public sealed class ResourceRecoveryPanelPresentation : IInsufficientResourcesPresentation
    {
        private readonly IResourceBalanceSource balances;
        private readonly ResourceAcquisitionCoordinator acquisition;
        private readonly NotEnoughResourcesPanel prefab;
        private UniTaskCompletionSource sessionFinished;
        private CancellationTokenSource activeSession;

        public ResourceRecoveryPanelPresentation(IResourceBalanceSource balances, ResourceAcquisitionCoordinator acquisition,
            NotEnoughResourcesPanel prefab)
        {
            this.balances = balances ?? throw new ArgumentNullException(nameof(balances));
            this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
            this.prefab = prefab;
        }

        public async UniTask<ResourceRecoveryStatus> ShowAsync(IReadOnlyList<ResourceAmount> costs, string contextId, CancellationToken token)
        {
            if (token.IsCancellationRequested) return ResourceRecoveryStatus.Cancelled;
            if (prefab == null) return ResourceRecoveryStatus.PresentationUnavailable;
            // A fresh insufficient purchase supersedes acquisition still waiting in the same Shop.
            var previous = sessionFinished;
            var completion = new UniTaskCompletionSource();
            sessionFinished = completion;
            using var session = CancellationTokenSource.CreateLinkedTokenSource(token);
            var previousSession = activeSession;
            activeSession = session;
            previousSession?.Cancel();
            NotEnoughResourcesPanel panel = null;
            try
            {
                // Serialize owner-thread sessions through shared completions, including close tweens.
                if (previous != null) await previous.Task;
                session.Token.ThrowIfCancellationRequested();
                panel = await PanelManager.Instance.Create<NotEnoughResourcesPanel>(prefab.gameObject);
                session.Token.ThrowIfCancellationRequested();
                using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(session.Token, panel.GetCancellationTokenOnDestroy());
                using var presenter = new NotEnoughResourcesPresenter(balances, panel, acquisition);
                return await presenter.ShowAsync(costs, contextId, lifetime.Token);
            }
            catch (OperationCanceledException) when (session.IsCancellationRequested || panel == null)
            { return ResourceRecoveryStatus.Cancelled; }
            finally
            {
                try { if (panel != null) await panel.CloseAsync(); }
                finally
                {
                    if (ReferenceEquals(activeSession, session)) activeSession = null;
                    completion.TrySetResult();
                }
            }
        }
    }
}
