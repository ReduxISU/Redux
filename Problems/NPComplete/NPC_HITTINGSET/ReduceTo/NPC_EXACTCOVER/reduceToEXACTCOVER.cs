using System.ComponentModel;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_EXACTCOVER;
using SPADE;
using Microsoft.Net.Http.Headers;

namespace API.Problems.NPComplete.NPC_HITTINGSET.ReduceTo.NPC_EXACTCOVER;

class reduceToEXACTCOVER : IReversibleReduction<HITTINGSET, EXACTCOVER, NodeSet>, IRecordedReduction<HITTINGSET, EXACTCOVER> {

    // --- Fields ---
    public string reductionName { get; } = "Exact Cover Reduction";
    public string reductionDefinition { get; } = "Reduction from Exact Hitting Set to Exact Cover by transposing which elements belong to which subsets. Karp (1972, p. 99) gives the same transpose in the other direction, from Exact Cover to Hitting Set.";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Russell Phillips" };
    // reduce() transposes the input's own subset/element incidence structure (one
    // output entry per (item, subset) pair where the item IS a member) — that
    // incidence count is already what the HITTINGSET instance's own encoding lists,
    // so output size tracks input size rather than blowing up beyond it.
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. Transposes the input's own subset/element incidence
    // structure into EXACTCOVER's shape -- the same combinatorial object re-expressed,
    // no new gadgetry built.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. One pass over subsets to number them, then one pass over
    // universalSet x subSets to transpose the incidence relation.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(subSets * universalSet), sizes of HITTINGSET's own sets";


    private HITTINGSET _reductionFrom;
    private EXACTCOVER _reductionTo;


    // --- Properties ---
    public HITTINGSET reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public EXACTCOVER reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public reduceToEXACTCOVER(HITTINGSET from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private reduceToEXACTCOVER(HITTINGSET from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public reduceToEXACTCOVER(string instance) : this(new HITTINGSET(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public reduceToEXACTCOVER(string instance, StepRecorder<BuildDelta> rec) : this(new HITTINGSET(instance), rec) { }
    public reduceToEXACTCOVER() : this(new HITTINGSET()) { }

    /// <summary>
    /// The gadgets (#695), in the Bipartite Graph's ids on both sides: per subset, an element (Hitting Set's set S{i} to Exact
    /// Cover's element i); per item, an element (Hitting Set's element to the Exact Cover set of the subsets that hold it).
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    // The item each set of the target was made from (the first, when items are in the same subsets), by the set's index among
    // the sets Exact Cover reads. Private, so it stays out of the JSON.
    private readonly List<string> _itemOfSet = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new reduceToEXACTCOVER(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public EXACTCOVER reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per subset (its element), then one per item (its set of subsets).
    private EXACTCOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        _itemOfSet.Clear();
        UtilCollection universal = new UtilCollection("{}");
        Dictionary<UtilCollection, int> setsToElement = new Dictionary<UtilCollection, int>();
        int elementNum = 1;
        foreach (UtilCollection set in _reductionFrom.subSets) {
            setsToElement.Add(set, elementNum);
            universal.Add(new UtilCollection(elementNum.ToString()));
            elementNum++;
        }

        UtilCollection subsets = new UtilCollection("{}");
        foreach (UtilCollection item in _reductionFrom.universalSet) {
            UtilCollection newSubset = new UtilCollection("{}");
            foreach (UtilCollection set in _reductionFrom.subSets) {
                if (set.Contains(item)) {
                    newSubset.Add(new UtilCollection(setsToElement.GetValueOrDefault(set).ToString()));
                }
            }
            subsets.Add(newSubset);
        }

        string input = "(" + universal.ToString() + "," + subsets.ToString() + ")";
        reductionTo = new EXACTCOVER(input);

        // Subset i (0-based, the order Hitting Set's picture names them S{i+1}) is the element i + 1; item x is the set of the
        // subsets that hold x. Exact Cover reads equal sets as one (two items in the same subsets), so an item's set is its
        // place among the sets Exact Cover reads.
        var sourceSets = _reductionFrom.subSets.ToList();
        var items = _reductionFrom.universalSet.ToList().Select(x => x.ToString()).ToList();
        var targetSets = reductionTo.S;
        _itemOfSet.AddRange(Enumerable.Repeat("", targetSets.Count));
        for (int i = 0; i < sourceSets.Count; i++) {
            string subset = BipartiteFrames.SetId(i), element = (i + 1).ToString();
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { subset }, new List<string> { BipartiteFrames.ElementId(element) }));
            var members = sourceSets[i].ToList().Select(x => x.ToString()).ToList();
            rec.Accept(() => new BuildDelta { SourceFocus = [subset], Nodes = [BipartiteFrames.ElementId(element)], Gadgets = [gadget] },
                () => $"Subset {subset} {BipartiteFrames.Braces(members)} becomes the element {element}: Exact Cover covers it exactly once, as a hitting set hits the subset exactly once.",
                BipartiteFrames.ElementId(element));
        }
        var built = new HashSet<int>();
        foreach (string item in items) {
            var holders = Enumerable.Range(0, sourceSets.Count).Where(i => sourceSets[i].Contains(new UtilCollection(item))).ToList();
            var mine = holders.Select(i => (i + 1).ToString()).ToHashSet(StringComparer.Ordinal);
            int set = targetSets.FindIndex(s => s.ToHashSet(StringComparer.Ordinal).SetEquals(mine));
            string setId = BipartiteFrames.SetId(set), element = BipartiteFrames.ElementId(item);
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { element }, new List<string> { setId }));
            bool again = !built.Add(set);
            string owner = _itemOfSet[set];
            if (owner == "") _itemOfSet[set] = item;
            string[] sourceFocus = holders.Select(i => BipartiteFrames.EdgeId(element, BipartiteFrames.SetId(i))).Prepend(element).ToArray();
            BuildEdge[] memberships = again ? [] : targetSets[set].Distinct(StringComparer.Ordinal).Select(e => BipartiteFrames.BuildMembership(e, set)).ToArray();
            string[] focus = targetSets[set].Select(BipartiteFrames.ElementId).Append(setId).Distinct(StringComparer.Ordinal).ToArray();
            string text = BipartiteFrames.Braces(holders.Select(i => (i + 1).ToString()).ToList());
            rec.Accept(() => new BuildDelta { SourceFocus = sourceFocus, Nodes = again ? [] : [setId], Edges = BuildEdge.Sorted(memberships), Gadgets = [gadget] },
                () => again
                    ? $"Item {item} is in the same subsets as {owner}, so its set is {setId} {text} too: Exact Cover reads equal sets as one."
                    : holders.Count == 0
                        ? $"Item {item} is in no subset, so it becomes the empty set {setId}, which covers nothing."
                        : $"Item {item} is in {(holders.Count == 1 ? "subset" : "subsets")} {BuildCaptions.Names(holders.Select(i => BipartiteFrames.SetId(i)).ToList())}, so it becomes the set {setId} {text} of their elements: taking {item} into the hitting set covers each of them once.",
                focus);
        }
        if (rec.Enabled)
            rec.Done(new BuildDelta(), true, sourceSets.Count == 0
                ? "Hitting Set has no subsets to hit, so Exact Cover has no elements to cover: choosing nothing does it."
                : $"Exact Cover asks for sets that cover each of the {Words.Count(sourceSets.Count, "element")} exactly once: items that hit each subset exactly once.");

        return reductionTo;
    }

