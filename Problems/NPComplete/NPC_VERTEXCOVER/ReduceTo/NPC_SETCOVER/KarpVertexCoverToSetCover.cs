using API.Interfaces.JSON_Objects;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_SETCOVER;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_SETCOVER;

class KarpVertexCoverToSetCover : IReversibleReduction<VERTEXCOVER, SETCOVER, NodeSet>, IRecordedReduction<VERTEXCOVER, SETCOVER> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's Set Cover Reduction";
    public string reductionDefinition { get; } = "";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Caleb Eardley" };
    // reduce() transposes the input's own node/edge incidence structure (one subset
    // entry per edge touching a node) — total elements are bounded by O(m) (sum of
    // node degrees), already implied by the input's own edge list.
    public ReductionCost cost { get; } = ReductionCost.Linear;
    // Declared, not derived. Transposes the input's own node/edge incidence structure
    // into one subset per node -- the same incidence relation re-expressed, no new
    // gadgetry built.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. Nested loop over nodes x edges to build each node's
    // incident-edge subset.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n * m), n = |VERTEXCOVER.nodes|, m = |edges|";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private VERTEXCOVER _reductionFrom;
    private SETCOVER _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public VERTEXCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public SETCOVER reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public KarpVertexCoverToSetCover(VERTEXCOVER from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpVertexCoverToSetCover(VERTEXCOVER from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public KarpVertexCoverToSetCover(string instance) : this(new VERTEXCOVER(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpVertexCoverToSetCover(string instance, StepRecorder<BuildDelta> rec) : this(new VERTEXCOVER(instance), rec) { }
    public KarpVertexCoverToSetCover() : this(new VERTEXCOVER()) { }

    // An edge {u,v} is the element "u_v". A name that holds "_" (or the escape character) is written with a \ before
    // every \ and "_", so two different edges never share an element; other names are written as they are (#824).
    private static string EdgeElement(string u, string v) => ElementName(u) + "_" + ElementName(v);

    private static string ElementName(string name) =>
        name.IndexOfAny(['\\', '_']) < 0 ? name : name.Replace(@"\", @"\\").Replace("_", @"\_");

    /// <summary>
    /// The gadgets (#694): per node with edges, an element (the node to its set, in the Bipartite Graph's ids); per edge, an
    /// element (the edge to the element it becomes); and the bound, K unchanged.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    // The node each set of the target was made from (the first, when nodes touch the same edges), by the set's index among
    // the sets Set Cover reads. Private, so it stays out of the JSON.
    private readonly List<string> _nodeOfSet = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpVertexCoverToSetCover(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    /***************************************************
     * reduce() called after GareyAndJohnsonReduction reduction, and returns a THREE_DM object, that
     * is a reduction from the VERTEXCOVER object passed into GareyAndJohnsonReduction.
     */
    public SETCOVER reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per node (its set, and the edges it is the first to touch as new
    // elements), then K.
    private SETCOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        _nodeOfSet.Clear();
        VERTEXCOVER VERTEXCOVERInstance = _reductionFrom;
        SETCOVER reducedSetCover = new SETCOVER();

        List<List<string>> subsets = new List<List<string>>();
        List<string> universal = new List<string>();
        // The edges each node is the first to touch, with their elements, in the order they join the universe.
        var firstTouched = new List<List<(string U, string V, string Element)>>();

        for (int i = 0; i < reductionFrom.nodes.Count; i++) {
            subsets.Add(new List<string>());
            firstTouched.Add(new());
            foreach (var j in reductionFrom.edges) {
                if (j.Key == reductionFrom.nodes[i] || j.Value == reductionFrom.nodes[i]) {
                    string element = EdgeElement(j.Key, j.Value);
                    subsets[i].Add(element);
                    if (!universal.Contains(element)) {
                        universal.Add(element);
                        firstTouched[i].Add((j.Key, j.Value, element));
                    }
                }
            }
        }

        string instance = "({";

        foreach (var i in universal) {
            instance += i + ",";
        }
        instance = instance.TrimEnd(',') + "},{{";

        foreach (var i in subsets) {
            foreach (var j in i) {
                instance += j + ",";
            }
            instance = instance.TrimEnd(',') + "},{";
            if (i.Count == 0) instance = instance.TrimEnd('{').TrimEnd(',').TrimEnd('}');
        }
        instance = instance.TrimEnd('{').TrimEnd(',') + "}," + reductionFrom.K + ")";
        // With no edges every set is empty and dropped, and the trimming above eats into the universe too: nothing to
        // cover and no sets (#826).
        if (universal.Count == 0) instance = "({},{}," + reductionFrom.K + ")";

        reducedSetCover.universal = universal;
        reducedSetCover.subsets = subsets;
        // The bound is A's K, as the instance text says; the object kept the default instance's K = 3 (#823).
        reducedSetCover.K = reductionFrom.K;
        reducedSetCover.instance = instance;

        // Gadgets, and the set of the target each node's set is. The instance text leaves out the empty sets, and Set Cover
        // reads equal sets as one set (two nodes that touch the same edges), so a set's index is its place among the sets
        // Set Cover reads.
        var parsedSets = new SETCOVER(instance).subsets;
        var setOf = new Dictionary<int, int>();
        var nodeGadget = new Dictionary<int, int>();
        var edgeGadgets = new List<int>[reductionFrom.nodes.Count];
        _nodeOfSet.AddRange(Enumerable.Repeat("", parsedSets.Count));
        for (int i = 0; i < reductionFrom.nodes.Count; i++) {
            string node = reductionFrom.nodes[i];
            edgeGadgets[i] = new List<int>();
            if (subsets[i].Count > 0) {
                int set = parsedSets.FindIndex(p => p.ToHashSet(StringComparer.Ordinal).SetEquals(subsets[i]));
                setOf[i] = set;
                if (_nodeOfSet[set] == "") _nodeOfSet[set] = node;
                nodeGadget[i] = gadgets.Count;
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { node }, new List<string> { BipartiteFrames.SetId(set) }));
            }
            foreach (var (u, v, element) in firstTouched[i]) {
                edgeGadgets[i].Add(gadgets.Count);
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { u == v ? u : GraphSubsetFrames.EdgeId(u, v) },
                    new List<string> { BipartiteFrames.ElementId(element) }));
            }
        }
        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {reductionFrom.K}"));

        if (rec.Enabled) {
            var builtSets = new HashSet<int>();
            for (int i = 0; i < reductionFrom.nodes.Count; i++) {
                string node = reductionFrom.nodes[i];
                if (!setOf.TryGetValue(i, out int set)) {
                    rec.Reject(() => new BuildDelta { SourceFocus = [node] },
                        () => $"Node {node} touches no edge, so its set would be empty; Set Cover's instance leaves it out.");
                    continue;
                }
                var mine = subsets[i];
                var fresh = firstTouched[i];
                string setId = BipartiteFrames.SetId(set);
                bool again = !builtSets.Add(set);
                string[] nodes = again ? [] : fresh.Select(f => BipartiteFrames.ElementId(f.Element)).Append(setId).ToArray();
                BuildEdge[] memberships = again ? [] : mine.Distinct(StringComparer.Ordinal).Select(e => BipartiteFrames.BuildMembership(e, set)).ToArray();
                int[] used = new[] { nodeGadget[i] }.Concat(edgeGadgets[i]).ToArray();
                string[] focus = mine.Select(BipartiteFrames.ElementId).Append(setId).Distinct(StringComparer.Ordinal).ToArray();
                string[] sourceFocus = new[] { node }.Concat(fresh.Select(f => f.U == f.V ? f.U : GraphSubsetFrames.EdgeId(f.U, f.V))).Distinct(StringComparer.Ordinal).ToArray();
                string owner = _nodeOfSet[set];
                rec.Accept(
                    () => new BuildDelta { SourceFocus = sourceFocus, Nodes = nodes, Edges = BuildEdge.Sorted(memberships), Gadgets = used },
                    () => again
                        ? $"Node {node} touches the same edges as {owner}, {{{string.Join(",", mine)}}}, so its set is {setId} too: Set Cover reads equal sets as one."
                        : $"Node {node} becomes set {setId}, the edges it touches: {{{string.Join(",", mine)}}}."
                            + (fresh.Count == 0 ? "" : $" {(fresh.Count == 1 ? "Edge" : "Edges")} {BuildCaptions.Names(fresh.Select(f => $"{{{f.U},{f.V}}}").ToList())} {(fresh.Count == 1 ? "is" : "are")} new, so {(fresh.Count == 1 ? "it joins" : "they join")} the elements to cover."),
                    focus);
            }
            int k = reductionFrom.K;
            rec.Done(new BuildDelta { Bound = k, Gadgets = [bound] }, true,
                $"K stays {k}: at most {BuildCaptions.Count(k, "set")} can cover every element exactly when at most {BuildCaptions.Count(k, "node")} can touch every edge.");
        }

        //return new THREE_DM();
        return reducedSetCover;
    }

    // A vertex cover is a set cover of the target: node i's set holds the edges that touch it, so the chosen nodes' sets
    // cover every edge. Each node of the cover gives its set; a node with no edges has an empty set, which the instance
    // leaves out, so it gives nothing (#823).
    public string mapSolutions(string problemFromSolution) {
        List<string> cover = GraphParser.parseNodeListWithStringFunctions(problemFromSolution);
        var sets = new List<string>();
        foreach (string node in cover) {
            int i = reductionFrom.nodes.IndexOf(node);
            if (i < 0)
                throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{node}' is not a node of the graph");
            if (reductionTo.subsets[i].Count > 0) sets.Add("{" + string.Join(",", reductionTo.subsets[i]) + "}");
        }
        return "{" + string.Join(",", sets) + "}";
    }

    public NodeSet EmptyAnswer() => new NodeSet([]);

    // "{}" on either side keeps the default (the verifiers): with no edge there is nothing to cover, and the empty cover
    // does it on both sides.

    /// <summary>
    /// Backward map (#694): each set of the cover is the set of some node, the edges that touch it, so that node covers
    /// those edges. The sets are named in the answer by their elements (two nodes that touch the same edges have one set,
    /// and either covers them: the first is taken).
    /// </summary>
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var target = new SETCOVER(reductionTo.instance).subsets;
        var cover = new List<string>();
        foreach (var listed in ReadSets(problemToSolution)) {
            int i = target.FindIndex(set => set.ToHashSet(StringComparer.Ordinal).SetEquals(listed));
            if (i < 0) throw new FormatException($"'{{{string.Join(",", listed)}}}' is not a set of the Set Cover instance");
            string node = _nodeOfSet[i];
            bool known = cover.Contains(node);
            if (!known) cover.Add(node);
            string setId = BipartiteFrames.SetId(i);
            rec.Accept(() => new NodeSet(cover),
                () => $"Set {setId} {{{string.Join(",", target[i])}}} is in the cover. It is node {node}'s set, the edges that touch {node}, so {node} is in the vertex cover.",
                setId);
        }
        return (ReductionBack.FormatNodeSet(cover), new NodeSet(cover));
    }

    // A Set Cover answer, {{a_b,a_c},{c_d}}: the chosen sets by their elements. Malformed text throws FormatException, which
    // the endpoint reports as a 400.
    private static List<HashSet<string>> ReadSets(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of sets in braces like {{{{a_b,a_c}},{{c_d}}}}");
        try {
            return new SPADE.UtilCollection(text).ToList().Select(s => s.ToList().Select(e => e.ToString()).ToHashSet(StringComparer.Ordinal)).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not a set of sets like {{{{a_b,a_c}},{{c_d}}}}", ex);
        }
    }
}
