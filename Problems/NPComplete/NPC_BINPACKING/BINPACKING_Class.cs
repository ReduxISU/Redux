using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_BINPACKING.Solvers;
using API.Problems.NPComplete.NPC_BINPACKING.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_BINPACKING;

class BINPACKING : IProblem<BinPackingBruteForce, BinPackingVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "Bin Packing";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Bin_packing_problem";

    public string formalDefinition { get; } = "BINPACKING = {<S, C, K> | S is a multiset of positive integer item sizes, C is a positive integer bin capacity, K is a positive integer bin limit, and there exists a partition of S into at most K subsets B_1,...,B_m (m <= K) such that for each B_i the sum of its elements is at most C.}";

    public string problemDefinition { get; } = "The Bin Packing decision problem asks: given a multiset of item sizes, a bin capacity C, and a bin limit K, can all items be packed into at most K bins such that the total size in each bin does not exceed C? Bin Packing is NP-Complete; the optimization variant (minimize the number of bins) is NP-Hard.";

    public string source { get; } = "Garey, M. R., and Johnson, D. S. Computers and Intractability: A Guide to the Theory of NP-Completeness. W. H. Freeman, 1979. Problem SR1.";
    public string sourceLink { get; } = "https://en.wikipedia.org/wiki/Bin_packing_problem";

    public string[] contributors { get; } = { "TBD" };

    public string instance { get; set; } = string.Empty;

    private static readonly string _defaultInstance = "((4,7,3,6,2,8),10,3)";
    public string defaultInstance { get; } = _defaultInstance;

    public string wikiName { get; } = "";

    private List<int> _S = new List<int>();
    private int _C;
    private int _K;

    public BinPackingBruteForce defaultSolver { get; } = new BinPackingBruteForce();
    public BinPackingVerifier defaultVerifier { get; } = new BinPackingVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    // --- Properties ---
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

    // --- Methods Including Constructors ---
    public BINPACKING() : this(_defaultInstance) {
    }

    public BINPACKING(string input) {
        instance = input;

        StringParser parser = new("{(S,C,K) | S is list, C is int, K is int}");
        parser.parse(input);
        _S = parser["S"].ToList().Select(item => int.Parse(item.ToString())).ToList();
        _C = int.Parse(parser["C"].ToString());
        _K = int.Parse(parser["K"].ToString());
    }
}