    /// <summary>
    /// Maps a Hitting Set answer to an Exact Cover answer (#828): each chosen item becomes its set, the numbers of the
    /// subsets that hold it. An exact hitting set meets every subset once, so those sets hold every subset's number once.
    /// An item U does not have is a 400.
    /// </summary>
    public string mapSolutions(string problemFromSolution) {
        string text = problemFromSolution.Trim();
        List<UtilCollection> items;
        try {
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}') throw new FormatException();
            items = new UtilCollection(text).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{problemFromSolution}' is not a set of items like {{1,2}}", ex);
        }
        var subsets = _reductionFrom.subSets.ToList();
        var sets = new List<string>();
        foreach (UtilCollection item in items) {
            if (!_reductionFrom.universalSet.Contains(item))
                throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{item}' is not an item of U");
            var hit = Enumerable.Range(0, subsets.Count).Where(i => subsets[i].Contains(item)).Select(i => (i + 1).ToString());
            sets.Add("{" + string.Join(",", hit) + "}");
        }
        return "{" + string.Join(",", sets) + "}";
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // "{}" on either side keeps the default (the verifiers): with no subsets the empty cover and the empty hitting set are both
    // answers.

    /// <summary>
    /// Backward map (#695): each set of the exact cover is the set of some item, the subsets that hold it, so that item hits
    /// those subsets. The sets are named in the answer by their elements (two items in the same subsets have one set, and
    /// either hits them: the first is taken).
    /// </summary>
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var target = reductionTo.S;
        var used = new bool[target.Count];
        var hitting = new List<string>();
        foreach (var listed in ReadSets(problemToSolution)) {
            int p = Enumerable.Range(0, target.Count).FirstOrDefault(k => !used[k] && target[k].ToHashSet(StringComparer.Ordinal).SetEquals(listed), -1);
            if (p < 0) throw new FormatException($"'{{{string.Join(",", listed)}}}' is not a set of the Exact Cover instance");
            used[p] = true;
            string item = _itemOfSet[p], setId = BipartiteFrames.SetId(p);
            if (!hitting.Contains(item)) hitting.Add(item);
            rec.Accept(() => new NodeSet(hitting),
                () => $"Set {setId} {BipartiteFrames.Braces(target[p])} is in the exact cover. It is item {item}'s set, the subsets that hold {item}, so {item} is in the hitting set.",
                setId);
        }
        return (ReductionBack.FormatNodeSet(hitting), new NodeSet(hitting));
    }

    // An Exact Cover answer, {{1,3},{2}}: the chosen sets by their elements. Malformed text throws FormatException, which the
    // endpoint reports as a 400.
    private static List<HashSet<string>> ReadSets(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of sets in braces like {{{{1,3}},{{2}}}}");
        try {
            return new UtilCollection(text).ToList().Select(s => s.ToList().Select(e => e.ToString()).ToHashSet(StringComparer.Ordinal)).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not a set of sets like {{{{1,3}},{{2}}}}", ex);
        }
    }
}