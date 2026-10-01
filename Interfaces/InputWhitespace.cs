using System.Collections.Concurrent;
using System.Text;

namespace API.Interfaces;

// Whitespace tolerance for user-supplied instances and certificates. The problem parsers (most of
// them SPADE, an external package) reject or silently misread whitespace around structural
// delimiters, so a multi-line or comma-and-space instance pasted into a client either fails or
// verifies as false. Callers normalize before parsing, in the shared chokepoints (ParseGuard,
// ProblemProvider.Reduction / MapSolutions).
internal static class InputWhitespace {
    // Structural delimiters of the instance / certificate grammars. Whitespace next to one of these
    // never carries meaning; whitespace between two other characters ("New York", "x1 | !x2") might.
    private const string Delimiters = ",{}()[];:=";

    /// <summary>
    /// Trims leading and trailing whitespace and removes whitespace (any <c>char.IsWhiteSpace</c>
    /// character, including CR, LF and tab) adjacent to a structural delimiter <c>, { } ( ) [ ] ; : =</c>.
    /// A whitespace run between two non-delimiter characters is kept so that free text ("New York",
    /// "x1 | !x2") survives; if such a run contains a tab or line break it is collapsed to one space
    /// (a line break inside a value can only be a wrap, never meaningful), while a run of plain
    /// spaces is left exactly as typed.
    /// </summary>
    public static string Normalize(string? input) {
        if (string.IsNullOrEmpty(input)) return input ?? "";
        string s = input.Trim();
        var sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length) {
            if (!char.IsWhiteSpace(s[i])) {
                sb.Append(s[i++]);
                continue;
            }
            int j = i;
            bool plainSpaces = true;
            while (j < s.Length && char.IsWhiteSpace(s[j])) {
                if (s[j] != ' ') plainSpaces = false;
                j++;
            }
            // Trim() guarantees a run is never first or last, so sb[^1] and s[j] exist.
            if (!Delimiters.Contains(sb[^1]) && !Delimiters.Contains(s[j])) {
                if (plainSpaces) sb.Append(s, i, j - i);
                else sb.Append(' ');
            }
            i = j;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Normalizes an instance for <paramref name="problem"/>: full <see cref="Normalize"/>, or only a
    /// trim for a problem that sets <see cref="IProblem.preserveInstanceWhitespace"/>. Certificates have
    /// no opt-out: they are only normalized on a retry after the as-given text failed.
    /// </summary>
    public static string ForInstance(IProblem? problem, string input) =>
        problem?.preserveInstanceWhitespace == true ? input?.Trim() ?? "" : Normalize(input);

    private static readonly ConcurrentDictionary<Type, bool> PreserveByType = new();

    /// <summary>As <see cref="ForInstance(IProblem?, string)"/>, for a problem type that has not been constructed yet.</summary>
    public static string ForInstanceOfType(Type problemType, string input) {
        bool preserve = PreserveByType.GetOrAdd(problemType, t => {
            try {
                return Activator.CreateInstance(t) is IProblem { preserveInstanceWhitespace: true };
            } catch {
                return false; // no default instance to ask: use the default rules
            }
        });
        return preserve ? input?.Trim() ?? "" : Normalize(input);
    }
}
