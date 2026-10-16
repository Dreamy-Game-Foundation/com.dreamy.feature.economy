using System;
using System.Collections.Generic;

namespace Dreamy.Economy
{
    public enum ResourceTransactionStatus
    {
        InvalidRequest = 0, Committed, AlreadyCommitted, InsufficientResources,
        Busy, Overflow, PersistenceFailed, PayloadMismatch, LegacyReceiptUnverifiable, Unsupported,
        PersistenceUncertain
    }

    public readonly struct ResourceTransactionResult
    {
        private readonly IReadOnlyList<ResourceDeficit> deficits;

        public ResourceTransactionResult(ResourceTransactionStatus status, string transactionId = null,
            IReadOnlyList<ResourceDeficit> deficits = null, string errorCode = null)
        {
            Status = status;
            TransactionId = transactionId;
            this.deficits = deficits == null ? Array.Empty<ResourceDeficit>() : Array.AsReadOnly(Copy(deficits));
            ErrorCode = errorCode;
        }

        public ResourceTransactionStatus Status { get; }
        public string TransactionId { get; }
        public string ErrorCode { get; }
        public IReadOnlyList<ResourceDeficit> Deficits => deficits ?? Array.Empty<ResourceDeficit>();
        public bool IsSuccess => Status == ResourceTransactionStatus.Committed || Status == ResourceTransactionStatus.AlreadyCommitted;
        public bool IsNewCommit => Status == ResourceTransactionStatus.Committed;

        private static ResourceDeficit[] Copy(IReadOnlyList<ResourceDeficit> source)
        {
            var result = new ResourceDeficit[source.Count];
            for (int i = 0; i < result.Length; i++) result[i] = source[i];
            return result;
        }
    }
}
