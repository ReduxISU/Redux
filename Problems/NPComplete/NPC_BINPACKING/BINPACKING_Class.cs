// BINPACKING_Class.cs
// This is the main problem class for Bin Packing.
// It stores the problem instance (a multiset of item sizes S, a bin capacity C,
// and a bin limit K), and wires up the default brute-force solver and verifier.
// Redux discovers this class automatically via the IProblem interface at runtime —
// no manual registration in Program.cs is needed.

using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_BINPACKING.Solvers;
using API.Problems.NPComplete.NPC_BINPACKING.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_BINPACKING;

class BINPACKING : IProblem<BinPackingBruteForce, BinPackingVerifier, DummyVisualization> {

    // ── Human-readable metadata shown in the Redux UI 

    public string problemName { get; } = "Bin Packing";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Bin_packing_problem";

    // The formal language definition: an instance is a YES-instance if and only if
    // there exists a valid packing of all items into at most K bins, each within capacity C.
    public string formalDefinition { get; } = "BINPACKING = {<S, C, K> | S is a multiset of positive integer item sizes, C is a positive integer bin capacity, K is a positive integer bin limit, and there exists a partition of S into at most K subsets B_1,...,B_m (m <= K) such that for each B_i the sum of its elements is at most C.}";

    // Plain-English explanation of what the problem is asking.
    // Also notes that the decision variant is NP-Complete and the optimization
    // variant (minimizing bin count) is NP-Hard — a useful distinction for students.
    public string problemDefinition { get; } = "The Bin Packing decision problem asks: given a multiset of item sizes, a bin capacity C, and a bin limit K, can all items be packed into at most K bins such that the total size in each bin does not exceed C? Bin Packing is NP-Complete; the optimization variant (minimize the number of bins) is NP-Hard.";

    // Academic citation — Garey & Johnson is the canonical NP-Completeness reference.
    // Problem SR1 in their appendix is the Bin Packing entry.
    public string source { get; } = "Garey, M. R., and Johnson, D. S. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SR1.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Bin_packing_problem";

    public string[] contributors { get; } = { "Himanshu", "Rakesh", "Prashanta" };



    // The current instance string (set by the constructor or by Redux when a user
    // types a custom input into the UI).
    public string instance { get; set; } = string.Empty;

    // The example instance shown to users when the page first loads:
    // 6 items of sizes {4,7,3,6,2,8}, bin capacity 10, and at most 3 bins.
    // This is a YES-instance — the items do fit (e.g. bins (4,6), (7,3), (2,8)).
    private static readonly string _defaultInstance = "((4,7,3,6,2,8),10,3)";
    public string defaultInstance { get; } = _defaultInstance;

    public string wikiName { get; } = "";



    // S — the multiset of item sizes parsed from the instance string.
    // Using "multiset" rather than "set" is important: duplicate sizes are allowed.
    private List<int> _S = new List<int>();

    // C — the capacity of each bin (every bin has the same capacity).
    private int _C;

    // K — the maximum number of bins we are allowed to use.
    private int _K;



    // Brute force is the default solver because it is always correct.
    // FFD is registered separately as a named heuristic solver.
    public BinPackingBruteForce defaultSolver { get; } = new BinPackingBruteForce();
    public BinPackingVerifier defaultVerifier { get; } = new BinPackingVerifier();

    // No custom visualization — we reuse the shared DummyVisualization,
    // Dummy visualization is an approach also used by KNAPSACK and JOBSEQ.
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    // Declared, not derived. BINPACKING is NP-complete (Garey & Johnson, 1979, problem SR1).
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.StorageAndRetrieval;

    public List<int> S {
        get { return _S; }
        set { _S = value; }
    }

    public int C {
        get { return _C; }
        set { _C = value; }
    }

    public int K {
        get { return _K; }
        set { _K = value; }
    }



    // Default constructor — loads the built-in example instance.
    public BINPACKING() : this(_defaultInstance) {
    }

    // Main constructor — parses any valid instance string using SPADE.
    // The parse pattern "{(S,C,K) | S is list, C is int, K is int}" matches
    // the format "((item1,item2,...),C,K)", which is the same structure used
    // by JOBSEQ and other multi-component NPC problems in Redux.
    public BINPACKING(string input) {
        instance = input;

        StringParser parser = new("{(S,C,K) | S is list, C is int, K is int}");
        parser.parse(input);

        // Each element of the parsed list comes back as an object; we cast to int.
        _S = parser["S"].ToList().Select(item => int.Parse(item.ToString())).ToList();
        _C = int.Parse(parser["C"].ToString());
        _K = int.Parse(parser["K"].ToString());
    }
}
