using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Solvers;
using API.Problems.NPComplete.NPC_DEUTSCHJOZSA.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_DEUTSCHJOZSA;

class DEUTSCHJOZSA : IProblem<DeutschJozsaClassicalSolver, DeutschJozsaVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Deutsch Jozsa"; // Name as it appears in the dropdown selection panel
    public string formalDefinition {get;} =  "Deutsch Jozsa = {(n, <w_1, w_2, ... , w_(2^n - 1), w_(2^n)> | n is int, w_i is bit (0 or 1)}"; // Mathematical description of the problem (todo later)
    public string problemDefinition { get; } = "Deutsch-Jozsa's algorithm solves the parity problem. It is represented by an ordered list......"; // plaintext description of the problem
    public string source { get; } = "todo"; // Academic paper proper citation
    public string sourceLink { get; } = "todo"; // Link to the academic paper
    private static readonly string _defaultInstance = "(2,(1, 1, 1, 1))";
    public string defaultInstance {get;} = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    public string wikiName {get;} = ""; // Wiki name or link? - not used yet
    public DeutschJozsaClassicalSolver defaultSolver {get;} = new DeutschJozsaClassicalSolver();
    public DeutschJozsaVerifier defaultVerifier { get; } = new DeutschJozsaVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();
    public string[] contributors {get;} = { "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara", "George Lake" };
    
    public int n { get; set; }
    public List<int> w {get; set; } = new List<int>();

    // --- Methods and Constructors ---
    public DEUTSCHJOZSA() : this(_defaultInstance) {}

    public DEUTSCHJOZSA(string input) {
        instance = input;

        // use SPADE parser to extract n and the list S
        StringParser parser = new("{(n, S) | n is int, S is list}");
        Console.WriteLine($"Parsing instance: {instance}");
        parser.parse(instance);
        n = int.Parse(parser["n"].ToString());
        SPADE.UtilCollection bitslist = parser["S"];
        // Console.WriteLine(bitslist);

        // Convert to C# list
        w = new List<int>();
        foreach (var bit in bitslist)
        {
            w.Add(int.Parse(bit.ToString()));
        }

        // --- Validation -----------------------------------------
        int expectedSize = (int)Math.Pow(2, n);
        if (w.Count != expectedSize)
        {
            throw new ArgumentException($"Invalid instance: n is {n}, which requires {expectedSize} bits in the list, but {w.Count} were given.");
        }

        // This parsing is left over from the template, but we're pretty sure it works for extracting the values from the input
        // Good luck solver team :) - problems team
        // It worked good! -GL

    }

}
