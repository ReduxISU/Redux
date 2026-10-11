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
    // never share an element; other names are written as they are (#824).
    private static string EdgeElement(string u, string v) => ElementName(u) + "_" + ElementName(v);

    private static string SlotElement(string node, string u, string v, int color) => ElementName(node) + "_" + EdgeElement(u, v) + "_" + color;

    private static string ElementName(string name) =>
        name.IndexOfAny(['\\', '_']) < 0 ? name : name.Replace(@"\", @"\\").Replace("_", @"\_");

    /// <summary>
    /// The gadgets (#694): per node a group (the node to its element, its color slots and its K color sets) and per edge a
    /// group (the edge to its element and its K * (K - 1) sets, one per pair of different colors of its ends).
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpGraphColorToExactCover(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public EXACTCOVER reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per node (its element, its slots and its color sets), one per edge
    // (its element and its sets), then Done.
    private EXACTCOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
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

        RecordPieces(subsets, rec);

        reductionTo = reducedExactCover;
        return reducedExactCover;
    }

    // The gadgets, and the construction steps when recording: one step per node, then one per edge. reduce() lists K sets per
    // node (color 1 first), then K * (K - 1) sets per edge, one per ordered pair of different colors, so a set's index says
    // what it is.
    private void RecordPieces(List<List<string>> subsets, StepRecorder<BuildDelta> rec) {
        var nodes = reductionFrom.nodes;
        var edges = reductionFrom.edges;
        int k = reductionFrom.K;
        var built = new HashSet<string>(StringComparer.Ordinal);
        BuildEdge[] Memberships(IEnumerable<int> sets) =>
            sets.SelectMany(s => subsets[s].Distinct(StringComparer.Ordinal).Select(e => BipartiteFrames.BuildMembership(e, s))).ToArray();
        List<string> Fresh(IEnumerable<string> elements) =>
            elements.Where(built.Add).Select(BipartiteFrames.ElementId).ToList();

        var nodeSteps = new List<(string Node, int Gadget, string[] Pieces, BuildEdge[] Edges, int Slots)>();
        for (int u = 0; u < nodes.Count; u++) {
            string node = nodes[u];
            var slots = edges.Where(e => e.Key == node || e.Value == node)
                .SelectMany(e => Enumerable.Range(1, k).Select(j => SlotElement(node, e.Key, e.Value, j))).ToList();
            var sets = Enumerable.Range(u * k, k).ToList();
            var pieces = Fresh(new[] { ElementName(node) }.Concat(slots)).Concat(sets.Select(BipartiteFrames.SetId)).ToArray();
            nodeSteps.Add((node, gadgets.Count, pieces, Memberships(sets), slots.Distinct().Count()));
            gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { node }, pieces.ToList()));
        }
        var edgeSteps = new List<(string A, string B, int Gadget, string[] Pieces, BuildEdge[] Edges)>();
        int first = nodes.Count * k;
        for (int e = 0; e < edges.Count; e++) {
            var (a, b) = (edges[e].Key, edges[e].Value);
            var sets = Enumerable.Range(first + e * k * (k - 1), k * (k - 1)).ToList();
            var pieces = Fresh([EdgeElement(a, b)]).Concat(sets.Select(BipartiteFrames.SetId)).ToArray();
            edgeSteps.Add((a, b, gadgets.Count, pieces, Memberships(sets)));
            gadgets.Add(new Gadget(GadgetKind.Group, new List<string> { a == b ? a : GraphSubsetFrames.EdgeId(a, b) }, pieces.ToList()));
        }
        if (!rec.Enabled) return;

        foreach (var (node, index, pieces, memberships, slots) in nodeSteps) {
            string[] focus = pieces.ToArray();
            rec.Accept(
                () => new BuildDelta { SourceFocus = [node], Nodes = pieces, Edges = BuildEdge.Sorted(memberships), Gadgets = [index] },
                () => $"Node {node} becomes the element {ElementName(node)}, {BuildCaptions.Count(slots, "slot")} (one per edge at {node} and color) and {BuildCaptions.Count(k, "set")}, "
                    + $"one per color: the set for color j holds {ElementName(node)} and {node}'s slots for color j. Exactly one of them covers {ElementName(node)}, so {node} gets one color.",
                focus);
        }
        foreach (var (a, b, index, pieces, memberships) in edgeSteps) {
            string[] sourceFocus = a == b ? [a] : [GraphSubsetFrames.EdgeId(a, b), a, b];
            rec.Accept(
                () => new BuildDelta { SourceFocus = sourceFocus, Nodes = pieces, Edges = BuildEdge.Sorted(memberships), Gadgets = [index] },
                () => $"Edge {{{a},{b}}} becomes the element {EdgeElement(a, b)} and {BuildCaptions.Count(k * (k - 1), "set")}, one per pair of different colors (f, g): "
                    + $"each holds {EdgeElement(a, b)}, {a}'s slots for this edge except color f and {b}'s except color g. One fits exactly when {a} and {b} have different colors.",
                pieces);
        }
        rec.Done(new BuildDelta(), true,
            $"An exact cover takes one color set per node and one set per edge, which fits only when the edge's two ends have different colors: "
            + $"the sets have an exact cover exactly when the graph can be colored with {BuildCaptions.Count(k, "color")}.");
    }

    public Groups EmptyAnswer() => new Groups([]);

    // "{}" on either side keeps the default (the verifiers): only a graph with no nodes has the empty exact cover and the
    // empty coloring.

    /// <summary>
    /// Backward map (#694): each chosen set is named in the answer by its elements; it is the first set of the target with
    /// those elements not taken yet. A node's set for color j gives the node color j. The edge sets only confirm that the
    /// colors of an edge's ends differ, so they give nothing. A node with no color set chosen gets no color (a Reject), and
    /// Graph Coloring's verifier decides.
    /// </summary>
    public (string Answer, Groups Partial) MapBack(string problemToSolution, StepRecorder<Groups> rec) {
        var target = new EXACTCOVER(reductionTo.instance).S;
        var taken = new bool[target.Count];
        int k = reductionFrom.K;
        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var listed in ReadSets(problemToSolution)) {
            int i = Enumerable.Range(0, target.Count).FirstOrDefault(j => !taken[j] && target[j].ToHashSet().SetEquals(listed), -1);
            if (i < 0) throw new FormatException($"'{{{string.Join(",", listed)}}}' is not a set of the Exact Cover instance");
            taken[i] = true;
            if (i >= reductionFrom.nodes.Count * k) continue;
            string node = reductionFrom.nodes[i / k], setId = BipartiteFrames.SetId(i);
            int c = i % k;
            if (color.TryGetValue(node, out int had)) {
                rec.Reject(() => new Groups(color), () => $"Set {setId} gives {node} color {c + 1}, but {node} already has color {had + 1}, so it keeps that.", setId);
                continue;
            }
            color[node] = c;
            rec.Accept(() => new Groups(color), () => $"Set {setId} {{{string.Join(",", target[i])}}} is in the exact cover. It is {node}'s set for color {c + 1}, so {node} takes color {c + 1}.", setId);
        }
        foreach (string node in reductionFrom.nodes.Where(n => !color.ContainsKey(n)))
            rec.Reject(() => new Groups(color), () => $"No set of the exact cover is one of {node}'s color sets, so {node} gets no color.", BipartiteFrames.ElementId(ElementName(node)));
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
    // K * (K - 1) sets per edge, one per ordered pair of different colors (#823).
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