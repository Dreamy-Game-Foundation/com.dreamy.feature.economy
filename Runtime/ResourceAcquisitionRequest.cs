using System;
using System.Collections.Generic;

namespace Dreamy.Economy
{
    public readonly struct ResourceAcquisitionRequest
    {
        public ResourceAcquisitionRequest(ResourceId targetResourceId, long missingAmount, string sourceContextId = null,
            IReadOnlyList<ResourceDeficit> deficits = null)
        {
            if (!targetResourceId.IsValid) throw new ArgumentException("Invalid resource.", nameof(targetResourceId));
            if (missingAmount <= 0) throw new ArgumentOutOfRangeException(nameof(missingAmount));
            TargetResourceId = targetResourceId;
            MissingAmount = missingAmount;
            SourceContextId = sourceContextId;
            var copy = new ResourceDeficit[deficits?.Count ?? 0];
            for (int i = 0; i < copy.Length; i++) copy[i] = deficits[i];
            Deficits = Array.AsReadOnly(copy);
        }

        public ResourceId TargetResourceId { get; }
        public long MissingAmount { get; }
        public string SourceContextId { get; }
        public IReadOnlyList<ResourceDeficit> Deficits { get; }
        public bool IsValid => TargetResourceId.IsValid && MissingAmount > 0;
    }
}
