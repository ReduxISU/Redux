using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_SUBSETSUM;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.ReduceTo.NPC_SUBSETSUM;

class KarpExactCoverToSubsetSum : IReduction<EXACTCOVER, SUBSETSUM> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's Subset Sum Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Exact Cover to Subset Sum";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // The e[j,i] loop is |S| x |X|, and each of the |S| output numbers needs a full
    // |X|-digit base-(|S|+1) positional encoding regardless of how sparse each set is
    // — total output (string) size is O(|S| * |X|), even though the element COUNT of
    // the output (|S| numbers) is only O(|S|).
    public ReductionCost cost { get; } = ReductionCost.Quadratic;

    // Declared, not derived. The base-(|S|+1) positional encoding requires every
    // digit position to share a common base so sums can't carry into each other --
    // a global coordination constraint, not an independent per-element replacement.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. The e[j,i] loop is |S| x |X|.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(|S| * |X|), S = subsets, X = universal set";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private EXACTCOVER _reductionFrom;
    private SUBSETSUM _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public EXACTCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public SUBSETSUM reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public KarpExactCoverToSubsetSum(EXACTCOVER from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public KarpExactCoverToSubsetSum(string instance) : this(new EXACTCOVER(instance)) { }
    public KarpExactCoverToSubsetSum() : this(new EXACTCOVER()) { }
    public SUBSETSUM reduce() {
        EXACTCOVER ExactCoverInstance = _reductionFrom;
        SUBSETSUM reducedSUBSETSUM = new SUBSETSUM();

        // Each set is a number in base d with one digit per element of X: a 1 where the set holds that element. d = |S| + 1
        // keeps the digits apart however many sets are added (a digit sum never reaches d, so nothing carries); with no sets
        // any base does, and base 1 has no digits, so it is at least 2 (#829).
        int r = reductionFrom.S.Count;
        int d = Math.Max(r + 1, 2);
        int digits = reductionFrom.X.Count;

        // The numbers grow as d^|X|; Subset Sum holds ints. T, the sum of every digit's 1, is the largest number written and
        // bounds every sum a solver or verifier adds up, so the instance is refused (400) when T does not fit (#829).
        long K;
        var place = new long[digits];
        try {
            long power = 1;
            K = 0;
            for (int i = 0; i < digits; i++) {
                place[i] = power;
                K = checked(K + power);
                if (i < digits - 1) power = checked(power * d);
            }
        } catch (OverflowException) {
            throw TooBig(digits, d);
        }
        if (K > int.MaxValue) throw TooBig(digits, d);

        List<string> subsetSumS = new List<string>();
        for (int j = 0; j < reductionFrom.S.Count; j++) {
            long sum = 0;
            for (int i = 0; i < reductionFrom.X.Count; i++) {
                if (reductionFrom.S[j].Contains(reductionFrom.X[i])) sum += place[i];
            }
            subsetSumS.Add(sum.ToString());
        }

        string instance = "({" + string.Join(",", subsetSumS) + "}," + K + ")";

        reducedSUBSETSUM.S = subsetSumS;
        reducedSUBSETSUM.T = (int)K;
        reducedSUBSETSUM.instance = instance;

        reductionTo = reducedSUBSETSUM;
        return reducedSUBSETSUM;
    }

    private ReductionInputException TooBig(int digits, int d) =>
        new(this, _reductionFrom.instance, _reductionFrom.instanceFormat,
            $"the sets become numbers with {digits} digits in base {d} (one digit per element of X, base |S| + 1), and the target T = 11...1 in base {d} is past Subset Sum's largest integer ({int.MaxValue}); use fewer elements or sets");

    /// <summary>
    /// Maps an Exact Cover answer to a Subset Sum answer (#828): each chosen set becomes its number. The sets of an exact
    /// cover hold every element once, so their numbers have a 1 in every digit and add up to T. A set S does not have is a 400.
    /// </summary>
    public string mapSolutions(string reductionFromSolution) {
        var used = new bool[reductionFrom.S.Count];
        var numbers = new List<string>();
        foreach (var listed in ReadSets(reductionFromSolution)) {
            int j = Enumerable.Range(0, reductionFrom.S.Count).FirstOrDefault(k => !used[k] && reductionFrom.S[k].ToHashSet(StringComparer.Ordinal).SetEquals(listed), -1);
            if (j < 0)
                throw new ReductionInputException(this, reductionFromSolution, reductionFrom.certificateFormat, $"'{{{string.Join(",", listed)}}}' is not a set of S");
            used[j] = true;
            numbers.Add(reductionTo.S[j]);
        }
        return "{" + string.Join(",", numbers) + "}";
    }

    // An Exact Cover answer, {{2,3},{4,1}}: the chosen sets by their elements.
    private List<HashSet<string>> ReadSets(string certificate) {
        string text = certificate.Trim();
        try {
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}') throw new FormatException();
            return new SPADE.UtilCollection(text).ToList().Select(s => s.ToList().Select(e => e.ToString()).ToHashSet(StringComparer.Ordinal)).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(this, certificate, reductionFrom.certificateFormat, $"'{certificate}' is not a set of sets like {{{{2,3}},{{4,1}}}}", ex);
        }
    }

}
