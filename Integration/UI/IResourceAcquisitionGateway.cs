using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Economy.UI
{
    public interface IResourceAcquisitionGateway
    {
        ResourceAcquisitionAvailability GetAvailability(ResourceId resourceId);
        UniTask<ResourceAcquisitionResult> OpenAsync(ResourceAcquisitionRequest request, CancellationToken cancellationToken);
    }
}
