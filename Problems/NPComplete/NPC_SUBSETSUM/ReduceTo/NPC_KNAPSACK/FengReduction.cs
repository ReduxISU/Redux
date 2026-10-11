using API.Interfaces;
using API.Problems.NPComplete.NPC_KNAPSACK;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_KNAPSACK;

class FengReduction : IReduction<SUBSETSUM, KNAPSACK> {

    // --- Fields ---
    public string reductionName { get; } = "Feng's Knapsack Reduction";
    public string reductionDefinition { get; } = "Fengs reduction converts positive integers in SUBSETSUM to items in KNAPSACK";
    public string source { get; } = "Feng, Thomas";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Steiner_tree_problem";
    public string[] contributors { get; } = { "Garret Stouffer", "Daniel Igbokwe" };
    // reduce() maps each SUBSETSUM integer to exactly one KNAPSACK item (n,n) — a
    // straight 1:1 mapping, O(n).
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. SUBSETSUM is essentially KNAPSACK restricted to the
    // sub-space where each item's value equals its weight -- a straight 1:1 mapping,
    // not a gadget construction.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. One pass over the input integers.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;

    private string _complexity = "O(n)";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private SUBSETSUM _reductionFrom;
    private KNAPSACK _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public SUBSETSUM reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public KNAPSACK reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }
    public string complexity {
        get {
            return _complexity;
        }
    }


    // --- Methods Including Constructors ---
    public FengReduction(SUBSETSUM from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public FengReduction(string instance) : this(new SUBSETSUM(instance)) { }
    public FengReduction() : this(new SUBSETSUM()) { }
    public KNAPSACK reduce() {
        SUBSETSUM SUBSETSUMInstance = _reductionFrom;
        KNAPSACK reducedKNAPSACK = new KNAPSACK {
            //We reduce by setting T from SUBSETSUM equal to both the minimum value and maxmimum weight constraints. 
            W = SUBSETSUMInstance.T,
            V = SUBSETSUMInstance.T
        };

        // We reduce the set of integers to a set of items by having each integer n equal (n,n) as an item. 
        List<string> integers = SUBSETSUMInstance.S;
        UtilCollection items = new UtilCollection("{}");
        for (int i = 0; i < SUBSETSUMInstance.S.Count; i++) {
            UtilCollection item = new UtilCollection($"({integers[i]},{integers[i]})");
            items.Add(item);
            _gadgetMap[integers[i]] = integers[i];
        }

        reducedKNAPSACK.items = items;

        reducedKNAPSACK.instance = $"({items},{reducedKNAPSACK.W},{reducedKNAPSACK.V})";

        reductionTo = reducedKNAPSACK;
        return reducedKNAPSACK;
    }

    /// <summary>
    /// Maps a Subset Sum answer to a Knapsack answer (#828): each chosen number n becomes the item (n,n). The chosen numbers
    /// add up to T, so the items weigh T (at most W = T) and are worth T (at least V = T). A number S does not have is a 400.
    /// </summary>
    public string mapSolutions(string problemFromSolution) {
        string text = problemFromSolution.Trim();
        List<string> chosen;
        try {
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}') throw new FormatException();
            chosen = new UtilCollection(text).ToList().Select(x => x.ToString()).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{problemFromSolution}' is not a set of numbers like {{1,12,15}}", ex);
        }
        foreach (string number in chosen)
            if (!reductionFrom.S.Contains(number))
                throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{number}' is not a number of S");
        return "{" + string.Join(",", chosen.Select(n => $"({n},{n})")) + "}";
    }
}
// return an instance of what you are reducing to
