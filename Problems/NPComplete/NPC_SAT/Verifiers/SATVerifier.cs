using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT.Verifiers;

    class SATVerifier : IVerifier {

    #region Fields
    private string _verifierName = "SAT Verifier";
    private string _verifierDefinition = "This is a verifier for SAT";
    private string _source = " ";
    private string[] _contributors = { "Daniel Igbokwe, Show Pratoomratana"};

    private string _complexity = " ";

    private string _certificate = "(x1:True)";
    
    #endregion

    #region Properties

    // --- Properties ---
    public string verifierName
    {
        get
        {
            return _verifierName;
        }
    }
    public string verifierDefinition
    {
        get
        {
            return _verifierDefinition;
        }
    }
    public string source {
        get
        {
            return _source;
        }
    }
      public string[] contributors{
        get{
            return _contributors;
        }
    }
    public string complexity {
        get {
            return _complexity;
        }

        set{
            _complexity = value;
        }
    }

      public string certificate {
        get {
            return _certificate;
        }
    }
    


    #endregion 

    #region Constructors

    // --- Methods Including Constructors ---
    public SATVerifier() {
        
    }

    // Identical verifier for the 3SAT for KadensSimpleVerifier. Works for any SAT instance not just 3SAT.
    public Boolean verify(SAT problem, string userInput){
        List<List<string>> clauses = problem.clauses;
        
        string strippedInput = userInput.Replace(" ", "").Replace("(", "").Replace(")","");

        string[] assignments = strippedInput.Split(',');
        List<string> trueLiterals = new List<string>();

        foreach (string assignment in assignments) {
            string[] assignmentParts = assignment.Split(':');
            if (assignmentParts.Length <= 1){
                assignmentParts = assignment.Split('=');
            }
            string literalName = assignmentParts[0];
            string TF = assignmentParts[1];

            if (TF == "True" | TF == "T") {
                trueLiterals.Add(literalName);
            }
            else if (TF == "False" | TF == "F") {
                string inverseLiteralName = "!" + literalName; 
                trueLiterals.Add(inverseLiteralName);
            }
        }

        for(int i = 0; i < clauses.Count; i++) {
            bool containedInClause = false;

            foreach (string literal in trueLiterals) {
                if (clauses[i].Contains(literal)) {
                    containedInClause = true;
                }
            }

            if (containedInClause == false) {
                return false;
            }
            
        }

        return true;
    }

    #endregion
    }
