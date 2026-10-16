using System;
using System.Collections.Generic;

namespace Dreamy.Economy.UI
{
    public interface INotEnoughResourcesView
    {
        event Action<ResourceId> AcquireRequested;
        event Action CancelRequested;
        void Open();
        void Render(IReadOnlyList<ResourceDeficit> deficits, Func<ResourceId, ResourceAcquisitionAvailability> availability,
            bool opening, ResourceAcquisitionResult? lastNavigation);
        void Close();
    }
}
