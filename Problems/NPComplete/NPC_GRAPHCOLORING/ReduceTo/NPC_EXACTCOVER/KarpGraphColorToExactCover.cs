using System.Linq;
using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo;
using API.Problems.NPComplete.NPC_GRAPHCOLORING;
using API.Problems.NPComplete.NPC_EXACTCOVER;

namespace API.Problems.NPComplete.NPC_GRAPHCOLORING.ReduceTo.NPC_EXACTCOVER;

class KarpGraphColorToExactCover : IReduction<GRAPHCOLORING, EXACTCOVER> {

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
    public KarpGraphColorToExactCover(GRAPHCOLORING from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public KarpGraphColorToExactCover(string instance) : this(new GRAPHCOLORING(instance)) { }
    public KarpGraphColorToExactCover() : this(new GRAPHCOLORING()) { }

    // Elements: a node u is "u", an edge {u,v} is "u_v" and the slot of u at edge {u,v} for color j is "u_u_v_j". A name
    // that holds "_" (or the escape character) is written with a \ before every \ and "_", so a node, an edge and a slot
    // never share an element; other names are written as they are (#824).
    private static string EdgeElement(string u, string v) => ElementName(u) + "_" + ElementName(v);

    private static string SlotElement(string node, string u, string v, int color) => ElementName(node) + "_" + EdgeElement(u, v) + "_" + color;

    private static string ElementName(string name) =>
        name.IndexOfAny(['\\', '_']) < 0 ? name : name.Replace(@"\", @"\\").Replace("_", @"\_");

    public EXACTCOVER reduce() {
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

        reductionTo = reducedExactCover;
        return reducedExactCover;
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