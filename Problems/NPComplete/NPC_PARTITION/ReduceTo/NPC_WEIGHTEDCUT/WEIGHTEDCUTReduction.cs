using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;
using API.Problems.NPComplete.NPC_WEIGHTEDCUT;

namespace API.Problems.NPComplete.NPC_PARTITION.ReduceTo.NPC_WEIGHTEDCUT;

// Partition to Weighted Cut: every number becomes a node, every two nodes are joined by an edge weighing the product of
// their numbers, and K is the weight of an even split's cut (sum^2 / 4), or one more than the best cut when the total is
// odd. It records the construction steps (one per number, then K; see Build) and the backward map's steps (one per node;
// see MapBack).
class WEIGHTEDCUTReduction : IReversibleReduction<PARTITION, WEIGHTEDCUT, Placement>, IRecordedReduction<PARTITION, WEIGHTEDCUT> {

    // --- Fields ---
    public string reductionName { get; } = "Weighted Cut Reduction";
    public string reductionDefinition { get; } = "Karp's reduction from Partition to Weighted Cut: each number of S becomes a node, every two nodes are joined by an edge weighing the product of their two numbers, and K is a quarter of the square of the numbers' total. A split of S into two sides that add up to a and b cuts edges weighing a * b in all, which reaches K only when a = b, so S splits into two halves of equal sum exactly when the graph has a cut weighing at least K.";
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
    public WEIGHTEDCUTReduction(PARTITION from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private WEIGHTEDCUTReduction(PARTITION from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public WEIGHTEDCUTReduction(string instance) : this(new PARTITION(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public WEIGHTEDCUTReduction(string instance, StepRecorder<BuildDelta> rec) : this(new PARTITION(instance), rec) { }
    public WEIGHTEDCUTReduction() : this(new PARTITION()) { }

    /// <summary>
    /// The gadgets: per number of S, an element (Partition's item I{i} to node i); per pair of numbers, an edge rule
    /// (the two items to the edge between their nodes, weighing their product); and the bound, Partition's two sides to K,
    /// which Weighted Cut's picture draws as no piece of its own.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new WEIGHTEDCUTReduction(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public WEIGHTEDCUT reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show a number: the number in Partition; its node and its edges to the nodes before it,
    // added to Weighted Cut; and the gadgets that make them. The state is (Number, FirstGadget): the number's index, and the
    // index of its first gadget (its element, then one edge rule per earlier node).
    private static RunSteps<(int Number, int FirstGadget), BuildDelta> NewNumberSteps(StepRecorder<BuildDelta> rec, List<string> nodes) => new(rec,
        // What the Reduction View shows: the number, the node and edges added, and the gadgets that make them.
        partial: s => new BuildDelta {
            SourceFocus = [PackingFrames.ItemId(s.Number)],
            Nodes = [nodes[s.Number]],
            Edges = BuildEdge.Sorted(Enumerable.Range(0, s.Number).Select(j => new BuildEdge(nodes[j], nodes[s.Number]))),
            Gadgets = Enumerable.Range(s.FirstGadget, s.Number + 1).ToArray(),
        },
        focus: s => [nodes[s.Number]]);

    // Builds the Weighted Cut instance, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept per number of S, in order: it becomes a node, joined to every earlier node by an edge weighing the
    //     product of their numbers;
    //   - the Done: K, the weight an even split's cut reaches (or one more than the best cut, when the total is odd).
    // A construction has no verdicts, so Accept means "these pieces go into the target". The steps come from the very
    // decisions below, so what is recorded is what is built.
    // Gadgets: per number, an element (its item to its node) and an edge rule per earlier node (the two items to the edge
    // between their nodes); then the bound (K).
    private WEIGHTEDCUT Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        PARTITION PARTITIONInstance = _reductionFrom;
        WEIGHTEDCUT reducedWEIGHTEDCUT = new WEIGHTEDCUT();

        List<string> nodes = new List<string>();
        List<(string source, string destination, int weight)> edges = new List<(string source, string destination, int weight)>();
        List<string> written = new List<string>();
        var numbers = reductionFrom.S.Select(int.Parse).ToList();

        for (int i = 1; i <= numbers.Count; i++)
            nodes.Add(i.ToString());

        // Node i is the i-th number; every two nodes are joined by an edge weighing the product of their numbers. Weights and K
        // are worked out in long, so a product or square that does not fit Weighted Cut's integers is a 400, never a wrap.
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
        // round sum^2 / 4 down to: K is then (sum^2 + 3) / 4, one more, which no cut reaches.
        long total = numbers.Sum(n => (long)n);
        long square;
        try {
            square = checked(total * total);
        } catch (OverflowException) {
            throw TooBig($"the sum of the numbers, {total}, squared");
        }
        int k = FitInt(total % 2 == 0 ? square / 4 : (square + 3) / 4, $"K, from the sum of the numbers {total}");

        // One number gives one node and no edges: "(({1},{}),K)".
        string instance = $"(({{{string.Join(",", nodes)}}},{{{string.Join(",", written)}}}),{k})";

        reducedWEIGHTEDCUT.K = k;
        reducedWEIGHTEDCUT.nodes = nodes;
        reducedWEIGHTEDCUT.edges = edges;
        reducedWEIGHTEDCUT.instance = instance;

        // One gadget per number (its item to its node), then one edge rule per edge to an earlier node; each number is then
        // one step.
        var numberSteps = NewNumberSteps(rec, nodes);
        for (int i = 0; i < numbers.Count; i++) {
            string item = PackingFrames.ItemId(i), node = nodes[i];
            int element = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { item }, new List<string> { node }));
            for (int j = 0; j < i; j++)
                gadgets.Add(new Gadget(GadgetKind.EdgeRule, new List<string> { PackingFrames.ItemId(j), item }, new List<string> { new BuildEdge(nodes[j], node).Id },
                    note: $"{numbers[j]} * {numbers[i]} = {(long)numbers[j] * numbers[i]}"));

            int number = numbers[i];
            if (i == 0)
                numberSteps.Accept((i, element), $"Number {number} becomes node {node}.");
            else if (i == 1)
                numberSteps.Accept((i, element),
                    $"Number {number} becomes node {node}, joined to node {nodes[0]} by an edge weighing the product of their numbers: {numbers[0]} * {number} = {(long)number * numbers[0]}.");
            else
                numberSteps.Accept((i, element),
                    $"Number {number} becomes node {node}, joined to nodes {BuildCaptions.Names(nodes.Take(i).ToList())} by edges weighing the product of their numbers: {BuildCaptions.Names(Enumerable.Range(0, i).Select(j => $"{numbers[j]} * {number} = {(long)number * numbers[j]}").ToList())}.");
        }
        // Then the bound: Partition's two sides to K. The Weighted Cut picture has no piece of its own for it.
        int bound = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string> { PackingFrames.ContainerId(0), PackingFrames.ContainerId(1) }, new List<string>(), note: $"K = {k}"));

