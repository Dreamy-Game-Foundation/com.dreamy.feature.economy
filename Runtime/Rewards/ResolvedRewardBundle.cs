using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dreamy.Economy.Rewards
{
    public readonly struct ResolvedRewardEntry
    {
        public ResolvedRewardEntry(string entryId, string kindId, string targetId, long amount)
        {
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException("Entry ID is required.", nameof(entryId));
            if (string.IsNullOrWhiteSpace(kindId)) throw new ArgumentException("Kind ID is required.", nameof(kindId));
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Target ID is required.", nameof(targetId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            EntryId = entryId;
            KindId = kindId;
            TargetId = targetId;
            Amount = amount;
        }

        public string EntryId { get; }
        public string KindId { get; }
        public string TargetId { get; }
        public long Amount { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(EntryId) &&
            !string.IsNullOrWhiteSpace(KindId) && !string.IsNullOrWhiteSpace(TargetId) && Amount > 0;
    }

    public sealed class ResolvedRewardBundle
    {
        public const int CurrentSchemaVersion = 1;

        public ResolvedRewardBundle(int schemaVersion, string resolverVersion, IReadOnlyList<ResolvedRewardEntry> entries)
        {
            if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (string.IsNullOrWhiteSpace(resolverVersion)) throw new ArgumentException("Resolver version is required.", nameof(resolverVersion));
            if (entries == null || entries.Count == 0) throw new ArgumentException("At least one entry is required.", nameof(entries));
            var copy = new ResolvedRewardEntry[entries.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsValid || !ids.Add(entries[i].EntryId))
                    throw new ArgumentException("Entries must be valid and have unique entry IDs.", nameof(entries));
                copy[i] = entries[i];
            }
            SchemaVersion = schemaVersion;
            ResolverVersion = resolverVersion;
            Entries = Array.AsReadOnly(copy);
            var fields = new List<string> { "resolved-reward", schemaVersion.ToString(CultureInfo.InvariantCulture), resolverVersion };
            foreach (ResolvedRewardEntry entry in copy)
            {
                fields.Add(entry.EntryId);
                fields.Add(entry.KindId);
                fields.Add(entry.TargetId);
                fields.Add(entry.Amount.ToString(CultureInfo.InvariantCulture));
            }
            Fingerprint = RewardIdentity.Fingerprint(fields.ToArray());
        }

        public int SchemaVersion { get; }
        public string ResolverVersion { get; }
        public IReadOnlyList<ResolvedRewardEntry> Entries { get; }
        public string Fingerprint { get; }
    }
}
