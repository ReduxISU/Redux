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
using SPADE;

namespace API.Problems.NPComplete.NPC_BINPACKING.Verifiers;

class BinPackingVerifier : IVerifier<BINPACKING> {
    public const string CertificateGrammar = "(bin1,...,binM) | each bin is (item,...), S partitioned exactly across bins, sum(bin) <= C, M <= K";
    public const string CertificateExample = "((8,2),(7,3),(6,4))";

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
        } catch {
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
    // where each bin is a list of integers, using SPADE's UtilCollection.
    // The outer collection is the ordered list of bins; each bin is itself an
    // ordered list of item sizes. assertOrdered() throws (caught by the caller
    // in verify()) if either level isn't the expected list shape.
    private List<List<int>> ParseCertificate(string cert) {
        UtilCollection bins = new UtilCollection(cert);
        bins.assertOrdered();

        List<List<int>> result = new List<List<int>>();
        foreach (UtilCollection bin in bins) {
            bin.assertOrdered();
            List<int> items = new List<int>();
            foreach (UtilCollection item in bin) {
                items.Add(item.parseInt());
            }
            result.Add(items);
        }
        return result;
    }
}
