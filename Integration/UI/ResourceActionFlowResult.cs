namespace Dreamy.Economy.UI
{
    public enum InsufficientResourcePresentation { None, Prompt }
    public enum ResourceRecoveryStatus { None, Cancelled, Returned, Busy, PresentationUnavailable, Failed }

    public readonly struct ResourceActionFlowResult
    {
        public ResourceActionFlowResult(ResourceTransactionResult transaction, ResourceRecoveryStatus recovery)
        { Transaction = transaction; Recovery = recovery; }
        public ResourceTransactionResult Transaction { get; }
        public ResourceRecoveryStatus Recovery { get; }
    }
}
