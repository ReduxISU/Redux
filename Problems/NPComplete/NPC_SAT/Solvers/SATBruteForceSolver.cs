using API.Interfaces;
using API.Problems.NPComplete.NPC_SAT;

using API.Tools.Boolean_Parser; 

namespace API.Problems.NPComplete.NPC_SAT.Solvers;
#pragma warning disable CS1591


    public class SATBruteForceSolver : ISolver {


    #region Fields

    // --- Fields ---
    private string _solverName = "SAT Brute Force Solver";
    private string _solverDefinition = "This is a simple brute force solver for SAT";
    private string _source = "";
    private string[] _contributors = { "Daniel Igbokwe, Show Pratoomratana"};

    private string _complexity = "";


    #endregion


    #region Properties
   // --- Properties ---
    public string solverName {
        get {
            return _solverName;
        }
    }
    public string solverDefinition {
        get {
            return _solverDefinition;
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
    public string complexity {
        get {
            return _complexity;
        }

        set{
            _complexity = value;
        }
    }

    #endregion

    #region Constructors
    // --- Methods Including Constructors ---
    public SATBruteForceSolver() {

    }
    #endregion


    #region Methods 
    
    // We can think of a SAT problem as 0 as false and 1 as true. To cycle through every possible combination
    // we can think of it as counting in binary as in 01 = FT, 11 = TT, 00 = FF.
    // This makes it simple to try every combination of T/F pairs in a SAT problem by simply converting it to binary adding +1 and converting back.
    public Dictionary<string, Boolean> increment(Dictionary<string, Boolean> dict){
        string binaryConv = "";
        List<bool> values = dict.Values.ToList();
        // Converting the current T/F assigment to binary
        foreach(bool currentValue in values){
            if (currentValue == false){
                binaryConv = binaryConv + "0"; 
            }
            else {
                binaryConv = binaryConv + "1"; 
            }
        }
        // Converting the string to binary and adding 1.
        int originalBinNumber = Convert.ToInt32(binaryConv, 2);
        int one = Convert.ToInt32("1", 2);
        string incrementedNumber = Convert.ToString(originalBinNumber + one, 2);

        // Adding back the 0s that proceed the number.
        for (int i = binaryConv.Length; incrementedNumber.Length < binaryConv.Length; i++){
            incrementedNumber = "0" + incrementedNumber;
        }

        // Making a new dictionary and converting the binary back into a T/F assigment.
        List<string> originalDictKey = dict.Keys.ToList();
        Dictionary<string, Boolean> convertedDict = new Dictionary<string, bool>(); 
        for(int i = 0; i < binaryConv.Length; i++){
            char intBool = incrementedNumber[i];
            convertedDict.Add(originalDictKey[i], charToBool(intBool));
        }
        return convertedDict;
    }

    // Just a quick char to binary converter. 
    private bool charToBool(char character){
        string zero = "0"; // Bit of weird casting here, but it works.
        if (character == zero.ToCharArray()[0]) {return false;}
        return true;
    }
    // Evalutes a clause, given the True/False value of each literal and it's single clause it's within.
    public Boolean evaluate(Dictionary<string, Boolean> mapping, List<string> clause){
        List<Boolean> booleanClause = new List<bool>();
        
        foreach (string literal in clause){
            // If there is a !(Not) in front of the literal. Remove it(to match it's Dictionary value) then invert the boolean and add it to the list
            if (literal.Contains("!")){
                booleanClause.Add(!mapping[literal.Replace("!", "")]);
            }
            // Otherwise just add the boolean to the list
            else { 
                booleanClause.Add(mapping[literal]);
            }
        }

        // Iterate through our boolean list, if any are true the clause is satisfied.
        foreach(bool currentBool in booleanClause){
            if (currentBool == true){
                return true;
            }
        }
        // All literals in the clause evaluated to false. Thus the whole clause is false.
        return false;
    }

        public string Solver(string SATInstance){
        //string SATInstance = "(!x1 | !x2 | !x3) & (!x1 | x3 | x1) & (x2 | !x3 | x1)";
        Boolean_Parser parser = new Boolean_Parser(SATInstance);
        List<string> literals = parser.getLiterals();
        // Removing all ! from the literals and removing duplicates for the dictionary
        for(int i = 0; i < literals.Count; i++){
            literals[i] = literals[i].Replace("!", "");
        }
        literals = literals.Distinct().ToList();

        // Adding all the literals to a dictionary with their starting value as false
        Dictionary<string, Boolean> literalDict = new Dictionary<string, bool>();
        foreach(string literal in literals){    
            literalDict.Add(literal, false);
        }

        List<List<string>> clause = parser.getClause();

        // Loop through all combinations. The total number of binary choices you can make is 2^(number of items). E.G. 3 variables is 2^3.
        for (int currentCombination = 0; currentCombination < Math.Pow(2, literals.Count); currentCombination++){
            int trueClauses = 0;
                foreach (List<string> currentClause in clause){
                    // change the T/F values of the literals. Starts with at least 1 being true by incrementing at the start.
                    literalDict = increment(literalDict); 
                    bool currentEvaluation = evaluate(literalDict, currentClause);
                    
                    if (currentEvaluation == false){
                        break;// A clause is false, so the whole SAT is false.
                    }

                    trueClauses++; // All clauses are true. We found a valid SAT solution
                    if (clause.Count == trueClauses){
                        string solutionString = "(";
                        foreach (KeyValuePair<string, bool> pair in literalDict){
                        
                            solutionString += string.Format("{0}:{1},", pair.Key, pair.Value);
                        }
                        solutionString += ")";
                        // Just getting rid of the extra comma at the end.
                        solutionString = solutionString.Remove(solutionString.Length - 2, 1);
                        return solutionString;
                    }
                }
                        //literalDict = increment(literalDict); // Incrementing here starts with the formula being all false.
        }
            return "No solution exists";
        }


    #endregion
        
    }
