using System.Globalization;

namespace Dreamy.Economy.UI
{
    public static class ResourceAmountFormatter
    {
        public static string Format(long value, ResourceAmountFormat format)
        {
            return format switch
            {
                ResourceAmountFormat.Raw => value.ToString(CultureInfo.InvariantCulture),
                ResourceAmountFormat.Grouped => value.ToString("N0", CultureInfo.InvariantCulture),
                _ => FormatCompact(value)
            };
        }

        private static string FormatCompact(long value)
        {
            long absolute = value < 0 ? -value : value;
            if (absolute < 1_000) return value.ToString(CultureInfo.InvariantCulture);
            if (absolute < 1_000_000) return FormatScaled(value, 1_000d, "K");
            if (absolute < 1_000_000_000) return FormatScaled(value, 1_000_000d, "M");
            return FormatScaled(value, 1_000_000_000d, "B");
        }

        private static string FormatScaled(long value, double divisor, string suffix)
        {
            double scaled = value / divisor;
            string pattern = System.Math.Abs(scaled) >= 100d ? "0" : "0.#";
            return scaled.ToString(pattern, CultureInfo.InvariantCulture) + suffix;
        }
    }
}
