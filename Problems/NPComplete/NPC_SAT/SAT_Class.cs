using API.Interfaces;
using API.Interfaces.Logic;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;

namespace API.Problems.NPComplete.NPC_SAT;

class SAT : IProblem<SATBruteForceSolver, SATVerifier, SatDefaultVisualization> {


    #region Fields

    // --- Fields ---
    public string problemName { get; } = "SAT";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Boolean_satisfiability_problem";
    public string formalDefinition { get; } = "SAT = {Φ | Φ is a satisfiable Boolean formula in CNF}";
    public string problemDefinition { get; } = "SAT is the Boolean satisfiability problem for formulas in conjunctive normal form (CNF): a conjunction of clauses, each a disjunction of literals. It asks whether there is an assignment of True/False values to the variables of Φ that makes the whole formula True. This is Karp's formulation; satisfiability of an arbitrary Boolean formula reduces to it (Tseitin).";
    public string inputDescription { get; } = "Φ, a Boolean formula in CNF";
    public string outputDescription { get; } = "True or False, whether Φ is satisfiable";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Daniel Igbokwe" };

    public static string _defaultInstance { get; } = "(x1 | x2 | x3 | x4) & (!x1 | !x2) & (!x3 | x4) & (!x4 | x2) & (!x2 | !x3)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instanceFormat { get; } = "Boolean formula in CNF. Clauses joined by '&', literals within a clause joined by '|', negation prefix '!' written directly before the name. Variable names start with a letter or '_' and contain only letters, digits and '_'. When there are several clauses, each clause with more than one literal must be in parentheses. Each clause has 1+ literals of any size. Example: (x1 | !x2 | x3) & (!x1 | x2)";
    public string certificateFormat { get; } = "Comma-separated variable:value pairs, optionally wrapped in parentheses. Booleans must be capitalized True/False (T/F also accepted); ':' or '=' may be used as the separator. List every variable you are assigning. Example: (x1:True,x2:False,x3:True)";
    public string instance { get; set; } = string.Empty;

    public string wikiName { get; } = "";
    private List<List<string>> _clauses = new List<List<string>>();
    private List<string> _literals = new List<string>();

    private string _circuit = "";

    public string circuit {
        get {
            return _circuit;
        }
        set {
            _circuit = value;
        }
    }

    public SATBruteForceSolver defaultSolver { get; } = new SATBruteForceSolver();
    public SATVerifier defaultVerifier { get; } = new SATVerifier();
    public SatDefaultVisualization defaultVisualization { get; } = new SatDefaultVisualization();
    // Declared, not derived. SAT is the canonical NP-complete problem (Cook-Levin).
    public ComplexityClass complexityClass { get; } = ComplexityClass.NPComplete;
    public ProblemType problemType { get; } = ProblemType.Logic;
    public ProblemVariant problemVariant { get; } = ProblemVariant.Decision;

    #endregion


    #region Properties
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

    #endregion

    // The parsed instance. Internal so it stays out of the serialized problem JSON.
    // Note: clauses/literals still have public setters for older callers; code that assigns
    // them directly does not update this.
    internal CnfFormula formula { get; private set; }

    #region Constructors
    // --- Methods Including Constructors ---
    public SAT() : this(_defaultInstance) {
    }
    public SAT(string phiInput) {
        formula = parseInstance(phiInput);
        instance = phiInput;
        clauses = formula.ClauseStrings();
        literals = formula.LiteralStrings();
    }

    private static CnfFormula parseInstance(string phiInput) {
        if (string.IsNullOrWhiteSpace(phiInput)) {
            throw new ProblemParseException("SAT", phiInput, "instance is empty");
        }
        try {
            return CnfParser.Parse(phiInput);
        } catch (CnfParseException e) {
            throw new ProblemParseException("SAT", phiInput, e.Message, e);
        }
    }

    #endregion


    #region Methods

    public void ParseProblem(string phiInput) {
    }

    public List<List<string>> getClauses(string phiInput) {
        return parseInstance(phiInput).ClauseStrings();
    }

    public List<string> getLiterals(string phiInput) {
        return parseInstance(phiInput).LiteralStrings();
    }

    #endregion

    #region Certificates
    // SAT and 3SAT write an answer the same way, so the reductions into and out of either read and write it with these.

    /// <summary>An assignment as certificate text, "(x1:True,x2:False)", in the given order.</summary>
    public static string Certificate(IEnumerable<string> order, IReadOnlyDictionary<string, bool> values) =>
        "(" + string.Join(",", order.Select(v => $"{v}:{Assignment.Word(values[v])}")) + ")";

    /// <summary>
    /// An assignment as a SAT or 3SAT solver writes it, <c>(x1:True,x2:False)</c> (':' or '=', T and F accepted). Every name
    /// must be one of <paramref name="variables"/>; malformed text throws <see cref="FormatException"/>, which the endpoint
    /// reports as a 400. <paramref name="formula"/> names the problem ("SAT" or "3SAT") in that message.
    /// </summary>
    public static Dictionary<string, bool> ReadAssignment(string certificate, IEnumerable<string> variables, string formula) {
        var known = new HashSet<string>(variables, StringComparer.Ordinal);
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        string text = certificate.Trim();
        if (text.StartsWith('(') && text.EndsWith(')')) text = text[1..^1];
        foreach (string raw in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            string[] parts = raw.Split(':', '=');
            bool? value = parts.Length != 2 ? null : parts[1].Trim().ToLowerInvariant() switch {
                "true" or "t" => true,
                "false" or "f" => false,
                _ => null,
            };
            if (value is not bool v) throw new FormatException($"'{raw}' is not a variable:True or variable:False pair");
            string name = parts[0].Trim();
            if (!known.Contains(name)) throw new FormatException($"'{name}' is not a variable of the {formula} formula");
            values[name] = v;
        }
        return values;
    }

    #endregion

}
