using API.Interfaces;
using API.Interfaces.Logic;
using API.Problems.NPComplete.NPC_SAT3.Solvers;
using API.Problems.NPComplete.NPC_SAT3.Verifiers;

namespace API.Problems.NPComplete.NPC_SAT3;

class SAT3 : IProblem<Sat3BacktrackingSolver, SAT3Verifier, Sat3DefaultVisualization> {

    // --- Fields ---
    public string problemName { get; } = "3SAT";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Boolean_satisfiability_problem#3-satisfiability";
    public string formalDefinition { get; } = "3SAT = {Φ | Φ is a satisfiable Boolean formula in 3CNF}";
    public string problemDefinition { get; } = "3SAT is the Boolean satisfiability problem restricted to formulas in conjunctive normal form with at most three literals per clause. It asks whether there is an assignment of True/False values to the variables of Φ that makes the whole formula True.";
    public string inputDescription { get; } = "Φ, a boolean formula";
    public string outputDescription { get; } = "True or False, whether Φ is satisfiable";
    public string[] contributors { get; } = { "Kaden Marchetti" };
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public static string _defaultInstance { get; } = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = "Boolean formula in 3-CNF. Clauses joined by '&', literals within a clause joined by '|', negation prefix '!'. Each clause has at most 3 literals. Example: (x1 | !x2 | x3) & (!x1 | x3 | x1)";
    public string certificateFormat { get; } = "Comma-separated variable:Boolean pairs, optionally wrapped in parentheses. Booleans must be capitalized True/False (T/F also accepted); ':' or '=' may be used as the separator. List every variable you are assigning. Example: (x1:True,x2:False,x3:True)";
    public Sat3BacktrackingSolver defaultSolver { get; } = new Sat3BacktrackingSolver();
    public SAT3Verifier defaultVerifier { get; } = new SAT3Verifier();
    public Sat3DefaultVisualization defaultVisualization { get; } = new Sat3DefaultVisualization();
    // Declared, not derived. 3-SAT is NP-complete (Cook-Levin plus the standard
    // SAT-to-3SAT clause-splitting reduction); it is the canonical starting point for
    // most of this repo's NP-completeness reductions.
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.Logic;
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
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


    // The parsed instance. Internal so it stays out of the serialized problem JSON.
    // Note: clauses/literals still have public setters for older callers; code that assigns
    // them directly (KarpSATToSAT3, SAT3PQObject) does not update this.
    internal CnfFormula formula { get; private set; }


    // --- Methods Including Constructors ---
    public SAT3() : this(_defaultInstance) {
    }
    public SAT3(string phiInput) {
        formula = parseInstance(phiInput);
        instance = phiInput;
        clauses = formula.ClauseStrings();
        literals = formula.LiteralStrings();
    }

    private static CnfFormula parseInstance(string phiInput) {
        if (string.IsNullOrWhiteSpace(phiInput)) {
            throw new ProblemParseException("3SAT", phiInput, "instance is empty");
        }

        CnfFormula parsed;
        try {
            parsed = CnfParser.Parse(phiInput);
        } catch (CnfParseException e) {
            throw new ProblemParseException("3SAT", phiInput, e.Message, e);
        }

        foreach (CnfClause clause in parsed.Clauses) {
            if (clause.Literals.Count > 3) {
                throw new ProblemParseException("3SAT", phiInput,
                    $"clause '{clause}' at column {clause.Position + 1} has {clause.Literals.Count} literals; 3-CNF allows 1-3");
            }
        }
        return parsed;
    }

    public List<List<string>> getClauses(string phiInput) {
        return parseInstance(phiInput).ClauseStrings();
    }

    public List<string> getLiterals(string phiInput) {
        return parseInstance(phiInput).LiteralStrings();
    }
}
