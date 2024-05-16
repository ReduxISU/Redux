using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Problems.NPComplete.NPC_SAT.Verifiers;



namespace API.Problems.NPComplete.NPC_SAT;

 class SAT : IProblem<SATBruteForceSolver, SATVerifier> {


    #region Fields

    // --- Fields ---
    private string _problemName = "SAT";
    private string _formalDefinition = "SAT = {Φ | Φ is a satisfiable Boolean formula}";
    private string _problemDefinition = "SAT, or the Boolean satisfiability problem, is a problem that asks for a list of assignments to the literals of phi to result in 'True'";
    private string _source = ".";
    private string[] _contributors = { "Daniel Igbokwe" };

    private string _defaultInstance = "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1) & (!x3 | x4 | !x2 | x1) & (!x4 | !x1) & (x4 | x3 | !x1)";
    private string _instance = string.Empty;

    private string _wikiName = "";
    private List<List<string>> _clauses = new List<List<string>>();
    private List<string> _literals = new List<string>();
   
    private SATBruteForceSolver _defaultSolver = new SATBruteForceSolver();
    private SATVerifier _defaultVerifier = new SATVerifier();

    #endregion


    #region Properties
    // --- Properties ---
    public string problemName {
        get {
            return _problemName;
        }
    }


    public string formalDefinition {
        get {
            return _formalDefinition;
        }
    }

    public string problemDefinition {
        get {
            return _problemDefinition;
        }
    }

    public string source {
        get {
            return _source;
        }
    }

    public string[] contributors{
        get{
            return _contributors;
        }
    }
    public string defaultInstance {
        get {
            return _defaultInstance;
        }
    }

    public string wikiName {
        get {
            return _wikiName;
        }
    }


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


    public String instance  {
        get{
            return _instance ;
        }

        set {
            _instance  = value;
        }
    }

    public SATVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }

      public SATBruteForceSolver defaultSolver {
        get {
            return _defaultSolver;
        }
    }


    #endregion

    #region Constructors
    // --- Methods Including Constructors ---
    public SAT() {
        _instance = defaultInstance;
         clauses = getClauses(_instance);
        literals = getLiterals(_instance);
      
    }
    public SAT(string phiInput) {
        _instance = phiInput;
         clauses = getClauses(phiInput);
        literals = getLiterals(phiInput);
     
    }

    #endregion


    #region Methods

    public void ParseProblem(string phiInput) {
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
        string[] rawClauses = strippedInput.Split('|');

        foreach(string clause in rawClauses) {
            string[] rawLiterals = clause.Split('&');

            foreach(string literal in rawLiterals) {
                literals.Add(literal);
            }
        }
        return literals;
    }

    #endregion
        
}

