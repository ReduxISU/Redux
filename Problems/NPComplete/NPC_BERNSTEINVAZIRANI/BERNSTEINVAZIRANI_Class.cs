using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;
using API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Verifiers;
using SPADE;
using System.Reflection;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI;

class BERNSTEINVAZIRANI : IProblem<BernsteinVaziraniClassicalSolver, BernsteinVaziraniClassicalVerifier, DummyVisualization>
{

    // --- Fields ---
    public string problemName {get;} = "Bernstein Vazirani"; // Name as it appears in the dropdown selection panel
    public string problemLink {get;} = "https://en.wikipedia.org/wiki/Bernstein%E2%80%93Vazirani_algorithm"; // Link to the Wikipedia page for the problem
    public string formalDefinition {get;} =  "Bernstein Vazirani = {(n, <w_1, w_2, ... , w_(2^n - 1), w_(2^n)> | n is int, w_i is bit (0 or 1)}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "The Bernstein-Vazirani problem asks for the identification of an unknown bit string s that defines a linear Boolean function f(x)= s*x (mod 2). The task is to determine the hidden string s using as few queries as possible"; // plaintext description of the problem
    public string source { get; } = "Bernstein, Ethan, and Umesh, Vazirani. Quantum complexity theory. Proceedings of the twenty-fifth annual ACM symposium on Theory of computing. 1993."; // Academic paper proper citation
    public string sourceLink { get; } = "https://dl.acm.org/doi/pdf/10.1145/167088.167097"; // Link to the academic paper
    private static readonly string _defaultInstance = "(2,(1, 1, 1, 1))";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Wiki name or link? - not used yet
    public BernsteinVaziraniClassicalSolver defaultSolver {get;} = new BernsteinVaziraniClassicalSolver();
    public BernsteinVaziraniClassicalVerifier defaultVerifier { get; } = new BernsteinVaziraniClassicalVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara", "Jason L. Wright" };

    // --- Methods and Constructors ---
    public BERNSTEINVAZIRANI() : this(_defaultInstance) {
    }

    private List<bool> _funcValues = new List<bool>{false, true};
    
    public List<bool> funcValues {
        get {
            return _funcValues;
        }
        set {
            _funcValues = value;
        }
    }

    private int nbits = 1;
    public int NBits {
        get {
            return nbits;
        }
        set {
            nbits = value;
        }
    }

    public bool Func(int x)
    {
        if (x < 0 || x >= funcValues.Count) {
            // XXX deal with error?
            Console.WriteLine($"{this.GetType().Name}:{MethodBase.GetCurrentMethod()?.Name}: input {x} out of range for function values array of length {funcValues.Count}");
            return false;
        }
        return funcValues[x];
    }

    public BERNSTEINVAZIRANI(string input) {
        instance = input;

        StringParser parser = new("{(n, S) | n is int, S is list}");
        parser.parse(instance);

        int n = int.Parse(parser["n"].ToString());
        UtilCollection bitslist = parser["S"];

        var fvalues = new List<bool>();
        bitslist.ToList().ForEach(x => {
            if (x.ToString() == "0") {
                fvalues.Add(false);
            } else if (x.ToString() == "1") {
                fvalues.Add(true);
            } else {
                Console.WriteLine($"{this.GetType().Name}:{MethodBase.GetCurrentMethod()?.Name}: encountered non-bit value {x.ToString()} in function values list");
                // XXX how do deal with error?
            }
        });

        if (fvalues.Count != Math.Pow(2, n)) {
            Console.WriteLine($"{this.GetType().Name}:{MethodBase.GetCurrentMethod()?.Name}: wrong length of function value list (should be {Math.Pow(2, n)}, got {fvalues.Count})");
            // XXX the array is the wrong size... how to deal with error?
            return;
        }

        nbits = n;
        funcValues = fvalues;
    }
}
