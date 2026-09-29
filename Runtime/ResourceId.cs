using System;

namespace Dreamy.Economy
{
    public readonly struct ResourceId : IEquatable<ResourceId>
    {
        public ResourceId(string value)
        {
            if (!TryParse(value, out ResourceId parsed))
            {
                throw new ArgumentException(
                    "Resource IDs must use dot-separated alphanumeric, underscore, or hyphen segments.",
                    nameof(value));
            }

            Value = parsed.Value;
        }

        private ResourceId(string value, bool isValidated)
        {
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(ResourceId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ResourceId other && Equals(other);

        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(ResourceId left, ResourceId right) => left.Equals(right);
        public static bool operator !=(ResourceId left, ResourceId right) => !left.Equals(right);

        public static bool TryParse(string value, out ResourceId resourceId)
        {
            resourceId = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] segments = value.Split('.');
            if (segments.Length < 2)
            {
                return false;
            }

            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment))
                {
                    return false;
                }

                for (int index = 0; index < segment.Length; index++)
                {
                    char character = segment[index];
                    if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    {
                        return false;
                    }
                }
            }

            resourceId = new ResourceId(value, true);
            return true;
        }
    }
}
