// BinPackingVerifier.cs
// This is the polynomial-time verifier for Bin Packing.
//
// Given a problem instance <S, C, K> and a candidate certificate (a proposed
// packing of items into bins), the verifier checks three conditions:
//
//   1. MULTISET CHECK — every item in S appears in the certificate exactly
//      the right number of times (no missing items, no extra items, no swaps).
//      We use sort-then-SequenceEqual for correct multiset comparison —
//      this handles duplicate sizes properly, unlike a set-membership check.
//
//   2. CAPACITY CHECK — the total size of items in each bin does not exceed C.
//
//   3. BIN COUNT CHECK — the number of non-empty bins does not exceed K.
//
// All three checks run in O(n log n) time (dominated by the sort), so the
// verifier is genuinely polynomial — as required for NP membership.
//
// Malformed certificates (wrong syntax, garbage input) are rejected gracefully
// by returning false rather than throwing an exception to the caller.

using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BINPACKING.Verifiers;

class BinPackingVerifier : IVerifier<BINPACKING> {

    //  Metadata
    public string verifierName { get; } = "Bin Packing Verifier";

    // Explains all three checks and the overall time complexity.
    public string verifierDefinition { get; } = "Polynomial-time verifier for Bin Packing. Given a problem instance <S, C, K> and a candidate certificate representing a partition of S into bins, the verifier checks three conditions: (1) the multiset of item sizes appearing in the certificate equals S exactly (every item appears the correct number of times, no items missing or added), (2) the sum of sizes in each bin does not exceed C, and (3) the number of non-empty bins does not exceed K. The verifier runs in O(n log n) time, dominated by the sort used for multiset comparison.";

    public string source { get; } = "Garey, M. R., and Johnson, D. S. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SR1.";

    public string[] contributors { get; } = { "Himanshu", "Rakesh", "Prashanta" };

    // O(n log n) — the sort for multiset equality is the bottleneck.
    private string _complexity = "O(n log n)";

    // This is the example certificate shown to users in the Redux UI.
    // It corresponds to the default instance ((4,7,3,6,2,8),10,3):
    //   bin 1: 8+2=10, bin 2: 7+3=10, bin 3: 6+4=10 — all within capacity.
    private string _certificate = "((8,2),(7,3),(6,4))";

    public string complexity {
        get { return _complexity; }
    }

    public string certificate {
        get { return _certificate; }
    }

    

    public BinPackingVerifier() {
    }

    //  Main verification logic 

    public bool verify(BINPACKING problem, string certificate) {

        // Try to parse the certificate string into a list of bins.
        // If the format is wrong (missing parens, bad numbers, etc.), reject it.
        List<List<int>> bins;
        try {
            bins = ParseCertificate(certificate);
        }
        catch {
            return false;
        }

        // Walk through every bin, collecting items and checking capacity.
        int nonEmptyBinCount = 0;
        List<int> flatItems = new List<int>();

        foreach (List<int> bin in bins) {
            // Skip empty bins — they don't count toward the K-bin limit.
            if (bin.Count == 0) continue;
            nonEmptyBinCount++;

            int binSum = 0;
            foreach (int size in bin) {
                // Item sizes must be positive — a size of 0 or negative is invalid.
                if (size <= 0) return false;
                binSum += size;
                flatItems.Add(size);
            }

            // CHECK 2: This bin's total must not exceed the bin capacity C.
            if (binSum > problem.C) return false;
        }

        // CHECK 3: We cannot use more bins than K allows.
        if (nonEmptyBinCount > problem.K) return false;

        // CHECK 1: The multiset of items in the certificate must exactly equal S.
        // Sorting both sides before comparing handles duplicate sizes correctly.
        List<int> expected = problem.S.OrderBy(x => x).ToList();
        List<int> actual = flatItems.OrderBy(x => x).ToList();
        if (!expected.SequenceEqual(actual)) return false;

        // All three checks passed — this is a valid packing certificate.
        return true;
    }

    //  Certificate parser 
    // Parses the certificate string "((a,b),(c,d,e),...)" into a list of bins,
    // where each bin is a list of integers.
    //
    // We hand-roll this parser instead of using SPADE because the nested
    // tuple structure ((a,b),(c,d)) is awkward to express as a SPADE pattern.
    // The depth-tracking SplitTopLevel helper handles arbitrarily nested commas.
    private List<List<int>> ParseCertificate(string cert) {
        if (cert == null) throw new FormatException("certificate is null");

        string trimmed = cert.Trim();

        // The entire certificate must be wrapped in outer parentheses.
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[trimmed.Length - 1] != ')') {
            throw new FormatException("certificate must be wrapped in parentheses");
        }

        // Strip the outermost parentheses to get the comma-separated list of bins.
        string inner = trimmed.Substring(1, trimmed.Length - 2).Trim();

        // An empty inner string means no bins — valid for an empty item list.
        if (inner.Length == 0) {
            return new List<List<int>>();
        }

        // Split on top-level commas only (ignoring commas inside bin parentheses).
        List<string> binStrings = SplitTopLevel(inner);
        List<List<int>> bins = new List<List<int>>();

        foreach (string binStr in binStrings) {
            string binTrimmed = binStr.Trim();

            // Each individual bin must also be wrapped in parentheses: (a,b,c).
            if (binTrimmed.Length < 2 || binTrimmed[0] != '(' || binTrimmed[binTrimmed.Length - 1] != ')') {
                throw new FormatException("each bin must be wrapped in parentheses");
            }

            // Strip the bin's parentheses and parse the comma-separated integers.
            string binInner = binTrimmed.Substring(1, binTrimmed.Length - 2).Trim();
            List<int> bin = new List<int>();
            if (binInner.Length > 0) {
                foreach (string token in binInner.Split(',')) {
                    bin.Add(int.Parse(token.Trim()));
                }
            }
            bins.Add(bin);
        }

        return bins;
    }

    // Splits a string on commas that are NOT inside any parentheses.
    // For example: "(4,6),(7,3),(2,8)" → ["(4,6)", "(7,3)", "(2,8)"]
    // A depth counter goes up on '(' and down on ')'; we only split at depth 0.
    private List<string> SplitTopLevel(string s) {
        List<string> parts = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < s.Length; i++) {
            char c = s[i];
            if      (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0) {
                // Found a top-level comma — everything from start to here is one bin.
                parts.Add(s.Substring(start, i - start));
                start = i + 1;
            }
        }

        // Don't forget the last segment after the final comma.
        parts.Add(s.Substring(start));
        return parts;
    }
}
