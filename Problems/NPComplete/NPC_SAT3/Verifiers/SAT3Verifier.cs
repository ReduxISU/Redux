using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT3;

namespace API.Problems.NPComplete.NPC_SAT3.Verifiers;

class SAT3Verifier : IVerifier {

    // --- Fields ---
    private string _verifierName = "3SAT Verifier";
    private string _verifierDefinition = "This is a verifier for 3SAT. It takes the certificate from " + 
                                         "the user and validates that every clause contains a true literal";
    private string _source = " ";
    private string[] _contributors = { "Kaden Marchetti"};

    private string _certificate = "";
    
    // --- Properties ---
    public string verifierName {
        get {
            return _verifierName;
        }
    }
    public string verifierDefinition {
        get {
            return _verifierDefinition;
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
      public string certificate {
        get {
            return _certificate;
        }
    }




    

    // --- Methods Including Constructors ---
    public SAT3Verifier() {
        
    }

    // Take in a problem and a possible solution and evaluate it. Expected userInput follows the format (LiteralName = Assignement, LiteralName = Assignment, ...)
    // EXAMPLE: (x1 = False, x2 = True, x3 = False)
    // ONLY true literal names should be included in the user input seperated by commas
    public Boolean verify(SAT3 problem, string userInput) {

        // User input is effectively asking for the list of variables assigned to "True"
        List<List<string>> clauses = problem.clauses;
        string strippedInput = userInput.Replace(" ", "").Replace("(", "").Replace(")","");

        // Get user input and parse out true literals (including inverses)
        string[] assignments = strippedInput.Split(',');
        List<string> trueLiterals = new List<string>();

        // If True, just add literalName, if False, add literalName with ! prepending. Then add it to the trueLiterals list
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

        // Check if each clause is satisfied after some basic checks
        for(int i = 0; i < clauses.Count; i++) {
            bool containedInClause = false;

            foreach (string literal in trueLiterals) {
                // Check if any of the trueLiterals are in the clause
                if (clauses[i].Contains(literal)) {
                    containedInClause = true;
                }
            }

            // If ever a clause does not contain one of the true literals, return false
            if (containedInClause == false) {
                return false;
            }
            
        }

        // Backup just in case
        return true;
    }
}