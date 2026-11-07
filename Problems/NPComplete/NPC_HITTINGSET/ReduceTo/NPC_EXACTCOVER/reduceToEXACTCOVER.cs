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

// Exact Hitting Set to Exact Cover, by transposing which items belong to which subsets: every subset becomes an element to
// cover, and every item becomes the set of the subsets that hold it. It records the construction steps (one per subset,
// one per item, then the Done; see Build) and the backward map's steps (one per set of the exact cover; see MapBack).
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
    /// The gadgets, in the set problems' piece ids on both sides: per subset, an element (Hitting Set's set S{i} to Exact
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

    // How the construction steps show a subset: the subset in Hitting Set, the element it becomes in Exact Cover, and the
    // gadget that makes it. The state is (Subset, Gadget): the subset's index and its gadget's index.
    private static RunSteps<(int Subset, int Gadget), BuildDelta> NewSubsetSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the subset, its element, and the gadget.
        partial: s => new BuildDelta {
            SourceFocus = [SetPieces.SetId(s.Subset)],
            Nodes = [SetPieces.ElementId((s.Subset + 1).ToString())],
            Gadgets = [s.Gadget],
        },
        focus: s => [SetPieces.ElementId((s.Subset + 1).ToString())]);

    // How the construction steps show an item: the item and its memberships in Hitting Set; the set it becomes, with the
    // set's memberships, added to Exact Cover (nothing added when an earlier item made the same set); and its gadget. The
    // state is (Item, Holders, Set, Again, Gadget): the item's element id in the picture, the indices of the subsets that
    // hold it, its set's index among the sets Exact Cover reads, whether an earlier item already made that set, and the
    // gadget's index.
    private static RunSteps<(string Item, List<int> Holders, int Set, bool Again, int Gadget), BuildDelta> NewItemSteps(
            StepRecorder<BuildDelta> rec, List<List<string>> targetSets) => new(rec,
        // What the Reduction View shows: the item and its memberships, the set added, and the gadget.
        partial: s => new BuildDelta {
            SourceFocus = s.Holders.Select(i => SetPieces.EdgeId(s.Item, SetPieces.SetId(i))).Prepend(s.Item).ToArray(),
            Nodes = s.Again ? [] : [SetPieces.SetId(s.Set)],
            Edges = s.Again ? [] : BuildEdge.Sorted(targetSets[s.Set].Distinct(StringComparer.Ordinal).Select(e => SetPieces.BuildMembership(e, s.Set))),
            Gadgets = [s.Gadget],
        },
        // The set and every element in it.
        focus: s => targetSets[s.Set].Select(SetPieces.ElementId).Append(SetPieces.SetId(s.Set)).Distinct(StringComparer.Ordinal).ToArray());

    // Builds the Exact Cover instance, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept per subset, in Hitting Set's order: the subset becomes an element to cover;
    //   - an Accept per item, in U's order: the item becomes the set of the subsets that hold it (or names an earlier item's
    //     set again, when the two are in the same subsets: Exact Cover reads equal sets as one);
    //   - the Done: what Exact Cover asks for.
    // A construction has no verdicts, so Accept means "these pieces go into the target". The steps come from the very
    // decisions below, so what is recorded is what is built.
    // Gadgets: an element per subset (the subset to its element), then an element per item (the item to its set).
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
        // place among the sets Exact Cover reads. Each subset, then each item, is one step.
        var sourceSets = _reductionFrom.subSets.ToList();
        var items = _reductionFrom.universalSet.ToList().Select(x => x.ToString()).ToList();
        var targetSets = reductionTo.S;
        var subsetSteps = NewSubsetSteps(rec);
        var itemSteps = NewItemSteps(rec, targetSets);
        _itemOfSet.AddRange(Enumerable.Repeat("", targetSets.Count));
        for (int i = 0; i < sourceSets.Count; i++) {
            string element = (i + 1).ToString();
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { SetPieces.SetId(i) }, new List<string> { SetPieces.ElementId(element) }));
            subsetSteps.Accept((i, gadget),
                $"Subset {SetPieces.SetId(i)} {Words.Braces(sourceSets[i].ToList().Select(x => x.ToString()))} becomes the element {element}: Exact Cover covers it exactly once, as a hitting set hits the subset exactly once.");
        }
        // The sets an earlier item already made.
        var built = new HashSet<int>();
        foreach (string item in items) {
            var holders = Enumerable.Range(0, sourceSets.Count).Where(i => sourceSets[i].Contains(new UtilCollection(item))).ToList();
            var mine = holders.Select(i => (i + 1).ToString()).ToHashSet(StringComparer.Ordinal);
            int set = targetSets.FindIndex(s => s.ToHashSet(StringComparer.Ordinal).SetEquals(mine));
            string setId = SetPieces.SetId(set), element = SetPieces.ElementId(item);
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { element }, new List<string> { setId }));
            bool again = !built.Add(set);
            // The first item to make this set owns it; a later item in the same subsets names the same set.
            string owner = _itemOfSet[set];
            if (owner == "") _itemOfSet[set] = item;

            if (again)
                itemSteps.Accept((element, holders, set, again, gadget),
                    $"Item {item} is in the same subsets as {owner}, so its set is {setId} {Words.Braces(holders.Select(i => (i + 1).ToString()))} too: Exact Cover reads equal sets as one.");
            else if (holders.Count == 0)
                itemSteps.Accept((element, holders, set, again, gadget),
                    $"Item {item} is in no subset, so it becomes the empty set {setId}, which covers nothing.");
            else
                itemSteps.Accept((element, holders, set, again, gadget),
                    $"Item {item} is in {(holders.Count == 1 ? "subset" : "subsets")} {BuildCaptions.Names(holders.Select(i => SetPieces.SetId(i)).ToList())}, so it becomes the set {setId} {Words.Braces(holders.Select(i => (i + 1).ToString()))} of their elements: taking {item} into the hitting set covers each of them once.");
        }

        // The closing step, on either recorder (both record to rec).
        if (sourceSets.Count == 0)
            itemSteps.Done(new BuildDelta(), true,
                $"Hitting Set has no subsets to hit, so Exact Cover has no elements to cover: choosing nothing does it.");
        else
            itemSteps.Done(new BuildDelta(), true,
                $"Exact Cover asks for sets that cover each of the {Words.Count(sourceSets.Count, "element")} exactly once: items that hit each subset exactly once.");

        return reductionTo;
    }

    /// <summary>
    /// Maps a Hitting Set answer to an Exact Cover answer: each chosen item becomes its set, the numbers of the
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

    // How the map-back steps show a set read: the hitting set so far, and the set.
    private static RunSteps<string, NodeSet> NewMapSteps(StepRecorder<NodeSet> rec, List<string> hitting) => new(rec,
        // What every picture shows: the hitting set so far, and the set the step is about.
        partial: _ => new NodeSet(hitting),
        focus: setId => [setId]);

    // Backward map: each set of the exact cover is the set of some item, the subsets that hold it, so that item hits those
    // subsets. The sets are named in the answer by their elements (two items in the same subsets have one set, and either
    // hits them: the first is taken).
    // Its steps: an Accept per set of the exact cover, in the answer's order: its item is in the hitting set. The caller
    // adds the Done.
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var target = reductionTo.S;
        var used = new bool[target.Count];
        var hitting = new List<string>();
        var steps = NewMapSteps(rec, hitting);
        foreach (var listed in ReadSets(problemToSolution)) {
            int p = Enumerable.Range(0, target.Count).FirstOrDefault(k => !used[k] && target[k].ToHashSet(StringComparer.Ordinal).SetEquals(listed), -1);
            if (p < 0) throw new FormatException($"'{{{string.Join(",", listed)}}}' is not a set of the Exact Cover instance");
            used[p] = true;
            string item = _itemOfSet[p], setId = SetPieces.SetId(p);
            if (!hitting.Contains(item)) hitting.Add(item);
            steps.Accept(setId,
                $"Set {setId} {Words.Braces(target[p])} is in the exact cover. It is item {item}'s set, the subsets that hold {item}, so {item} is in the hitting set.");
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