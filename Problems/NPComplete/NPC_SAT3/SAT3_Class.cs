using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;

namespace API.Problems.NPComplete.NPC_SAT3;

class SAT3 : IProblem<Sat3BacktrackingSolver,SAT3Verifier,Sat3DefaultVisualization> {

    // --- Fields ---
    public string problemName {get;} = "3SAT";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Boolean_satisfiability_problem#3-satisfiability";
    public string formalDefinition {get;} = "3SAT = {Φ | Φ is a satisfiable Boolean formula in 3CNF}";
    public string problemDefinition {get;} = "3SAT, or the Boolean satisfiability problem, is a problem that asks for a list of assignments to the literals of phi (with a maximum of 3 literals per clause) to result in 'True'.";
    public string[] contributors {get;} = { "Kaden Marchetti"};
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string defaultInstance {get;} = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)";
    public Sat3BacktrackingSolver defaultSolver {get;} = new Sat3BacktrackingSolver();
    public SAT3Verifier defaultVerifier {get;} = new SAT3Verifier();
    public Sat3DefaultVisualization defaultVisualization { get; } = new Sat3DefaultVisualization();
    public string instance {get;set;} = string.Empty;

    public string wikiName {get;} = "";
    private List<List<string>> _clauses = new List<List<string>>();
    private List<string> _literals = new List<string>();

    // --- Properties ---
    public List<List<string>> clauses {
        get {
            return _clauses;
        }
        set {
            _clauses = value;
        }
    }
    public List<string> literals {
        get {
            return _literals;
        }
        set {
            _literals = value;
        }
    }


    // --- Methods Including Constructors ---
    public SAT3() {
        instance = defaultInstance;
        clauses = getClauses(instance);
        literals = getLiterals(instance);
    }
    public SAT3(string phiInput) {

        // TODO Validate there are only a maximum of 3 literals in each clause

        instance = phiInput;
        clauses = getClauses(instance);
        literals = getLiterals(instance);
    }

    public List<List<string>> getClauses(string phiInput) {
        
        List<List<string>> clauses = new List<List<string>>();

        // Strip extra characters
        string strippedInput = phiInput.Replace(" ", "").Replace("(", "").Replace(")","");

        // Parse on | to collect each clause
        string[] rawClauses = strippedInput.Split('&');

        foreach(string clause in rawClauses) {
            List<string> clauseToAdd = new List<string>();
            string[] literals = clause.Split('|');

            foreach(string literal in literals) {
                clauseToAdd.Add(literal);
            }
            clauses.Add(clauseToAdd);
        }

        return clauses;

    }

    public List<string> getLiterals(string phiInput) {
        
        List<string> literals = new List<string>();
        string strippedInput = phiInput.Replace(" ", "").Replace("(", "").Replace(")","");

        // Parse on | to collect each clause
        string[] rawClauses = strippedInput.Split('&');

        foreach(string clause in rawClauses) {
            string[] rawLiterals = clause.Split('|');

            foreach(string literal in rawLiterals) {
                literals.Add(literal);
            }
        }
        return literals;
    }
}