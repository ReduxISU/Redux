using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Solvers;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_INTPROGRAMMING01;

class INTPROGRAMMING01 : IProblem<IntegerProgrammingBruteForce, GenericVerifier01INTP, DummyVisualization>
{

    // --- Fields ---
    public string problemName { get; } = "0-1 Integer Programming";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Integer_programming";
    public string formalDefinition { get; } = "0-1 Integer Programming = {<C,d> | C is an m*n matrix, d is a m-vector, and a n-vector x exists such that Cx is <= d. }";
    public string problemDefinition { get; } = "0-1 Integer Programming is a system of inequalities, where each variable can be either a 0 or a 1. It is represented by a matrix, where each collumn is a variable, and each row is an inequality. In this implementation the inequality is alway <=. A problem is 0-1 integer programable, if each variable has an assignment of 0 or 1, such that each inequality is satisfiable.";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public static string _defaultInstance { get; } = "(-1 1 -1),(0 0 -1),(-1 -1 1)<=(0 0 0)";
    public string defaultInstance { get; } = _defaultInstance;
    private List<List<int>> _C = new List<List<int>>();
    private List<int> _d = new List<int>();
    public string wikiName { get; } = "";
    public IntegerProgrammingBruteForce defaultSolver { get; } = new IntegerProgrammingBruteForce();
    public GenericVerifier01INTP defaultVerifier { get; } = new GenericVerifier01INTP();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string instance { get; set; } = string.Empty;
    public string[] contributors { get; } = { "Caleb Eardley" };

    // --- Properties ---
    public List<List<int>> C
    {
        get
        {
            return _C;
        }
        set
        {
            _C = value;
        }
    }
    public List<int> d
    {
        get
        {
            return _d;
        }
        set
        {
            _d = value;
        }
    }

    // --- Methods Including Constructors ---
    public INTPROGRAMMING01() : this(_defaultInstance)
    {

    }
    public INTPROGRAMMING01(string instanceInput)
    {
        // TODO Validate there are only a maximum of 3 literals in each clause
        instance = instanceInput;
        C = getMatrixC(instance);
        d = getVectorD(instance);
    }

    public List<List<int>> getMatrixC(string G)
    {
        string strippedG = G.Replace(" )", "").Replace("( ", "").Replace("(", "").Replace(")", "");
        string[] matrixString = strippedG.Split("<=")[0].Split(",");
        List<List<int>> C = new List<List<int>>();
        for (int i = 0; i < matrixString.Length; i++)
        {
            string[] stringVariables = matrixString[i].Split(" ");
            List<int> row = new List<int>();
            for (int j = 0; j < stringVariables.Length; j++)
            {
                row.Add(int.Parse(stringVariables[j]));
                //row.Add(stringVariables[j]);
            }
            C.Add(row);
        }
        return C;

    }

    public List<int> getVectorD(string G)
    {
        string strippedG = G.Replace(" )", "").Replace("( ", "").Replace("(", "").Replace(")", "");
        string[] vectorStringArray = strippedG.Split("<=")[1].Split(" ");
        List<int> d = new List<int>();
        for (int i = 0; i < vectorStringArray.Length; i++)
        {
            d.Add(int.Parse(vectorStringArray[i]));
            //d.Add(vectorStringArray[i]);
        }
        return d;
    }
}