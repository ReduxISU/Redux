using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_DM3.Solvers;
using API.Problems.NPComplete.NPC_DM3.Verifiers;
using SPADE;
using System.Collections;

namespace API.Problems.NPComplete.NPC_DM3;

class DM3 : IProblem<ThreeDimensionalMatchingBruteForce, GenericVerifierDM3, DummyVisualization> {

    // --- Fields ---
    public string problemName { get; } = "3-Dimensional Matching";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/3-dimensional_matching";
    public string formalDefinition { get; } = "{<M,X,Y,Z> | M is a subset of X*Y*Z,|X|=|Y|=|Z| and a subset of M, M', exists, where |M'| = |A|,|B|,|C|, and no two elements of M' agree in any cooridinate}";
    public string problemDefinition { get; } = "3-Dimensional Matching is when, given 3 equally sized sets, X, Y, and Z, and a set of constraints M, being a subset of XxYxZ, are you able to select a set of constraints which contain each element of X, Y, and Z in one and only one 3-tuple.";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public static string _defaultInstance { get; } = "{Paul,Sally,Dave}{Madison,Austin,Bob}{Chloe,Frank,Jake}{Paul,Madison,Chloe}{Paul,Austin,Jake}{Sally,Bob,Chloe}{Sally,Madison,Frank}{Dave,Austin,Chloe}{Dave,Bob,Chloe}"; // simply a list of sets with the elements divided by commas, the first three are asumed to be X, Y, and Z, and all subsequent sets are sets in M
    public string defaultInstance { get; } = _defaultInstance;
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    private List<string> _X;
    private List<string> _Y;
    private List<string> _Z;
    private List<List<string>> _M;
    public ThreeDimensionalMatchingBruteForce defaultSolver { get; } = new ThreeDimensionalMatchingBruteForce();
    public GenericVerifierDM3 defaultVerifier { get; } = new GenericVerifierDM3();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    // Declared, not derived. DM3 (3-Dimensional Matching) is NP-complete (Karp, 1972).
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;

    public string[] contributors { get; } = { "Caleb Eardley" };


    // --- Properties ---
    public List<string> X {
        get {
            return _X;
        }
        set {
            _X = value;
        }
    }
    public List<string> Y {
        get {
            return _Y;
        }
        set {
            _Y = value;
        }
    }
    public List<string> Z {
        get {
            return _Z;
        }
        set {
            _Z = value;
        }
    }
    public List<List<string>> M {
        get {
            return _M;
        }
        set {
            _M = value;
        }
    }

    // --- Methods Including Constructors ---
    public DM3() : this(_defaultInstance) {
    }
    public DM3(string instanceInput) {
        instance = instanceInput;
        _X = ParseProblem(instance, "X");
        _Y = ParseProblem(instance, "Y");
        _Z = ParseProblem(instance, "Z");
        _M = ParseM(instance);
    }
    // The instance is a sequence of brace-delimited groups: {x1,...}{y1,...}{z1,...}{x,y,z},...
    // The first three groups are X, Y, and Z (each on its own, independent of the other groups'
    // sizes); every group after that is one 3-tuple candidate for M. Splitting on "}{" preserves
    // those group boundaries, which is what lets ParseProblem/ParseM stop at the end of their own
    // group instead of striding by 3 across the whole flattened, boundary-less string.
    private static List<List<string>> SplitGroups(string instanceInput) {
        string trimmed = instanceInput.Trim();
        if (trimmed.StartsWith("{")) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith("}")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
        if (trimmed.Length == 0) return new List<List<string>>();
        return trimmed.Split(new[] { "}{" }, StringSplitOptions.None)
            .Select(group => group.Split(',').ToList())
            .ToList();
    }
    /*************************************************
   ParseProblem(string instanceInput) takes the string representation of the 3-Dimensional Matching problem, and returns a
   3 dimensional list, the first depths of list, contains two lists, one with the sets X,Y,and Z, and the other containing all the sets in M.
   ***************************************************/
    public List<string> ParseProblem(string instanceInput, string set) {
        List<List<string>> groups = SplitGroups(instanceInput);
        int index = 0;
        if (set == "Y") index = 1;
        if (set == "Z") index = 2;
        List<string> variables = new List<string>();
        if (index >= groups.Count) return variables;
        foreach (string element in groups[index]) {
            if (!variables.Contains(element)) variables.Add(element);
        }
        return variables;
    }
    public List<List<string>> ParseM(string instanceInput) {
        List<List<string>> groups = SplitGroups(instanceInput);
        List<List<string>> M = new List<List<string>>();
        for (int i = 3; i < groups.Count; i++) {
            M.Add(groups[i]);
        }
        return M;
    }
}


