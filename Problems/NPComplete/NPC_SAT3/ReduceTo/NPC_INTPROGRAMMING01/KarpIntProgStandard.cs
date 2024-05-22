using API.Interfaces;
using API.Problems.NPComplete.NPC_INTPROGRAMMING01;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_INTPROGRAMMING01;

class KarpIntProgStandard : IReduction<SAT3, INTPROGRAMMING01> {

    // --- Fields ---
    private string _reductionName = "Karp's Integer Programming Reduction";
    private string _reductionDefinition = "Karps reduction maps each clause of a SAT problem into a row in a integer programming matrix.";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = { "Caleb Eardley"};
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();

    private SAT3 _reductionFrom;
    private INTPROGRAMMING01 _reductionTo;


    // --- Properties ---
    public string reductionName {
        get {
            return _reductionName;
        }
    }
    public string reductionDefinition {
        get {
            return _reductionDefinition;
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
    public Dictionary<Object,Object> gadgetMap {
        get{
            return _gadgetMap;
        }
        set{
            _gadgetMap = value;
        }
    }
    public SAT3 reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public INTPROGRAMMING01 reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public KarpIntProgStandard(SAT3 from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public INTPROGRAMMING01 reduce() {
        SAT3 SAT3Instance = _reductionFrom;
        INTPROGRAMMING01 reduced01INT = new INTPROGRAMMING01();
        
        List<int> dVector = new List<int>();
        List<List<int>> Cmatrix = new List<List<int>>();

        //Creates a list of variable from the list of literals- may need updated if SAT3 is changed to
        //include a variable list.
        List<string> variables = new List<string>();
        foreach(var l in SAT3Instance.literals){
            if(!variables.Contains(l.Replace("!", string.Empty))){
                variables.Add(l.Replace("!", string.Empty));
            }
        }

        //Creates the rows or ineqlalities of the matrix C, for each clause in SAT3
        for(int i=0; i<SAT3Instance.clauses.Count; i++){
            List<int> row = new List<int>();
            dVector.Add(-1);
            for(int j=0; j<variables.Count; j++){
                if(SAT3Instance.clauses[i].Contains(variables[j]) && !SAT3Instance.clauses[i].Contains("!"+variables[j])){
                    row.Add(-1);
                }
                else if(!SAT3Instance.clauses[i].Contains(variables[j]) && SAT3Instance.clauses[i].Contains("!"+variables[j])){
                    row.Add(1);
                }
                else{row.Add(0);}
                //constructs dVector
                if(SAT3Instance.clauses[i].Contains("!"+variables[j])){
                    dVector[i] += 1;
                }
            }
            Cmatrix.Add(row);
        }
        string Cstring = string.Empty;
        string dstring = string.Empty;

        for(int i=0; i<Cmatrix.Count-1; i++){
            Cstring += "(";
            for(int j = 0; j<Cmatrix[i].Count; j++){
                Cstring += " "+Cmatrix[i][j]+" ";
            }
            Cstring += "),";
        }
        Cstring += "(";
        for(int i=0; i<Cmatrix[Cmatrix.Count-1].Count; i++){
            Cstring += " "+Cmatrix[Cmatrix.Count-1][i]+" ";
        }
        Cstring += ")";
 
        dstring += "(";
        for(int i=0; i<dVector.Count; i++){
            dstring += " "+dVector[i]+" ";
        }
        dstring += ")";

        string instance = Cstring +"<="+dstring;

        reduced01INT.C = Cmatrix;
        reduced01INT.d = dVector;
        reduced01INT.instance = instance;
        
        //K is the number of clauses
        
        reductionTo = reduced01INT;
        return reduced01INT;
    }

    public string mapSolutions(SAT3 problemFrom, INTPROGRAMMING01 problemTo, string problemFromSolution){
        // Check if the colution is correct
        if(!problemFrom.defaultVerifier.verify(problemFrom,problemFromSolution)){
            return "Solution is inccorect";
        }

        //Parse problemFromSolution into a list of nodes
        List<string> solutionList = problemFromSolution.Replace(" ","").Replace("(","").Replace(")","").Split(",").ToList();
        for(int i=0; i<solutionList.Count; i++){
            string[] tempSplit = solutionList[i].Split(":");
            if(tempSplit[1] == "False"){
                solutionList[i] = "!"+tempSplit[0];
            }
            else if(tempSplit[1] == "True"){
                solutionList[i] = tempSplit[0];
            }
            else{solutionList[i] = "";}
        }
        solutionList.RemoveAll(x => string.IsNullOrEmpty(x));

        //Map solution
        List<string> mappedSolutionList = new List<string>();
        List<string> variables = new List<string>();
        foreach(string literal in problemFrom.literals){
            if(!variables.Contains(literal.Replace("!",""))){
                variables.Add(literal.Replace("!",""));
            }
        }
        foreach(string variable in variables){
            if(solutionList.Contains(variable)){
                mappedSolutionList.Add("1");
            }
            else{
                mappedSolutionList.Add("0");
            }
        }
        string problemToSolution = "";
        foreach(string num in mappedSolutionList){
            problemToSolution += num + ' ';
        }
        return '(' + problemToSolution.TrimEnd(' ') + ')';
    }
}