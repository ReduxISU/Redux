using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;

namespace API.Problems.NPComplete.NPC_SAT.Verifiers;

    class SATVerifier : IVerifier<SAT> {

    #region Fields
    public string verifierName {get;} = "SAT Verifier";
    public string verifierDefinition {get;} = "This is a verifier for SAT";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Daniel Igbokwe, Show Pratoomratana"};

    private string _complexity = " ";

    private string _certificate = "(x1:True)";
    
    #endregion

    #region Properties

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
    public bool verify(SAT problem, string certificate){
        List<List<string>> clauses = problem.clauses;
        
        string strippedInput = certificate.Replace(" ", "").Replace("(", "").Replace(")","");

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
