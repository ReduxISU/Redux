using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_KNAPSACK;
using SPADE;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_KNAPSACK;

class FengReduction : IReversibleReduction<SUBSETSUM, KNAPSACK, Placement>, IRecordedReduction<SUBSETSUM, KNAPSACK> {

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
    public FengReduction(SUBSETSUM from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private FengReduction(SUBSETSUM from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public FengReduction(string instance) : this(new SUBSETSUM(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public FengReduction(string instance, StepRecorder<BuildDelta> rec) : this(new SUBSETSUM(instance), rec) { }
    public FengReduction() : this(new SUBSETSUM()) { }

    /// <summary>
    /// The gadgets (#695), in the Packing picture's ids on both sides: per number of S, an element (Subset Sum's item I{i} to
    /// the Knapsack item I{i}, (n,n)); and the bound, Subset Sum's container (its target T) to the knapsack (W = V = T).
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new FengReduction(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public KNAPSACK reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per number of S (its item), then the knapsack with W and V.
    private KNAPSACK Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
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
            string id = PackingFrames.ItemId(i), n = integers[i];
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { id }, new List<string> { id }));
            rec.Accept(() => new BuildDelta { SourceFocus = [id], Nodes = [id], Gadgets = [gadget] },
                () => $"Number {n} becomes the item ({n},{n}): weight {n} and value {n}, so it adds as much to the knapsack's weight as to its value.",
                id);
        }
        int t = SUBSETSUMInstance.T;
        int bound = gadgets.Count;
        string container = PackingFrames.ContainerId(0);
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string> { container }, new List<string> { container }, note: $"W = V = T = {t}"));

        reducedKNAPSACK.items = items;

        reducedKNAPSACK.instance = $"({items},{reducedKNAPSACK.W},{reducedKNAPSACK.V})";

        if (rec.Enabled)
            rec.Done(new BuildDelta { SourceFocus = [container], Nodes = [container], Gadgets = [bound], Bound = t }, true,
                $"The target T = {t} becomes both the weight limit W = {t} and the value goal V = {t}. An item's weight equals its value, so items that weigh at most {t} and are worth at least {t} add up to exactly {t}.");

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

    public Placement EmptyAnswer() => new Placement([]);

    // "{}" on either side keeps the default (the verifiers): the empty knapsack is an answer exactly when T = 0, and so is the
    // empty subset.

    /// <summary>
    /// Backward map (#695): item I{i} is (n,n) for the i-th number n of S, so the items in the knapsack are the numbers of the
    /// subset. One step per item of the knapsack instance, in order: in the knapsack (Accept) or left out (Reject).
    /// </summary>
    public (string Answer, Placement Partial) MapBack(string problemToSolution, StepRecorder<Placement> rec) {
        var keys = reductionTo.items.ToList().Select(PackingFrames.Key).ToList();
        var chosen = new bool[keys.Count];
        foreach (string key in ReadItems(problemToSolution)) {
            int i = Enumerable.Range(0, keys.Count).FirstOrDefault(k => !chosen[k] && keys[k] == key, -1);
            if (i < 0) throw new FormatException($"'{key}' is not an item of the Knapsack instance");
            chosen[i] = true;
        }
        var assign = Enumerable.Repeat(-1, keys.Count).ToArray();
        for (int i = 0; i < keys.Count; i++) {
            string n = reductionFrom.S[i], id = PackingFrames.ItemId(i);
            if (chosen[i]) {
                assign[i] = 0;
                rec.Accept(() => new Placement(assign), () => $"Item ({n},{n}) is in the knapsack, so {n} is in the subset.", id);
            } else {
                rec.Reject(() => new Placement(assign), () => $"Item ({n},{n}) is not in the knapsack, so {n} is left out of the subset.", id);
            }
        }
        var subset = Enumerable.Range(0, keys.Count).Where(i => chosen[i]).Select(i => reductionFrom.S[i]);
        return ("{" + string.Join(",", subset) + "}", new Placement(assign));
    }

    // A Knapsack answer, {(1,1),(12,12)}: the chosen items as (weight,value) pairs. Malformed text throws FormatException,
    // which the endpoint reports as a 400.
    private static List<string> ReadItems(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of items in braces like {{(1,1),(12,12)}}");
        try {
            var items = new UtilCollection(text).ToList();
            foreach (var item in items) item.assertPair();
            return items.Select(PackingFrames.Key).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not a set of (weight,value) items like {{(1,1),(12,12)}}", ex);
        }
    }
}
// return an instance of what you are reducing to
