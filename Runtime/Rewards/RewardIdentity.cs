using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dreamy.Economy.Rewards
{
    public static class RewardIdentity
    {
        // Lengths are UTF-16 code units, as used by System.String, and numbers are invariant decimal.
        public static string Encode(params string[] fields)
        {
            if (fields == null || fields.Length == 0) throw new ArgumentException("Identity fields are required.", nameof(fields));
            var text = new StringBuilder("reward-v1:");
            foreach (string field in fields)
            {
                if (field == null) throw new ArgumentException("Identity fields cannot be null.", nameof(fields));
                text.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field);
            }
            return text.ToString();
        }

        public static string Request(string scope, string feature, string definitionId, string occurrenceId, string claimId)
        {
            foreach (string value in new[] { scope, feature, definitionId, occurrenceId, claimId })
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Request identity components are required.");
            return Encode("request", scope, feature, definitionId, occurrenceId, claimId);
        }

        public static string EntryTransaction(string requestId, string entryId)
        {
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Request and entry IDs are required.");
            return Encode("entry", requestId, entryId);
        }

        public static string Fingerprint(params string[] fields)
        {
            // Reject malformed UTF-16 rather than replacing distinct invalid strings with the same bytes.
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(Encode(fields));
            using (SHA256 hash = SHA256.Create())
                return "sha256-v1:" + BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
