using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_PARTITION.Solvers;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_PARTITION;

class PARTITION : IProblem<PartitionBruteForce, PartitionVerifier, DummyVisualization>
{

    // --- Fields ---
    public string problemName { get; } = "Partition";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Partition_problem";
    public string formalDefinition { get; } = "Partition = <S, I> | S is a set of positive integers and there exists a subset of S, I where the sum of I's elements equals the sum of elements not in set I";
    public string problemDefinition { get; } = "The partition problem is the task of deciding whether a given multiset S of positive integers can be partitioned into two subsets S1 and S2 such that the sum of the numbers in S1 equals the sum of the numbers in S2";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    private static string _defaultInstance = "{1,7,12,15,33,12,11,5,6,9,21,18}";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;
    private List<string> _S = new List<string>();
    public string wikiName { get; } = "";
    public PartitionBruteForce defaultSolver { get; } = new PartitionBruteForce();
    public PartitionVerifier defaultVerifier { get; } = new PartitionVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    // --- Properties ---
    public List<string> S
    {
        get
        {
            return _S;
        }
        set
        {
            _S = value;
        }
    }

    // --- Methods Including Constructors ---
    public PARTITION() : this(_defaultInstance)
    {
    }
    public PARTITION(string instance)
    {
        this.instance = instance;

        StringParser partition = new("{N | N is set}");
        partition.parse(instance);
        S = partition["N"].ToList().Select(node => node.ToString()).ToList();
    }

}