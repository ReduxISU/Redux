using API.Interfaces.JSON_Objects;
using API.Interfaces.Graphs;
using API.Interfaces.Steps;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using API.Problems.NPComplete.NPC_SETCOVER;

namespace API.Problems.NPComplete.NPC_VERTEXCOVER.ReduceTo.NPC_SETCOVER;

// Karp's Vertex Cover to Set Cover: every edge becomes an element to cover, every node becomes the set of the edges it
// touches, and K stays the same. It records the construction steps (one per node, then K; see Build) and the backward map's
// steps (one per set of the cover; see MapBack).
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
    // every \ and "_", so two different edges never share an element; other names are written as they are.
    private static string EdgeElement(string u, string v) => ElementName(u) + "_" + ElementName(v);

    private static string ElementName(string name) =>
        name.IndexOfAny(['\\', '_']) < 0 ? name : name.Replace(@"\", @"\\").Replace("_", @"\_");

    /// <summary>
    /// The gadgets: per node with edges, an element (the node to its set, in the set problems' piece ids); per edge, an
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

    public SETCOVER reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show a node whose set is in the target: the node and the edges it is the first to touch,
    // in the graph; the set and those new elements, with the set's memberships, added to Set Cover (nothing added when an
    // earlier node made the same set); and the node's gadgets. The state is (Node, Set, Again, FirstGadget): the node's
    // index, its set's index among the sets Set Cover reads, whether an earlier node already made that set, and the index of
    // the node's first gadget (its set's element, then one per new edge).
    private RunSteps<(int Node, int Set, bool Again, int FirstGadget), BuildDelta> NewNodeSteps(StepRecorder<BuildDelta> rec,
            List<List<string>> subsets, List<List<(string U, string V, string Element)>> firstTouched) => new(rec,
        // What the Reduction View shows: the node and its new edges, the set and elements added, and the gadgets that make them.
        partial: s => new BuildDelta {
            SourceFocus = firstTouched[s.Node].Select(f => f.U == f.V ? f.U : GraphSubsetFrames.EdgeId(f.U, f.V))
                .Prepend(reductionFrom.nodes[s.Node]).Distinct(StringComparer.Ordinal).ToArray(),
            Nodes = s.Again ? [] : firstTouched[s.Node].Select(f => SetPieces.ElementId(f.Element)).Append(SetPieces.SetId(s.Set)).ToArray(),
            Edges = s.Again ? [] : BuildEdge.Sorted(subsets[s.Node].Distinct(StringComparer.Ordinal).Select(e => SetPieces.BuildMembership(e, s.Set))),
            Gadgets = Enumerable.Range(s.FirstGadget, 1 + firstTouched[s.Node].Count).ToArray(),
        },
        // The set and every element in it.
        focus: s => subsets[s.Node].Select(SetPieces.ElementId).Append(SetPieces.SetId(s.Set)).Distinct(StringComparer.Ordinal).ToArray());

    // How the construction steps show a node that touches no edge: the node alone, since it adds nothing. The state is the node.
    private static RunSteps<string, BuildDelta> NewLoneNodeSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the node in the graph.
        partial: node => new BuildDelta { SourceFocus = [node] });

    // Builds the Set Cover instance, recording the construction steps as it goes (see IRecordedReduction). Its steps, one per
    // node in the graph's order:
    //   - an Accept when the node becomes a set, the edges it touches: the edges no earlier node touched join the elements
    //     to cover. When an earlier node touches exactly the same edges, the Accept names that set again (Set Cover reads
    //     equal sets as one);
    //   - a Reject when the node touches no edge: its set would be empty, and the instance leaves it out;
    //   - the Done: K stays the same.
    // A construction has no verdicts, so Accept means "these pieces go into the target" and Reject "this node adds nothing
    // to it". The steps come from the very decisions below, so what is recorded is what is built.
    // Gadgets: per node with edges, an element (the node to its set), then one per edge it is the first to touch (the edge
    // to its element); then the bound (K).
    private SETCOVER Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        _nodeOfSet.Clear();
        SETCOVER reducedSetCover = new SETCOVER();

        // Node i's set is subsets[i], the element of every edge that touches it. The universe is every edge's element, in
        // the order the nodes first touch them.
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
        // cover and no sets.
        if (universal.Count == 0) instance = "({},{}," + reductionFrom.K + ")";

        reducedSetCover.universal = universal;
        reducedSetCover.subsets = subsets;
        // The bound is A's K, as the instance text says.
        reducedSetCover.K = reductionFrom.K;
        reducedSetCover.instance = instance;

        // The gadgets, and the set of the target each node's set is. The instance text leaves out the empty sets, and Set
        // Cover reads equal sets as one set (two nodes that touch the same edges), so a set's index is its place among the
        // sets Set Cover reads. Each node is then one step.
        var parsedSets = new SETCOVER(instance).subsets;
        var nodeSteps = NewNodeSteps(rec, subsets, firstTouched);
        var loneNodeSteps = NewLoneNodeSteps(rec);
        _nodeOfSet.AddRange(Enumerable.Repeat("", parsedSets.Count));
        for (int i = 0; i < reductionFrom.nodes.Count; i++) {
            string node = reductionFrom.nodes[i];
            var mine = subsets[i];
            if (mine.Count == 0) {
                loneNodeSteps.Reject(node, $"Node {node} touches no edge, so its set would be empty; Set Cover's instance leaves it out.");
                continue;
            }
            int set = parsedSets.FindIndex(p => p.ToHashSet(StringComparer.Ordinal).SetEquals(mine));
            // The first node to make this set owns it; a later node that touches the same edges names the same set.
            string owner = _nodeOfSet[set];
            bool again = owner != "";
            if (!again) _nodeOfSet[set] = node;
            int firstGadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { node }, new List<string> { SetPieces.SetId(set) }));
            var fresh = firstTouched[i];
            foreach (var (u, v, element) in fresh)
                gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { u == v ? u : GraphSubsetFrames.EdgeId(u, v) },
                    new List<string> { SetPieces.ElementId(element) }));

            if (again)
                nodeSteps.Accept((i, set, again, firstGadget),
                    $"Node {node} touches the same edges as {owner}, {{{string.Join(",", mine)}}}, so its set is {SetPieces.SetId(set)} too: Set Cover reads equal sets as one.");
            else if (fresh.Count == 0)
                nodeSteps.Accept((i, set, again, firstGadget),
                    $"Node {node} becomes set {SetPieces.SetId(set)}, the edges it touches: {{{string.Join(",", mine)}}}.");
            else if (fresh.Count == 1)
                nodeSteps.Accept((i, set, again, firstGadget),
                    $"Node {node} becomes set {SetPieces.SetId(set)}, the edges it touches: {{{string.Join(",", mine)}}}. Edge {{{fresh[0].U},{fresh[0].V}}} is new, so it joins the elements to cover.");
            else
                nodeSteps.Accept((i, set, again, firstGadget),
                    $"Node {node} becomes set {SetPieces.SetId(set)}, the edges it touches: {{{string.Join(",", mine)}}}. Edges {BuildCaptions.Names(fresh.Select(f => $"{{{f.U},{f.V}}}").ToList())} are new, so they join the elements to cover.");
        }
        // Then the bound: K stays. The Set Cover picture has no piece of its own for it.
        int bound = gadgets.Count;
        int k = reductionFrom.K;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string>(), new List<string>(), note: $"K stays {k}"));

        // The closing step, on either recorder (both record to rec).
        nodeSteps.Done(new BuildDelta { Bound = k, Gadgets = [bound] }, true,
            $"K stays {k}, since at most {Words.Count(k, "set")} can cover every element exactly when at most {Words.Count(k, "node")} can touch every edge.");

        return reducedSetCover;
    }

    // A vertex cover is a set cover of the target: node i's set holds the edges that touch it, so the chosen nodes' sets
    // cover every edge. Each node of the cover gives its set; a node with no edges has an empty set, which the instance
    // leaves out, so it gives nothing.
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

    // How the map-back steps show a set read: the vertex cover so far, and the set.
    private static RunSteps<string, NodeSet> NewMapSteps(StepRecorder<NodeSet> rec, List<string> cover) => new(rec,
        // What every picture shows: the cover so far, and the set the step is about.
        partial: _ => new NodeSet(cover),
        focus: setId => [setId]);

    // Backward map: each set of the cover is the set of some node, the edges that touch it, so that node covers those edges.
    // The sets are named in the answer by their elements (two nodes that touch the same edges have one set, and either
    // covers them: the first is taken).
    // Its steps: an Accept per set of the cover, in the answer's order: its node is in the vertex cover. The caller adds the
    // Done.
    public (string Answer, NodeSet Partial) MapBack(string problemToSolution, StepRecorder<NodeSet> rec) {
        var target = new SETCOVER(reductionTo.instance).subsets;
        var cover = new List<string>();
        var steps = NewMapSteps(rec, cover);
        foreach (var listed in ReadSets(problemToSolution)) {
            int i = target.FindIndex(set => set.ToHashSet(StringComparer.Ordinal).SetEquals(listed));
            if (i < 0) throw new FormatException($"'{{{string.Join(",", listed)}}}' is not a set of the Set Cover instance");
            string node = _nodeOfSet[i];
            if (!cover.Contains(node)) cover.Add(node);
            steps.Accept(SetPieces.SetId(i),
                $"Set {SetPieces.SetId(i)} {{{string.Join(",", target[i])}}} is in the cover. It is node {node}'s set, the edges that touch {node}, so {node} is in the vertex cover.");
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
