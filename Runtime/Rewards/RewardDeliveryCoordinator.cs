using System;

namespace Dreamy.Economy.Rewards
{
    // Synchronous, main-thread only. Keep one coordinator per feature owner and guard
    // the feature's eligibility/finalization too. Callbacks must persist detached
    // feature candidates before publishing them. They must not mutate this snapshot.
    public sealed class RewardDeliveryCoordinator
    {
        private readonly IRewardDeliveryGateway gateway;
        private RewardJournal journal;
        private bool busy;

        public RewardDeliveryCoordinator(IRewardDeliveryGateway gateway, RewardJournal journal = null)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.journal = journal?.Clone();
        }

        public RewardJournal Journal => journal?.Clone();
        public bool IsBusy => busy;

        public RewardDeliveryResult Begin(string requestId, string requestFingerprint, Action<RewardJournal> persist)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(requestFingerprint))
                return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
            if (persist == null) throw new ArgumentNullException(nameof(persist));
            busy = true;
            try
            {
                if (journal != null)
                {
                    if (!journal.IsValid()) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidJournal);
                    if (journal.RequestId != requestId || journal.RequestFingerprint != requestFingerprint)
                        return RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                    return RewardDeliveryResult.AlreadyDelivered();
                }
                return Persist(new RewardJournal
                {
                    RequestId = requestId,
                    RequestFingerprint = requestFingerprint,
                    State = RewardJournalState.IntentPersisted
                }, persist);
            }
            finally { busy = false; }
        }

        public RewardDeliveryResult Resolve(ResolvedRewardBundle bundle, Action<RewardJournal> persist)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (persist == null) throw new ArgumentNullException(nameof(persist));
            if (bundle == null) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidRequest);
            busy = true;
            try
            {
                if (journal == null || !journal.IsValid()) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidJournal);
                if (journal.State != RewardJournalState.IntentPersisted)
                    return journal.ResolvedFingerprint == bundle.Fingerprint
                        ? RewardDeliveryResult.AlreadyDelivered()
                        : RewardDeliveryResult.Failed(RewardDeliveryReason.PayloadMismatch);
                RewardDeliveryResult validation;
                try { validation = gateway.Validate(journal.RequestId, journal.RequestFingerprint, bundle); }
                catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable); }
                if (!validation.IsSuccess) return validation;
                RewardJournal candidate = journal.Clone();
                candidate.State = RewardJournalState.Resolved;
                candidate.BundleSchemaVersion = bundle.SchemaVersion;
                candidate.ResolverVersion = bundle.ResolverVersion;
                candidate.ResolvedFingerprint = bundle.Fingerprint;
                foreach (ResolvedRewardEntry entry in bundle.Entries)
                    candidate.Entries.Add(new RewardJournalEntry
                    {
                        EntryId = entry.EntryId, KindId = entry.KindId,
                        TargetId = entry.TargetId, Amount = entry.Amount
                    });
                return Persist(candidate, persist);
            }
            finally { busy = false; }
        }

        public RewardDeliveryResult Deliver(Action<RewardJournal> persist)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (persist == null) throw new ArgumentNullException(nameof(persist));
            busy = true;
            try
            {
                if (journal == null || !journal.IsValid()) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidJournal);
                if (journal.State == RewardJournalState.IntentPersisted)
                    return RewardDeliveryResult.Pending(RewardDeliveryReason.ResolutionRequired);
                if (journal.State == RewardJournalState.DeliveredAwaitingClaimCommit || journal.State == RewardJournalState.Completed)
                    return RewardDeliveryResult.AlreadyDelivered();
                ResolvedRewardBundle bundle = journal.GetResolvedBundle();
                RewardDeliveryResult validation;
                try { validation = gateway.Validate(journal.RequestId, journal.RequestFingerprint, bundle); }
                catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable); }
                if (!validation.IsSuccess) return validation;
                for (int i = 0; i < journal.Entries.Count; i++)
                {
                    if (journal.Entries[i].Acknowledged) continue;
                    ResolvedRewardEntry entry = bundle.Entries[i];
                    RewardDeliveryResult delivery;
                    try
                    {
                        delivery = gateway.Deliver(new RewardDeliveryRequest(
                            RewardIdentity.EntryTransaction(journal.RequestId, entry.EntryId),
                            journal.RequestFingerprint, journal.ResolvedFingerprint, entry));
                    }
                    catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.DestinationUnavailable); }
                    if (!delivery.IsSuccess) return delivery;
                    RewardJournal candidate = journal.Clone();
                    candidate.Entries[i].Acknowledged = true;
                    candidate.State = i == candidate.Entries.Count - 1
                        ? RewardJournalState.DeliveredAwaitingClaimCommit : RewardJournalState.Delivering;
                    RewardDeliveryResult saved = Persist(candidate, persist);
                    if (!saved.IsSuccess) return saved;
                }
                return RewardDeliveryResult.Delivered();
            }
            finally { busy = false; }
        }

        // The callback must save claimed + this Completed journal atomically in the
        // feature document. Delivery alone is never a successful feature claim.
        public RewardDeliveryResult Complete(Action<RewardJournal> persistClaimed)
        {
            if (busy) return RewardDeliveryResult.Pending(RewardDeliveryReason.Busy);
            if (persistClaimed == null) throw new ArgumentNullException(nameof(persistClaimed));
            busy = true;
            try
            {
                if (journal == null || !journal.IsValid()) return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidJournal);
                if (journal.State == RewardJournalState.Completed) return RewardDeliveryResult.AlreadyDelivered();
                if (journal.State != RewardJournalState.DeliveredAwaitingClaimCommit)
                    return RewardDeliveryResult.Failed(RewardDeliveryReason.InvalidTransition);
                RewardJournal candidate = journal.Clone();
                candidate.State = RewardJournalState.Completed;
                return Persist(candidate, persistClaimed);
            }
            finally { busy = false; }
        }

        private RewardDeliveryResult Persist(RewardJournal candidate, Action<RewardJournal> persist)
        {
            try { persist(candidate.Clone()); }
            catch (Exception) { return RewardDeliveryResult.Pending(RewardDeliveryReason.PersistenceFailed); }
            journal = candidate;
            return RewardDeliveryResult.Delivered();
        }
    }
}
