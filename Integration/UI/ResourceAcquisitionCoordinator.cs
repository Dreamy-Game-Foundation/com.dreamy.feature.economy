using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    public sealed class ResourceAcquisitionCoordinator
    {
        private readonly IResourceAcquisitionGateway gateway;
        private bool busy;
        public ResourceAcquisitionCoordinator(IResourceAcquisitionGateway gateway) => this.gateway = gateway;
        public ResourceAcquisitionAvailability GetAvailability(ResourceId id) =>
            gateway?.GetAvailability(id) ?? ResourceAcquisitionAvailability.Unavailable;

        public UniTask<ResourceAcquisitionResult> OpenForResourceAsync(ResourceId id, long missingAmount,
            string contextId = null, CancellationToken token = default) =>
            OpenAsync(new ResourceAcquisitionRequest(id, missingAmount, contextId), token);

        public UniTask<ResourceAcquisitionResult> OpenForDeficitAsync(ResourceDeficit deficit,
            string contextId = null, CancellationToken token = default) =>
            OpenAsync(new ResourceAcquisitionRequest(deficit.ResourceId, deficit.Missing, contextId, new[] { deficit }), token);

        public async UniTask<ResourceAcquisitionResult> OpenAsync(ResourceAcquisitionRequest request, CancellationToken token)
        {
            if (token.IsCancellationRequested) return ResourceAcquisitionResult.Cancelled;
            if (!request.IsValid) return ResourceAcquisitionResult.Failed;
            if (busy) return ResourceAcquisitionResult.Busy;
            if (gateway == null) return ResourceAcquisitionResult.Unsupported;
            busy = true;
            try
            {
                if (GetAvailability(request.TargetResourceId) == ResourceAcquisitionAvailability.Unavailable)
                    return ResourceAcquisitionResult.NoMatchingDestination;
                var result = await gateway.OpenAsync(request, token);
                return token.IsCancellationRequested ? ResourceAcquisitionResult.Cancelled : result;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return ResourceAcquisitionResult.Cancelled; }
            catch (Exception) { return ResourceAcquisitionResult.Failed; }
            finally { busy = false; }
        }
    }
}
