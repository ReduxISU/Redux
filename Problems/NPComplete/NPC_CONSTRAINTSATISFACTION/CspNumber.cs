using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace API.Problems.NPComplete.NPC_CONSTRAINTSATISFACTION;

// Handles number parsing, validation, and formatting to ensure
// numbers are valid, accurate, and consistently represented.
// Single numeric policy for domains, expression literals, and external certificates.
// Input literals must be exactly representable as decimal; intermediate arithmetic
// still follows System.Decimal rounding, underflow, and overflow semantics.
internal static class CspNumber {
    // Invariant fixed-point grammar: no exponent, grouping separator, or trailing dot.
    // Consumers anchor this pattern to reject unexpected characters around a number.
    internal const string Pattern = @"[+-]?(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)";
    // TryParse silently rounds some literals; require an exact conversion instead.
    // Parses a bounded numeric token and rejects otherwise successful conversions
    // that would silently lose digits. BigInteger validates literals only; evaluated
    // expressions do not acquire arbitrary-precision arithmetic.
    internal static decimal Parse(string text) {
        // Bound token conversion work and use the same culture-independent syntax on
        // every input path. TryParse also rejects values outside decimal's magnitude.
        if (text.Length > 1024 || !Regex.IsMatch(text, @"\A" + Pattern + @"\z") ||
            !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal value)) throw new FormatException($"Invalid decimal '{text}'.");
        // Interpret the original text as coefficient / 10^scale without rounding.
        // Leading/trailing zeros do not affect the exact numerical comparison below.
        string unsigned = text.TrimStart('+', '-');
        int dot = unsigned.IndexOf('.'), scale = dot < 0 ? 0 : unsigned.Length - dot - 1;
        BigInteger coefficient = BigInteger.Parse(unsigned.Replace(".", ""), CultureInfo.InvariantCulture);
        if (text[0] == '-') coefficient = -coefficient;
        // Reconstruct decimal's signed 96-bit coefficient and stored scale. Its three
        // coefficient words must be treated as unsigned before combining them.
        int[] bits = decimal.GetBits(value);
        BigInteger stored = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0) stored = -stored;
        int storedScale = (bits[3] >> 16) & 255;
        // Cross-multiply using integers: comparing decimal values here would repeat
        // the very rounding we are trying to detect (including tiny literals becoming 0).
        if (coefficient * BigInteger.Pow(10, storedScale) != stored * BigInteger.Pow(10, scale))
            throw new FormatException($"Decimal '{text}' cannot be represented exactly.");
        return value;
    }
    // Fixed-point output: G29 can emit exponents, which are outside our grammar.
    // Emits verifier-compatible fixed-point text, independent of current culture.
    internal static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
}
