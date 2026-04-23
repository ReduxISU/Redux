using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BINPACKING.Verifiers;

class BinPackingVerifier : IVerifier<BINPACKING> {

    // --- Fields ---
    public string verifierName { get; } = "Bin Packing Verifier";
    public string verifierDefinition { get; } = "Polynomial-time verifier for Bin Packing. Given a problem instance <S, C, K> and a candidate certificate representing a partition of S into bins, the verifier checks three conditions: (1) the multiset of item sizes appearing in the certificate equals S exactly (every item appears the correct number of times, no items missing or added), (2) the sum of sizes in each bin does not exceed C, and (3) the number of non-empty bins does not exceed K. The verifier runs in O(n log n) time, dominated by the sort used for multiset comparison.";
    public string source { get; } = "Garey, M. R., and Johnson, D. S. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SR1.";
    public string[] contributors { get; } = { "TBD" };

    private string _complexity = "O(n log n)";
    private string _certificate = "((8,2),(7,3),(6,4))";

    public string complexity {
        get { return _complexity; }
    }

    public string certificate {
        get { return _certificate; }
    }

    // --- Methods Including Constructors ---
    public BinPackingVerifier() {
    }

    public bool verify(BINPACKING problem, string certificate) {
        List<List<int>> bins;
        try {
            bins = ParseCertificate(certificate);
        }
        catch {
            return false;
        }

        int nonEmptyBinCount = 0;
        List<int> flatItems = new List<int>();
        foreach (List<int> bin in bins) {
            if (bin.Count == 0) continue;
            nonEmptyBinCount++;

            int binSum = 0;
            foreach (int size in bin) {
                if (size <= 0) return false;
                binSum += size;
                flatItems.Add(size);
            }
            if (binSum > problem.C) return false;
        }

        if (nonEmptyBinCount > problem.K) return false;

        List<int> expected = problem.S.OrderBy(x => x).ToList();
        List<int> actual = flatItems.OrderBy(x => x).ToList();
        if (!expected.SequenceEqual(actual)) return false;

        return true;
    }

    private List<List<int>> ParseCertificate(string cert) {
        if (cert == null) throw new FormatException("certificate is null");
        string trimmed = cert.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '(' || trimmed[trimmed.Length - 1] != ')') {
            throw new FormatException("certificate must be wrapped in parentheses");
        }

        string inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
        if (inner.Length == 0) {
            return new List<List<int>>();
        }

        List<string> binStrings = SplitTopLevel(inner);
        List<List<int>> bins = new List<List<int>>();
        foreach (string binStr in binStrings) {
            string binTrimmed = binStr.Trim();
            if (binTrimmed.Length < 2 || binTrimmed[0] != '(' || binTrimmed[binTrimmed.Length - 1] != ')') {
                throw new FormatException("each bin must be wrapped in parentheses");
            }
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

    private List<string> SplitTopLevel(string s) {
        List<string> parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < s.Length; i++) {
            char c = s[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0) {
                parts.Add(s.Substring(start, i - start));
                start = i + 1;
            }
        }
        parts.Add(s.Substring(start));
        return parts;
    }
}
