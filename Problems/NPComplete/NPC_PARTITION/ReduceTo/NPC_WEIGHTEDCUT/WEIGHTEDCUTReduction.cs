using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;

namespace API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;

class WEIGHTEDCUTReduction : IReduction<PARTITION, WEIGHTEDCUT> {

    // --- Fields ---
    public string reductionName { get; } = "Weighted Cut Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Graph Coloring to Clique Cover";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // reduce() builds a complete weighted graph (all i&lt;j pairs) over the n input
    // numbers — O(n^2) edges from an O(n)-sized input.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;

    // Declared, not derived. The all-pairs product-weighted edges and the global cut
    // threshold (sum^2/4, computed over every element) only work together -- no single
    // edge or node is an independent, self-contained gadget.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. Nested i&lt;j loop over the n input numbers.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n^2), n = |PARTITION.S|";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private PARTITION _reductionFrom;
    private WEIGHTEDCUT _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public PARTITION reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public WEIGHTEDCUT reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public WEIGHTEDCUTReduction(PARTITION from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public WEIGHTEDCUTReduction(string instance) : this(new PARTITION(instance)) { }
    public WEIGHTEDCUTReduction() : this(new PARTITION()) { }
    public WEIGHTEDCUT reduce() {
        PARTITION PARTITIONInstance = _reductionFrom;
        WEIGHTEDCUT reducedWEIGHTEDCUT = new WEIGHTEDCUT();

        List<string> nodes = new List<string>();
        List<(string source, string destination, int weight)> edges = new List<(string source, string destination, int weight)>();
        List<string> written = new List<string>();
        var numbers = reductionFrom.S.Select(int.Parse).ToList();

        for (int i = 1; i <= numbers.Count; i++)
            nodes.Add(i.ToString());

        // Node i is the i-th number; every two nodes are joined by an edge weighing the product of their numbers. Weights and K
        // are worked out in long, so a product or square that does not fit Weighted Cut's integers is a 400, never a wrap (#830).
        for (int i = 0; i < numbers.Count; i++) {
            for (int j = i + 1; j < numbers.Count; j++) {
                int weight = FitInt((long)numbers[i] * numbers[j], $"the edge {{{i + 1},{j + 1}}} weighs {numbers[i]} * {numbers[j]}");
                // No space after the comma here -- SPADE's parser fails on it (confirmed empirically;
                // WEIGHTEDCUT's own defaultInstance has no space at this position either).
                written.Add($"({{{i + 1},{j + 1}}},{weight})");
                edges.Add(((i + 1).ToString(), (j + 1).ToString(), weight));
            }
        }

        // A split into sides adding up to a and b cuts edges weighing a * b in all, which reaches sum^2 / 4 exactly when
        // a = b. When the sum is odd no split is even, and the best one weighs (sum^2 - 1) / 4, which integer division would
        // round sum^2 / 4 down to (#830): K is then (sum^2 + 3) / 4, one more, which no cut reaches.
        long total = numbers.Sum(n => (long)n);
        long square;
        try {
            square = checked(total * total);
        } catch (OverflowException) {
            throw TooBig($"the sum of the numbers, {total}, squared");
        }
        int k = FitInt(total % 2 == 0 ? square / 4 : (square + 3) / 4, $"K, from the sum of the numbers {total}");

        // One number gives one node and no edges: "(({1},{}),K)" (#830).
        string instance = $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", written)}}}),{k})";

        reducedWEIGHTEDCUT.K = k;
        reducedWEIGHTEDCUT.nodes = nodes;
        reducedWEIGHTEDCUT.edges = edges;
        reducedWEIGHTEDCUT.instance = instance;

        reductionTo = reducedWEIGHTEDCUT;
        return reducedWEIGHTEDCUT;
    }

    private int FitInt(long value, string what) =>
        value is >= int.MinValue and <= int.MaxValue ? (int)value : throw TooBig($"{what} = {value}");

    private ReductionInputException TooBig(string what) =>
        new(this, _reductionFrom.instance, _reductionFrom.instanceFormat,
            $"{what} does not fit Weighted Cut's integers (at most {int.MaxValue}); use smaller numbers");

    /// <summary>
    /// Maps a Partition answer to a Weighted Cut answer (#740): node i is the i-th number of S, so the cut between the two
    /// sides is every edge {i,j} whose numbers are on different sides, with its weight. The two sides must hold every number
    /// of S once; anything else is a 400.
    /// </summary>
    public string mapSolutions(string reductionFromSolution) {
        List<List<string>> sides;
        try {
            SPADE.UtilCollection pair;
            try {
                pair = new SPADE.UtilCollection(reductionFromSolution);
                pair.assertCount(2);
            } catch {
                pair = new SPADE.UtilCollection("(" + reductionFromSolution + ")");
                pair.assertCount(2);
            }
            sides = pair.ToList().Select(group => group.ToList().Select(x => x.ToString()).ToList()).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(this, reductionFromSolution, reductionFrom.certificateFormat, $"'{reductionFromSolution}' is not two sides like (1,7),(8)", ex);
        }
        var side = new int?[reductionFrom.S.Count];
        for (int s = 0; s < 2; s++) {
            foreach (string number in sides[s]) {
                int i = Enumerable.Range(0, reductionFrom.S.Count).FirstOrDefault(k => reductionFrom.S[k] == number && side[k] == null, -1);
                if (i < 0)
                    throw new ReductionInputException(this, reductionFromSolution, reductionFrom.certificateFormat, $"'{number}' is not a number of S, or is on the sides twice");
                side[i] = s;
            }
        }
        int missing = Array.IndexOf(side, null);
        if (missing >= 0)
            throw new ReductionInputException(this, reductionFromSolution, reductionFrom.certificateFormat, $"'{reductionFromSolution}' leaves out the number {reductionFrom.S[missing]}");
        var cut = reductionTo.edges.Where(e => side[int.Parse(e.source) - 1] != side[int.Parse(e.destination) - 1])
            .Select(e => $"({{{e.source},{e.destination}}},{e.weight})");
        return "{" + string.Join(",", cut) + "}";
    }
}
// return an instance of what you are reducing to