using System.Linq;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_EXACTCOVER;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_EXACTCOVER;

// Karp's Graph Coloring to Exact Cover: every node, every edge, and every slot (a node at one of its edges, for one color)
// is an element; a node has one set per color (the node and its slots for that color), and an edge one set per pair of
// different colors of its ends (the edge and its ends' slots for every other color). It records the construction steps
// (one per node, one per edge, then the Done; see BuildPieces) and the backward map's steps (one per node color set read,
// then one per node left without a color; see MapBack).
class KarpGraphColorToExactCover : IReversibleReduction<GRAPHCOLORING, EXACTCOVER, Groups>, IRecordedReduction<GRAPHCOLORING, EXACTCOVER> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's Exact Cover Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Exact Cover to Subset Sum";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // The final gadget block ("foreach edge: for f1 in 1..K: for f2 in 1..K: build an
    // O(K)-sized subset") unconditionally emits O(m * K^3) new subset elements that
    // have no counterpart already in the GRAPHCOLORING input encoding.
    public ReductionCost cost { get; } = ReductionCost.HigherPolynomial;

    // Declared, not derived. Per-node "choose one color" subsets and per-edge
    // "no shared color" subsets must fit together in a single exact cover -- the
    // pieces are not independent, correctness depends on how they interact.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. The final gadget block emits O(m * K^3) subset entries.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(m * K^3), m = |edges|, K = number of colors";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private GRAPHCOLORING _reductionFrom;
    private EXACTCOVER _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public GRAPHCOLORING reductionFrom {
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
    public KarpGraphColorToExactCover(GRAPHCOLORING from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpGraphColorToExactCover(GRAPHCOLORING from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public KarpGraphColorToExactCover(string instance) : this(new GRAPHCOLORING(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpGraphColorToExactCover(string instance, StepRecorder<BuildDelta> rec) : this(new GRAPHCOLORING(instance), rec) { }
    public KarpGraphColorToExactCover() : this(new GRAPHCOLORING()) { }

    // Elements: a node u is "u", an edge {u,v} is "u_v" and the slot of u at edge {u,v} for color j is "u_u_v_j". A name
    // that holds "_" (or the escape character) is written with a \ before every \ and "_", so a node, an edge and a slot
    // never share an element; other names are written as they are.
    private static string EdgeElement(string u, string v) => ElementName(u) + "_" + ElementName(v);

    private static string SlotElement(string node, string u, string v, int color) => ElementName(node) + "_" + EdgeElement(u, v) + "_" + color;

    private static string ElementName(string name) =>
        name.IndexOfAny(['\\', '_']) < 0 ? name : name.Replace(@"\", @"\\").Replace("_", @"\_");

    /// <summary>
    /// The gadgets: per node a group (the node to its element, its color slots and its K color sets) and per edge a
    /// group (the edge to its element and its K * (K - 1) sets, one per pair of different colors of its ends). They are worked
    /// out the first time they are asked for (the Reduction View, /reduce's JSON, the backward map), not by every reduce():
    /// finding each set among the sets Exact Cover reads means parsing the target, which made reduce() some 25 times slower.
    /// </summary>
    public List<Gadget> gadgets {
        get {
            EnsurePieces();
            return _gadgets;
        }
    }

    private readonly List<Gadget> _gadgets = new();

    // The sets and instance of the last build, for working out the gadgets when first asked for; null once they are.
    private List<List<string>>? _pendingSubsets;
    private string? _pendingInstance;

    private void EnsurePieces() {
        if (_pendingSubsets is null) return;
        var (subsets, instance) = (_pendingSubsets, _pendingInstance!);
        _pendingSubsets = null;
        _pendingInstance = null;
        BuildPieces(subsets, instance, StepRecorder<BuildDelta>.Off);
    }

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpGraphColorToExactCover(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public EXACTCOVER reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Builds the Exact Cover instance. Its construction steps are recorded where the gadgets are worked out (see BuildPieces):
    // one per node, one per edge, then the Done.
    private EXACTCOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        _gadgets.Clear();
        _pendingSubsets = null;
        _pendingInstance = null;
        GRAPHCOLORING GRAPHCOLORINGInstance = _reductionFrom;
        EXACTCOVER reducedExactCover = new EXACTCOVER();

        List<string> universalSet = new List<string>();
        List<List<string>> subsets = new List<List<string>>();
        List<string> currentSubset = new List<string>();

        foreach (var i in reductionFrom.nodes) { //adding nodes to universal
            universalSet.Add(ElementName(i));
        }

        foreach (var i in reductionFrom.edges) {//adding edges to universal
            universalSet.Add(EdgeElement(i.Key, i.Value));
        }

        foreach (var u in reductionFrom.nodes) {
            foreach (var e in reductionFrom.edges) {
                //adding edges to universal
                if (e.Key == u || e.Value == u) {
                    for (int j = 1; j <= reductionFrom.K; j++) {
                        universalSet.Add(SlotElement(u, e.Key, e.Value, j));
                    }
                }

            }
        }

        foreach (var u in reductionFrom.nodes) {
            for (int j = 1; j <= reductionFrom.K; j++) {
                currentSubset.Add(ElementName(u));
                foreach (var e in reductionFrom.edges) {

                    if (e.Key == u || e.Value == u) {
                        currentSubset.Add(SlotElement(u, e.Key, e.Value, j));

                    }
                }
                subsets.Add(new List<string>(currentSubset));
                currentSubset.Clear();
            }


        }

        foreach (var e in reductionFrom.edges) {//adding edge, edge,color1, edge,color2
            for (int f1 = 1; f1 <= reductionFrom.K; f1++) {
                for (int f2 = 1; f2 <= reductionFrom.K; f2++) {
                    if (f1 != f2) {
                        currentSubset.Add(EdgeElement(e.Key, e.Value));
                        for (int i = 1; i <= reductionFrom.K; i++)
                            if (i != f1)
                                currentSubset.Add(SlotElement(e.Key, e.Key, e.Value, i));
                        for (int i = 1; i <= reductionFrom.K; i++)
                            if (i != f2)
                                currentSubset.Add(SlotElement(e.Value, e.Key, e.Value, i));
                        subsets.Add(new List<string>(currentSubset));
                        currentSubset.Clear();
                    }
                }
            }
        }


        // EXACTCOVER's grammar/constructor expects (U,S) -- U (universal set) first, S
        // (the subset collection) second, wrapped in parens -- matching its own
        // defaultInstance shape ("({1,2,3,4},{{1,2,3},{2,3},{4,1}})"), not the "{S : U}"
        // shape this used to emit.
        string uPart = "{" + string.Join(",", universalSet) + "}";
        string sPart = "{" + string.Join(",", subsets.Select(s => "{" + string.Join(",", s) + "}")) + "}";
        string instance = "(" + uPart + "," + sPart + ")";

        reducedExactCover.S = subsets;
        reducedExactCover.X = universalSet;
        reducedExactCover.instance = instance;

        // The gadgets, and the steps recorded with them, are worked out now when the steps are being recorded; otherwise they
        // wait until they are asked for (see gadgets), so reduce() alone does not pay for them.
        if (rec.Enabled) {
            BuildPieces(subsets, instance, rec);
        } else {
            _pendingSubsets = subsets;
            _pendingInstance = instance;
        }

        reductionTo = reducedExactCover;
        return reducedExactCover;
    }

    // Which set of reduce()'s list each set Exact Cover reads is (the first, when sets are equal). Private, so it stays out of
    // the JSON.
    private readonly List<int> _listedAs = new();

    // How the construction steps show a node: the node in the graph; its element, its slots and its color sets, with the
    // sets' memberships, added to Exact Cover; and its gadget. The state is (Node, Gadget, Pieces, Sets): the node, its
    // gadget's index, the pieces its turn adds (the elements no earlier step added, then its sets), and those sets' indices
    // among the sets Exact Cover reads.
    private static RunSteps<(string Node, int Gadget, string[] Pieces, List<int> Sets), BuildDelta> NewNodeSteps(StepRecorder<BuildDelta> rec,
                                                                                                                List<List<string>> parsed) => new(rec,
        // What the Reduction View shows: the node, the pieces and memberships added, and the gadget.
        partial: s => new BuildDelta { SourceFocus = [s.Node], Nodes = s.Pieces, Edges = BuildEdge.Sorted(Memberships(parsed, s.Sets)), Gadgets = [s.Gadget] },
        focus: s => s.Pieces);

    // How the construction steps show an edge: the edge and its two ends in the graph; its element and its sets, with the
    // sets' memberships, added to Exact Cover; and its gadget. The state is (A, B, Gadget, Pieces, Sets): the edge's two ends,
    // its gadget's index, the pieces its turn adds (its element when no earlier step added it, then its sets), and those
    // sets' indices among the sets Exact Cover reads.
    private static RunSteps<(string A, string B, int Gadget, string[] Pieces, List<int> Sets), BuildDelta> NewEdgeSteps(StepRecorder<BuildDelta> rec,
                                                                                                                        List<List<string>> parsed) => new(rec,
        // What the Reduction View shows: the edge, the pieces and memberships added, and the gadget.
        partial: s => new BuildDelta {
            SourceFocus = s.A == s.B ? [s.A] : [GraphSubsetFrames.EdgeId(s.A, s.B), s.A, s.B],
            Nodes = s.Pieces,
            Edges = BuildEdge.Sorted(Memberships(parsed, s.Sets)),
            Gadgets = [s.Gadget],
        },
        focus: s => s.Pieces);

    // The memberships of the given read sets: one per element of each set, each element once.
    private static IEnumerable<BuildEdge> Memberships(List<List<string>> parsed, List<int> sets) =>
        sets.SelectMany(s => parsed[s].Distinct(StringComparer.Ordinal).Select(e => BipartiteFrames.BuildMembership(e, s)));

    // Works out the gadgets, recording the construction steps as it goes when rec records (see IRecordedReduction). reduce()
    // lists K sets per node (color 1 first), then K * (K - 1) sets per edge, one per ordered pair of different colors, so a
    // set's place in that list says what it is. Exact Cover reads equal sets as one (the K color sets of a node with no
    // edges), so the picture's set ids are places among the sets it reads. Its steps:
    //   - an Accept per node: its element, its slots (one per edge at the node and color) and its K color sets;
    //   - an Accept per edge: its element and its K * (K - 1) sets, one per pair of different colors of its ends;
    //   - the Done: an exact cover is a K-coloring.
    // A construction has no verdicts, so Accept means "these pieces go into the target". Each step adds the pieces no
    // earlier step added; which ones those are is worked out here, for the gadgets, so the steps name exactly what the
    // gadgets name.
    // Gadgets: a group per node (the node to the pieces its step adds), then a group per edge (likewise).
    private void BuildPieces(List<List<string>> subsets, string instance, StepRecorder<BuildDelta> rec) {
        var nodes = reductionFrom.nodes;
        var edges = reductionFrom.edges;
        int k = reductionFrom.K;
        var parsed = new EXACTCOVER(instance).S;
        // Each read set's first place, by its elements (sorted, each once), so finding a set is a lookup, not a scan.
        var firstRead = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int p = 0; p < parsed.Count; p++)
            firstRead.TryAdd(SetKey(parsed[p]), p);
        int[] read = subsets.Select(set => firstRead.TryGetValue(SetKey(set), out int p) ? p : -1).ToArray();
        _listedAs.Clear();
        _listedAs.AddRange(parsed.Select((_, p) => Array.IndexOf(read, p)));
        // The elements and the read sets an earlier step already added.
        var built = new HashSet<string>(StringComparer.Ordinal);
        var builtSets = new HashSet<int>();
        // The read sets of the listed sets that no earlier step added, each once.
        List<int> NewSets(IEnumerable<int> listed) => listed.Select(i => read[i]).Distinct().Where(builtSets.Add).ToList();
        // The elements no earlier step added, as the picture's ids.
        List<string> Fresh(IEnumerable<string> elements) =>
            elements.Where(built.Add).Select(BipartiteFrames.ElementId).ToList();

        // One group per node, and one step.
        var nodeSteps = NewNodeSteps(rec, parsed);
        for (int u = 0; u < nodes.Count; u++) {
            string node = nodes[u];
            var slots = edges.Where(e => e.Key == node || e.Value == node)
                .SelectMany(e => Enumerable.Range(1, k).Select(j => SlotElement(node, e.Key, e.Value, j))).ToList();
            var sets = NewSets(Enumerable.Range(u * k, k));
            var pieces = Fresh(new[] { ElementName(node) }.Concat(slots)).Concat(sets.Select(BipartiteFrames.SetId)).ToArray();
            int gadget = _gadgets.Count;
            _gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { node }, pieces.ToList()));
            nodeSteps.Accept((node, gadget, pieces, sets),
                $"Node {node} becomes the element {ElementName(node)}, {Words.Count(slots.Distinct().Count(), "slot")} (one per edge at {node} and color) and {Words.Count(k, "set")}, one per color: the set for color j holds {ElementName(node)} and {node}'s slots for color j. Exactly one of them covers {ElementName(node)}, so {node} gets one color.");
        }
        // Then one group per edge, and one step.
        var edgeSteps = NewEdgeSteps(rec, parsed);
        int first = nodes.Count * k;
        for (int e = 0; e < edges.Count; e++) {
            var (a, b) = (edges[e].Key, edges[e].Value);
            var sets = NewSets(Enumerable.Range(first + e * k * (k - 1), k * (k - 1)));
            var pieces = Fresh([EdgeElement(a, b)]).Concat(sets.Select(BipartiteFrames.SetId)).ToArray();
            int gadget = _gadgets.Count;
            _gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { a == b ? a : GraphSubsetFrames.EdgeId(a, b) }, pieces.ToList()));
            edgeSteps.Accept((a, b, gadget, pieces, sets),
                $"Edge {{{a},{b}}} becomes the element {EdgeElement(a, b)} and {Words.Count(k * (k - 1), "set")}, one per pair of different colors (f, g): each holds {EdgeElement(a, b)}, {a}'s slots for this edge except color f and {b}'s except color g. One fits exactly when {a} and {b} have different colors.");
        }

        // The closing step, on either recorder (both record to rec).
        edgeSteps.Done(new BuildDelta(), true,
            $"An exact cover takes one color set per node and one set per edge, which fits only when the edge's two ends have different colors: the sets have an exact cover exactly when the graph can be colored with {Words.Count(k, "color")}.");
    }

    // A set's elements, sorted and each once: two sets have the same key exactly when they are equal as sets.
    private static string SetKey(IEnumerable<string> set) =>
        string.Join("", set.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    public Groups EmptyAnswer() => new Groups([]);

    // "{}" on either side keeps the default (the verifiers): only a graph with no nodes has the empty exact cover and the
    // empty coloring.

    // How the map-back steps show a set read: the coloring so far, and the id the step is about (the set read, or, for a
    // node no set colors, its element).
    private static RunSteps<string, Groups> NewMapSteps(StepRecorder<Groups> rec, Dictionary<string, int> color) => new(rec,
        // What every picture shows: the coloring so far, and the piece the step is about.
        partial: _ => new Groups(color),
        focus: id => [id]);

    // Backward map: each chosen set is named in the answer by its elements, and found among the sets Exact Cover reads. A
    // node's set for color j gives the node color j (a node with no edges has one set for every color, read as the first).
    // The edge sets only confirm that the colors of an edge's ends differ, so they give nothing. Graph Coloring's verifier
    // decides whether the coloring is a real one.
    // Its steps: an Accept per node color set of the exact cover (its node takes that color), a Reject per node color set of
    // a node that already has a color (it keeps that), then a Reject per node with no color set chosen (it gets no color).
    // The caller adds the Done.
    public (string Answer, Groups Partial) MapBack(string problemToSolution, StepRecorder<Groups> rec) {
        EnsurePieces();
        var target = new EXACTCOVER(reductionTo.instance).S;
        int k = reductionFrom.K;
        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        var steps = NewMapSteps(rec, color);
        foreach (var listed in ReadSets(problemToSolution)) {
            int p = target.FindIndex(set => set.ToHashSet(StringComparer.Ordinal).SetEquals(listed));
            if (p < 0) throw new FormatException($"'{{{string.Join(",", listed)}}}' is not a set of the Exact Cover instance");
            int i = _listedAs[p];
            if (i >= reductionFrom.nodes.Count * k) continue;
            string node = reductionFrom.nodes[i / k], setId = BipartiteFrames.SetId(p);
            int c = i % k;
            if (color.TryGetValue(node, out int had)) {
                steps.Reject(setId, $"Set {setId} gives {node} color {c + 1}, but {node} already has color {had + 1}, so it keeps that.");
                continue;
            }
            color[node] = c;
            steps.Accept(setId, $"Set {setId} {{{string.Join(",", target[p])}}} is in the exact cover. It is {node}'s set for color {c + 1}, so {node} takes color {c + 1}.");
        }
        foreach (string node in reductionFrom.nodes.Where(n => !color.ContainsKey(n)))
            steps.Reject(BipartiteFrames.ElementId(ElementName(node)), $"No set of the exact cover is one of {node}'s color sets, so {node} gets no color.");
        string answer = "{" + string.Join(",", Enumerable.Range(0, k)
            .Select(c => reductionFrom.nodes.Where(n => color.TryGetValue(n, out int x) && x == c).ToList())
            .Where(cls => cls.Count > 0)
            .Select(cls => "{" + string.Join(",", cls) + "}")) + "}";
        return (answer, new Groups(color));
    }

    // An Exact Cover answer, {{a,a_a_b_1},{b,b_a_b_2}}: the chosen sets by their elements. Malformed text throws
    // FormatException, which the endpoint reports as a 400.
    private static List<HashSet<string>> ReadSets(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of sets in braces like {{{{a,a_a_b_1}},{{b}}}}");
        try {
            return new SPADE.UtilCollection(text).ToList().Select(s => s.ToList().Select(e => e.ToString()).ToHashSet(StringComparer.Ordinal)).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not a set of sets like {{{{a,a_a_b_1}},{{b}}}}", ex);
        }
    }

    // A K-coloring picks, for each node u of color c, the set {u, u's slots for color c}, and for each edge {u,v}, the set
    // for the pair (color of u, color of v): it covers the edge and the slots of u and v for every other color. Every slot is
    // then covered once (by its node's set for its own color, by the edge's set for the rest), so the picked sets are an
    // exact cover. The sets are read from the target by position: reduce() lists K sets per node, color 1 first, then
    // K * (K - 1) sets per edge, one per ordered pair of different colors.
    public string mapSolutions(string reductionFromSolution) {
        Dictionary<string, int> color = ColoringAnswer.Read(this, reductionFrom, reductionFromSolution);
        int k = reductionFrom.K;
        var picked = new List<int>();
        for (int u = 0; u < reductionFrom.nodes.Count; u++)
            picked.Add(u * k + color[reductionFrom.nodes[u]]);
        int edgeSets = reductionFrom.nodes.Count * k;
        for (int e = 0; e < reductionFrom.edges.Count; e++) {
            int f1 = color[reductionFrom.edges[e].Key], f2 = color[reductionFrom.edges[e].Value];
            picked.Add(edgeSets + e * k * (k - 1) + f1 * (k - 1) + (f2 > f1 ? f2 - 1 : f2));
        }
        return "{" + string.Join(",", picked.Select(i => "{" + string.Join(",", reductionTo.S[i]) + "}")) + "}";
    }

}
// return an instance of what you are reducing to