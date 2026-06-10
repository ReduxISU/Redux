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
    public string instanceFormat {get;} = "Boolean formula in 3-CNF. Clauses joined by '&', literals within a clause joined by '|', negation prefix '!'. Each clause has at most 3 literals. Example: (x1 | !x2 | x3) & (!x1 | x3 | x1)";
    public string certificateFormat {get;} = "Comma-separated variable:Boolean pairs, optionally wrapped in parentheses. Booleans must be capitalized True/False (T/F also accepted); ':' or '=' may be used as the separator. List every variable you are assigning. Example: (x1:True,x2:False,x3:True)";
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

        validateInstance(phiInput);

        instance = phiInput;
        clauses = getClauses(instance);
        literals = getLiterals(instance);
    }

    private static void validateInstance(string phiInput) {
        if (string.IsNullOrWhiteSpace(phiInput)) {
            throw new ProblemParseException("3SAT", phiInput, "instance is empty");
        }

        string stripped = phiInput.Replace(" ", "").Replace("(", "").Replace(")", "");
        string[] rawClauses = stripped.Split('&');
        foreach (string clause in rawClauses) {
            string[] rawLiterals = clause.Split('|');
            if (rawLiterals.Length < 1 || rawLiterals.Length > 3) {
                throw new ProblemParseException("3SAT", phiInput,
                    $"clause '{clause}' has {rawLiterals.Length} literals; 3-CNF allows 1-3");
            }
            foreach (string literal in rawLiterals) {
                string name = literal.StartsWith("!") ? literal.Substring(1) : literal;
                if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0])) {
                    throw new ProblemParseException("3SAT", phiInput,
                        $"literal '{literal}' is not a valid identifier (optionally prefixed with '!')");
                }
            }
        }
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