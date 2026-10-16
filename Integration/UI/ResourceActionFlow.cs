using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    public sealed class ResourceActionFlow
    {
        private readonly IResourceTransactionWallet wallet;
        private readonly IInsufficientResourcesPresentation presentation;
        public ResourceActionFlow(IResourceTransactionWallet wallet, IInsufficientResourcesPresentation presentation = null)
        { this.wallet = wallet; this.presentation = presentation; }

        public UniTask<ResourceActionFlowResult> ExecuteAsync(ResourceSpendRequest request,
            InsufficientResourcePresentation policy = InsufficientResourcePresentation.None, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            var result = wallet?.Spend(request) ?? new ResourceTransactionResult(ResourceTransactionStatus.Unsupported, request.TransactionId);
            return PresentAsync(result, request.Costs, request.ContextId, policy, token);
        }

        public UniTask<ResourceActionFlowResult> ExecuteAsync(ResourceTransactionRequest request,
            InsufficientResourcePresentation policy = InsufficientResourcePresentation.None, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            var result = wallet?.Exchange(request) ?? new ResourceTransactionResult(ResourceTransactionStatus.Unsupported, request.TransactionId);
            return PresentAsync(result, request.Costs, request.ContextId, policy, token);
        }

        private async UniTask<ResourceActionFlowResult> PresentAsync(ResourceTransactionResult result,
            IReadOnlyList<ResourceAmount> costs, string context, InsufficientResourcePresentation policy, CancellationToken token)
        {
            var recovery = ResourceRecoveryStatus.None;
            if (result.Status == ResourceTransactionStatus.InsufficientResources && policy == InsufficientResourcePresentation.Prompt)
            {
                if (presentation == null) recovery = ResourceRecoveryStatus.PresentationUnavailable;
                else
                {
                    try { recovery = await presentation.ShowAsync(costs, context, token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { recovery = ResourceRecoveryStatus.Cancelled; }
                    catch (Exception) { recovery = ResourceRecoveryStatus.Failed; }
                }
            }
            // Never retry or replace the original transaction with a navigation result.
            return new ResourceActionFlowResult(result, recovery);
        }
    }
}
