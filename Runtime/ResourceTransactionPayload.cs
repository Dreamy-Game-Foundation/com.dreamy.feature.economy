using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;

namespace Dreamy.Economy
{
    internal static class ResourceTransactionPayload
    {
        internal static IReadOnlyList<ResourceAmount> Copy(IReadOnlyList<ResourceAmount> source)
        {
            if (source == null) return null;
            var copy = new ResourceAmount[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
            return Array.AsReadOnly(copy);
        }

        internal static SortedDictionary<string, long> Normalize(IReadOnlyList<ResourceAmount> source)
        {
            if (source == null || source.Count == 0) throw new ArgumentException("At least one resource is required.");
            var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (ResourceAmount amount in source)
            {
                if (!amount.ResourceId.IsValid || amount.Amount <= 0) throw new ArgumentException("Invalid resource amount.");
                result.TryGetValue(amount.ResourceId.Value, out long current);
                result[amount.ResourceId.Value] = checked(current + amount.Amount);
            }
            return result;
        }

        // Length-prefixed canonical payload, ordinal IDs and invariant numbers; receipts store its SHA-256.
        internal static string Fingerprint(string kind, string context, SortedDictionary<string, long> costs,
            SortedDictionary<string, long> rewards)
        {
            var text = new StringBuilder("v1|").Append(kind).Append('|');
            Append(text, context ?? string.Empty);
            foreach (var pair in costs) { text.Append('C'); Append(text, pair.Key); Append(text, pair.Value.ToString(CultureInfo.InvariantCulture)); }
            if (rewards != null)
                foreach (var pair in rewards) { text.Append('R'); Append(text, pair.Key); Append(text, pair.Value.ToString(CultureInfo.InvariantCulture)); }
            using var sha = SHA256.Create();
            string digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
            return "v1|" + kind + "|" + digest;
        }

        internal static bool IsValidFingerprint(string kind, string fingerprint)
        {
            string prefix = "v1|" + kind + "|";
            if (fingerprint == null || fingerprint.Length != prefix.Length + 64 || !fingerprint.StartsWith(prefix, StringComparison.Ordinal)) return false;
            for (int i = prefix.Length; i < fingerprint.Length; i++)
                if (!(fingerprint[i] >= '0' && fingerprint[i] <= '9') && !(fingerprint[i] >= 'a' && fingerprint[i] <= 'f')) return false;
            return true;
        }

        private static void Append(StringBuilder text, string value) =>
            text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
}
