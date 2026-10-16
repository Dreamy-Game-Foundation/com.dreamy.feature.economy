using System;

namespace Dreamy.Economy.UI
{
    public readonly struct ResourceSuggestionState
    {
        public ResourceSuggestionState(ResourceId resource, long required, long available,
            ResourceAcquisitionAvailability availability)
        { ResourceId = resource; Required = required; Available = available; Availability = availability; }
        public ResourceId ResourceId { get; }
        public long Required { get; }
        public long Available { get; }
        public long Missing => Math.Max(0, Required - Available);
        public ResourceAcquisitionAvailability Availability { get; }
        public bool CanAcquire => Missing > 0 && Availability != ResourceAcquisitionAvailability.Unavailable;
    }

    // One authored panel for one resource. Closing must finish before navigation takes input.
    public interface IResourceSuggestionView
    {
        event Action AcquireRequested;
        event Action LaterRequested;
        void Render(ResourceSuggestionState state);
    }
}
