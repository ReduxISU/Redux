using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PARTITION.Verifiers;

class PartitionVerifier : IVerifier<PARTITION> {
    public const string CertificateGrammar = "(S1),(S2) | S1,S2 partition S exactly (each element used once), sum(S1) = sum(S2)";
    public const string CertificateExample = "(33,21,15,6),(1,7,12,12,11,5,9,18)";

    // --- Fields ---
    public string verifierName { get; } = "Partition Solution Verifier";
    public string verifierDefinition { get; } = "This is a verifier for the Partition problem";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public PartitionVerifier() {

    }

    // Certificates come in two shapes: the documented bare pair
    // ("(33,21,15),(1,7,12,11,5,6,9,18)", no enclosing collection) and the form
    // PartitionBruteForce actually emits, which wraps that pair in an outer
    // "{...}". Try reading it as-is first (covers the solver's output); if that
    // isn't a single collection of two sides, wrap it in parens ourselves.
    // The sides are read from the text, not with SPADE's sets, so a number written twice stays twice (#628): S is a
    // multiset, and "{(3),(3)}" for {3,3} is two sides, not one. TODO(#628): once SPADE releases "is multiset", this can
    // read the sides with SPADE again.
    // Public so the reductions that read a Partition answer (Subset Sum to Partition, Partition to Weighted Cut) read it the
    // same way, repeats included.
    public static List<List<string>>? ReadSides(string certificate) {
        foreach (string text in new[] { certificate, "(" + certificate + ")" }) {
            List<string>? sides = RepeatedElements.CollectionElements(text);
            if (sides == null || sides.Count != 2) continue;
            List<string>? first = RepeatedElements.CollectionElements(sides[0]);
            List<string>? second = RepeatedElements.CollectionElements(sides[1]);
            if (first == null || second == null) continue;
            return new List<List<string>> { first, second };
        }
        return null;
    }

    /// <summary>
    /// The side (0 for S1, 1 for S2) of each number of S, by position, as <paramref name="certificate"/> splits them: each
    /// number on a side takes the first copy of it in S not yet placed, so two 12s on different sides are two different
    /// positions. Null when the certificate is not two sides of numbers of S, or does not place every number of S once.
    /// </summary>
    public static int[]? SidesByPosition(PARTITION problem, string certificate) {
        List<List<string>>? groups = ReadSides(certificate);
        if (groups == null) return null;
        List<int> numbers = problem.S.Select(int.Parse).ToList();
        int[] side = Enumerable.Repeat(-1, numbers.Count).ToArray();
        for (int s = 0; s < 2; s++) {
            foreach (string a in groups[s]) {
                if (!int.TryParse(a, out int number)) return null;
                int at = Enumerable.Range(0, numbers.Count).FirstOrDefault(i => side[i] < 0 && numbers[i] == number, -1);
                if (at < 0) return null;
                side[at] = s;
            }
        }
        return side.Contains(-1) ? null : side;
    }

    public bool verify(PARTITION problem, string certificate) {
        List<List<string>>? groups = ReadSides(certificate);
        if (groups == null) return false;

        // S as a multiset (number -> copies not yet placed): the two sides must use every number of S exactly as many times
        // as it occurs, so 12 can be on the sides twice only when S has two 12s.
        Dictionary<int, int> unplaced = new Dictionary<int, int>();
        foreach (string a in problem.S) {
            int number = int.Parse(a);
            unplaced[number] = unplaced.GetValueOrDefault(number) + 1;
        }

        long[] sums = new long[2];
        for (int side = 0; side < 2; side++) {
            foreach (string a in groups[side]) {
                if (!int.TryParse(a, out int number) || unplaced.GetValueOrDefault(number) == 0) return false;
                unplaced[number]--;
                sums[side] += number;
            }
        }

        return unplaced.Values.All(left => left == 0) && sums[0] == sums[1];
    }


}