        // The closing step: the best cut is an even split when the total is even, and K is out of reach when it is odd.
        long half = total / 2;
        if (total % 2 == 0)
            numberSteps.Done(new BuildDelta { SourceFocus = [PackingFrames.ContainerId(0), PackingFrames.ContainerId(1)], Gadgets = [bound], Bound = k }, true,
                $"K = {total}^2 / 4 = {k}. A cut between sides whose numbers add up to a and b weighs a * b, which reaches {half} * {half} = {k} only when a = b = {half}: an even split.");
        else
            numberSteps.Done(new BuildDelta { SourceFocus = [PackingFrames.ContainerId(0), PackingFrames.ContainerId(1)], Gadgets = [bound], Bound = k }, true,
                $"The numbers add up to {total}, an odd total, so no split is even and the best cut weighs {half} * {total - half} = {half * (total - half)}. K = ({total}^2 + 3) / 4 = {k} is one more, so no cut reaches it.");

        reductionTo = reducedWEIGHTEDCUT;
        return reducedWEIGHTEDCUT;
    }

    private int FitInt(long value, string what) =>
        value is >= int.MinValue and <= int.MaxValue ? (int)value : throw TooBig($"{what} = {value}");

    private ReductionInputException TooBig(string what) =>
        new(this, _reductionFrom.instance, _reductionFrom.instanceFormat,
            $"{what} does not fit Weighted Cut's integers (at most {int.MaxValue}); use smaller numbers");

    /// <summary>
    /// Maps a Partition answer to a Weighted Cut answer: node i is the i-th number of S, so the cut between the two
    /// sides is every edge {i,j} whose numbers are on different sides, with its weight. The two sides must hold every number
    /// of S once; anything else is a 400.
    /// </summary>
    public string mapSolutions(string reductionFromSolution) {
        // Read as Partition's verifier reads it, repeats included: {(3),(3)} puts a 3 on each side.
        List<List<string>> sides = PartitionVerifier.ReadSides(reductionFromSolution)
            ?? throw new ReductionInputException(this, reductionFromSolution, reductionFrom.certificateFormat, $"'{reductionFromSolution}' is not two sides like (1,7),(8)");
        var side = new int?[reductionFrom.S.Count];
        for (int s = 0; s < 2; s++) {
            foreach (string number in sides[s]) {
                // The first copy of the number (by value) not on a side yet, so two equal numbers are two nodes.
                int i = int.TryParse(number, out int value)
                    ? Enumerable.Range(0, reductionFrom.S.Count).FirstOrDefault(k => int.Parse(reductionFrom.S[k]) == value && side[k] == null, -1)
                    : -1;
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

    public Placement EmptyAnswer() => new Placement([]);

    // "{}" on either side keeps the default (the verifiers): Weighted Cut's verifier judges the empty cut, and Partition never
    // takes "{}" as a split.

    // How the map-back steps show a node read: the split so far (the side of each number placed), and the node.
    private static RunSteps<string, Placement> NewMapSteps(StepRecorder<Placement> rec, int[] assign) => new(rec,
        // What every picture shows: the split so far, and the node the step is about.
        partial: _ => new Placement(assign),
        focus: node => [node]);

    // Backward map: every two nodes are joined, so node j is across the cut from node 1 exactly when edge {1,j} is cut.
    // Its steps, one per node in order, all Accepts: node 1's number goes to side S1, then each other node's number to S1
    // (its edge to node 1 is not cut) or S2 (it is). The caller adds the Done.
    public (string Answer, Placement Partial) MapBack(string problemToSolution, StepRecorder<Placement> rec) {
        var nodes = reductionTo.nodes;
        var cut = ReadCut(problemToSolution);
        foreach (var (u, v) in cut)
            if (!nodes.Contains(u) || !nodes.Contains(v))
                throw new FormatException($"'{{{u},{v}}}' is not an edge of the Weighted Cut instance");
        var assign = Enumerable.Repeat(-1, nodes.Count).ToArray();
        var steps = NewMapSteps(rec, assign);
        for (int i = 0; i < nodes.Count; i++) {
            string node = nodes[i], number = reductionFrom.S[i];
            if (i == 0) {
                assign[0] = 0;
                steps.Accept(node, $"Node {node} is number {number}; it goes to side S1, and every other node's side is read from its edge to {node}.");
                continue;
            }
            bool across = cut.Contains((nodes[0], node)) || cut.Contains((node, nodes[0]));
            assign[i] = across ? 1 : 0;
            if (across)
                steps.Accept(node, $"Edge {{{nodes[0]},{node}}} is cut, so node {node} is across from node {nodes[0]}: {number} goes to side S2.");
            else
                steps.Accept(node, $"Edge {{{nodes[0]},{node}}} is not cut, so node {node} is on node {nodes[0]}'s side: {number} goes to side S1.");
        }
        string Side(int s) => "(" + string.Join(",", Enumerable.Range(0, nodes.Count).Where(i => assign[i] == s).Select(i => reductionFrom.S[i])) + ")";
        return ("{" + Side(0) + "," + Side(1) + "}", new Placement(assign));
    }

    // A Weighted Cut answer, {({1,2},7),({1,3},12)}: the cut edges with their weights. Malformed text throws FormatException,
    // which the endpoint reports as a 400.
    private static HashSet<(string, string)> ReadCut(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of weighted edges in braces like {{({{1,2}},7)}}");
        try {
            var edges = new HashSet<(string, string)>();
            foreach (SPADE.UtilCollection edge in new SPADE.UtilCollection(text).ToList()) {
                var ends = edge[0].ToList();
                if (ends.Count != 2) throw new FormatException($"'{edge}' is not an edge with two ends");
                edges.Add((ends[0].ToString(), ends[1].ToString()));
            }
            return edges;
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not a set of weighted edges like {{({{1,2}},7)}}", ex);
        }
    }
}
// return an instance of what you are reducing to