using System.Globalization;
using System.Text.RegularExpressions;
using API.Interfaces;
using SPADE;

namespace API.Problems.NPHard.NPH_MAXIMUMVALUEKNAPSACK.Verifiers;

[DefaultFor(typeof(MAXIMUMVALUEKNAPSACK))]
internal sealed class MaximumValueKnapsackSolutionVerifier : IVerifier<MAXIMUMVALUEKNAPSACK> {
    public const string CertificateExample = "({2,3},220)";
    public string verifierName { get; } = "Maximum-Value 0-1 Knapsack Solution Verifier";
    public string verifierDefinition { get; } = "Checks distinct 1-based item indices, capacity feasibility, and the reported total value against the selected items. Accepts feasible suboptimal selections and does not check optimality. Takes O(L + k) expected time and O(L + k) space for certificate length L and k selected items.";
    public string source { get; } = "Direct 0-1 knapsack feasibility and objective-value evaluation.";
    public string sourceLink { get; } = "https://doi.org/10.1007/978-3-540-24777-7";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Corbin Hay" };
    public string certificate { get; } = CertificateExample;

    // Validate the entire grammar before SPADE can discard malformed separators or
    // trailing text. NonBacktracking keeps this lexical check linear in input length.
    private static readonly Regex CertificateSyntax = new(
        @"\A\s*\(\s*\{\s*(?:[0-9]+(?:\s*,\s*[0-9]+)*)?\s*\}\s*,\s*[0-9]+\s*\)\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public bool verify(MAXIMUMVALUEKNAPSACK problem, string certificateText) {
        var parsed = ParseCertificate(problem, certificateText);
        // Parse all entries first, even when an earlier index is unknown. An index
        // outside the model is an invalid selection, not a malformed numeric token.
        if (parsed.Indices.Any(index => index < 1 || index > problem.items.Count)) return false;
        int[] selected = parsed.Indices.Select(index => (int)index).ToArray();
        return problem.IsFeasible(selected) && problem.Value(selected) == parsed.Value;
    }

    internal static (IReadOnlyList<long> Indices, long Value) ParseCertificate(
        MAXIMUMVALUEKNAPSACK problem, string text) {
        try {
            if (text is null || !CertificateSyntax.IsMatch(text))
                throw new FormatException("certificate must be a ({indices},totalValue) tuple containing ASCII decimal integers");
            string compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
            // Treat the already validated brace-delimited set as an ordered list:
            // duplicate item indices must be rejected, never silently deduplicated.
            StringParser parser = new("{C | C is list}");
            parser.parse(compact.Replace('{', '(').Replace('}', ')'));
            UtilCollection root = parser["C"];
            long value = NonnegativeInteger(root[1]);
            var indices = new List<long>();
            var seen = new HashSet<long>();
            foreach (UtilCollection entry in root[0].ToList()) {
                long index = NonnegativeInteger(entry);
                if (!seen.Add(index))
                    throw new FormatException($"item index {index.ToString(CultureInfo.InvariantCulture)} is selected more than once");
                indices.Add(index);
            }
            return (indices.AsReadOnly(), value);
        } catch (Exception ex) when (ex is not OutOfMemoryException && ex is not CertificateParseException) {
            throw new CertificateParseException(problem, text, ex.Message, ex);
        }
    }

    private static long NonnegativeInteger(UtilCollection entry) {
        if (!entry.IsValue() || !long.TryParse(entry.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out long value))
            throw new FormatException($"certificate numbers must be nonnegative integers no greater than {long.MaxValue}");
        return value;
    }
}
