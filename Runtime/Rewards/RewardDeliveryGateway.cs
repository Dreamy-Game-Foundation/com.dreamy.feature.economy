using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dreamy.Economy.Rewards
{
    public readonly struct RewardDeliveryRequest
    {
        public RewardDeliveryRequest(string transactionId, string requestFingerprint, string resolvedFingerprint, ResolvedRewardEntry entry)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(requestFingerprint) ||
                string.IsNullOrWhiteSpace(resolvedFingerprint) || !entry.IsValid)
                throw new ArgumentException("A delivery requires identity, both fingerprints and a valid resolved entry.");
            TransactionId = transactionId;
            Entry = entry;
            RequestFingerprint = requestFingerprint;
            ResolvedFingerprint = resolvedFingerprint;
            Fingerprint = RewardIdentity.Fingerprint("delivery", requestFingerprint, resolvedFingerprint,
                entry.EntryId, entry.KindId, entry.TargetId, entry.Amount.ToString(CultureInfo.InvariantCulture));
        }

        public string TransactionId { get; }
        public string RequestFingerprint { get; }
        public string ResolvedFingerprint { get; }
        public ResolvedRewardEntry Entry { get; }
        public string Fingerprint { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(TransactionId) && Entry.IsValid &&
            !string.IsNullOrWhiteSpace(Fingerprint);
    }

    // A destination owns durable effects and receipts in one save candidate. Validate never mutates.
    // Validate must check combined amounts, ignoring identical receipts already committed.
    public interface IRewardDestination
    {
        string KindId { get; }
        RewardDeliveryResult Validate(IReadOnlyList<RewardDeliveryRequest> requests);
        RewardDeliveryResult Deliver(RewardDeliveryRequest request);
    }

    public interface IRewardDeliveryGateway
    {
        RewardDeliveryResult Validate(string requestId, string requestFingerprint, ResolvedRewardBundle bundle);
        RewardDeliveryResult Deliver(RewardDeliveryRequest request);
    }

    public sealed class RewardDeliveryGateway : IRewardDeliveryGateway
    {
        private readonly Dictionary<string, IRewardDestination> destinations = new(StringComparer.Ordinal);
        private bool busy;

        public RewardDeliveryGateway(IReadOnlyList<IRewardDestination> destinations)
        {
            if (destinations == null) throw new ArgumentNullException(nameof(destinations));
            foreach (IRewardDestination destination in destinations)
            {
                if (destination == null || string.IsNullOrWhiteSpace(destination.KindId) ||
                    this.destinations.ContainsKey(destination.KindId))
                    throw new ArgumentException("Destination kinds must be nonempty and unique.", nameof(destinations));
                this.destinations.Add(destination.KindId, destination);
            }
        }

        public RewardDeliveryResult Validate(string requestId, string requestFingerprint, ResolvedRewardBundle bundle)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (bundle == null || string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(requestFingerprint))
                return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
            busy = true;
            try
            {
                var groups = new Dictionary<string, List<RewardDeliveryRequest>>(StringComparer.Ordinal);
                foreach (ResolvedRewardEntry entry in bundle.Entries)
                {
                    if (!destinations.ContainsKey(entry.KindId))
                        return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownKind);
                    if (!groups.TryGetValue(entry.KindId, out List<RewardDeliveryRequest> requests))
                        groups.Add(entry.KindId, requests = new List<RewardDeliveryRequest>());
                    requests.Add(new RewardDeliveryRequest(RewardIdentity.EntryTransaction(requestId, entry.EntryId),
                        requestFingerprint, bundle.Fingerprint, entry));
                }
                foreach (KeyValuePair<string, List<RewardDeliveryRequest>> group in groups)
                {
                    RewardDeliveryResult result = destinations[group.Key].Validate(group.Value);
                    if (!result.IsSuccess) return result;
                }
                return RewardDeliveryResult.Delivered();
            }
            finally { busy = false; }
        }

        public RewardDeliveryResult Deliver(RewardDeliveryRequest request)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (!request.IsValid) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
            if (!destinations.TryGetValue(request.Entry.KindId, out IRewardDestination destination))
                return RewardDeliveryResult.Failed(RewardDeliveryReason.UnknownKind);
            busy = true;
            try { return destination.Deliver(request); }
            finally { busy = false; }
        }
    }
}
