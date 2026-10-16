namespace Dreamy.Economy
{
    // Navigation outcome only; none of these values proves a purchase or grants resources.
    public enum ResourceAcquisitionResult { Returned, Cancelled, Unsupported, NoMatchingDestination, Failed, Busy }
    public enum ResourceAcquisitionAvailability { Unknown, Available, Unavailable }
}
