using System;
using System.Collections.Generic;

namespace Dreamy.Economy.Rewards
{
    public enum RewardJournalState
    {
        IntentPersisted, Resolved, Delivering, DeliveredAwaitingClaimCommit, Completed
    }

    // Embed in the feature's save. Claim context and the intended final feature transition
    // belong beside this journal in that same document, not in an Economy-owned save.
    [Serializable]
    public sealed class RewardJournal
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion = CurrentSchemaVersion;
        public string RequestId;
        public string RequestFingerprint;
        public RewardJournalState State;
        public int BundleSchemaVersion;
        public string ResolverVersion;
        public string ResolvedFingerprint;
        public List<RewardJournalEntry> Entries = new();

        public RewardJournal Clone()
        {
            var clone = new RewardJournal
            {
                SchemaVersion = SchemaVersion,
                RequestId = RequestId,
                RequestFingerprint = RequestFingerprint,
                State = State,
                BundleSchemaVersion = BundleSchemaVersion,
                ResolverVersion = ResolverVersion,
                ResolvedFingerprint = ResolvedFingerprint,
                Entries = Entries == null ? null : new List<RewardJournalEntry>()
            };
            if (Entries != null)
                foreach (RewardJournalEntry entry in Entries) clone.Entries.Add(entry?.Clone());
            return clone;
        }

        public ResolvedRewardBundle GetResolvedBundle()
        {
            var entries = new List<ResolvedRewardEntry>();
            foreach (RewardJournalEntry entry in Entries)
                entries.Add(new ResolvedRewardEntry(entry.EntryId, entry.KindId, entry.TargetId, entry.Amount));
            return new ResolvedRewardBundle(BundleSchemaVersion, ResolverVersion, entries);
        }

        public bool IsValid()
        {
            if (SchemaVersion != CurrentSchemaVersion || string.IsNullOrWhiteSpace(RequestId) ||
                string.IsNullOrWhiteSpace(RequestFingerprint) || Entries == null ||
                State < RewardJournalState.IntentPersisted || State > RewardJournalState.Completed)
                return false;
            if (State == RewardJournalState.IntentPersisted)
                return Entries.Count == 0 && BundleSchemaVersion == 0 &&
                    string.IsNullOrEmpty(ResolverVersion) && string.IsNullOrEmpty(ResolvedFingerprint);
            foreach (RewardJournalEntry entry in Entries)
                if (entry == null) return false;
            try
            {
                if (!string.Equals(GetResolvedBundle().Fingerprint, ResolvedFingerprint, StringComparison.Ordinal))
                    return false;
            }
            catch (ArgumentException) { return false; }
            bool all = true;
            bool any = false;
            bool unacknowledged = false;
            foreach (RewardJournalEntry entry in Entries)
            {
                if (unacknowledged && entry.Acknowledged) return false;
                unacknowledged |= !entry.Acknowledged;
                all &= entry.Acknowledged;
                any |= entry.Acknowledged;
            }
            if (State == RewardJournalState.Resolved) return !any;
            if (State == RewardJournalState.Delivering) return any && !all;
            return all;
        }
    }

    [Serializable]
    public sealed class RewardJournalEntry
    {
        public string EntryId;
        public string KindId;
        public string TargetId;
        public long Amount;
        public bool Acknowledged;

        public RewardJournalEntry Clone() => new()
        {
            EntryId = EntryId, KindId = KindId, TargetId = TargetId,
            Amount = Amount, Acknowledged = Acknowledged
        };
    }
}
