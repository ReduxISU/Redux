using API.Interfaces;
using API.Problems.NPComplete.NPC_DM3;

namespace API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;

class GareyJohnson : IReduction<SAT3, DM3> {

    // --- Fields ---
    private string _reductionName = " Garey & Johnson Reduction";
    private string _reductionDefinition = "Garey and Johnson Reduction converts 3SAT to a set of elements, and constraints of a 3-dimensional matching problem. The varibles are represented by wheels of 2 constraints for each clause a variable is in. The clauses are each mapped to a group of contraints all sharing two elements, with the third attaching to a varible gadget. Garbage collection gadgets are than created as constrains that assure any unincluded elements outside of the clause gadget are included in a matching.";
    private string _source = "Garey, M. R. and David S. Johnson. “Computers and Intractability: A Guide to the Theory of NP-Completeness.” (1978).";
    private string[] _contributors = { "Caleb Eardley"};
    private Dictionary<Object,Object> _gadgetMap = new Dictionary<Object,Object>();

    private SAT3 _reductionFrom;
    private DM3 _reductionTo;


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
    public DM3 reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public GareyJohnson(SAT3 from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    /***************************************************
     * reduce() called after GareyAndJohnsonReduction reduction, and returns a THREE_DM object, that
     * is a reduction from the SAT3 object passed into GareyAndJohnsonReduction.
     */
    public DM3 reduce() {
        SAT3 SAT3Instance = _reductionFrom;
        DM3 reduced3DM = new DM3();
        
        List<string> X = new List<string>();
        List<string> Y = new List<string>();
        List<string> Z = new List<string>();
        List<List<string>> M = new List<List<string>>();
        string instance = "";

        List<string> variables = new List<string>();

        //Creates a list of variable from the list of literals- may need updated if SAT3 is changed to
        //include a variable list.
        foreach(var l in SAT3Instance.literals){
            if(!variables.Contains(l.Replace("!", string.Empty))){
                variables.Add(l.Replace("!", string.Empty));
            }
        }
        // variable gadget
        foreach(var literal in variables) {
            int count = SAT3Instance.literals.Count(x => x.Replace("!", string.Empty) == literal);
            for(int i = 0; i < count; i++) {
                X.Add("x_" + literal + "_" + i.ToString());
                Y.Add("y_" + literal + "_" + i.ToString());
                Z.Add("z_" + literal + "_" + i.ToString());
                M.Add(new List<string>{X[X.Count - 1],Y[Y.Count - 1], Z[Z.Count - 1]});
                Z.Add("z_" + "!" + literal + "_" + i.ToString());
                M.Add(new List<string>{X[X.Count - 1],"y_" + literal + "_" + ((i + 2) % count).ToString(), Z[Z.Count - 1]});
            }
        }
        // clause gadget
        List<string> unusedLiterals = new List<string>(Z);
        for(int i = 0; i < SAT3Instance.clauses.Count; i++) {
            foreach(var literal in SAT3Instance.clauses[i]) {
                string found = unusedLiterals.Find(x => x.Contains("z_" + literal));
                M.Add(new List<string>{"x_clause_" + i.ToString(), "y_clause" + i.ToString(), found});
                unusedLiterals.Remove(found);
            }
            X.Add("x_clause_" + i.ToString());
            Y.Add("y_clause" + i.ToString());
        }
        // gaebage gadget
        for(int i = 0; i < SAT3Instance.literals.Count() - SAT3Instance.clauses.Count(); i++) {
            foreach(var j in Z) {
                M.Add(new List<string>{"x_garb_" + i.ToString(),"y_garb_" + i.ToString(), j});
            }
            X.Add("x_garb_" + i.ToString());
            Y.Add("y_garb_" + i.ToString());
        }

        foreach(var i in M) {
            instance += "{";
            foreach(var j in i) {
                instance += j + ",";
            }
            instance = instance.TrimEnd(',') + "}";
        }
        
        reduced3DM.X = X;
        reduced3DM.Y = Y;
        reduced3DM.Z = Z;
        reduced3DM.M = M;
        reduced3DM.instance = instance;

        //return new THREE_DM();
        return reduced3DM;
    }

    public string mapSolutions(SAT3 problemFrom, DM3 problemTo, string problemFromSolution){
        if(!problemFrom.defaultVerifier.verify(problemFrom,problemFromSolution)){
            return "Solution is inccorect";
        }

        //Parse out given solution
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
        List<string> inverseGC = new List<string>();
        foreach(string literal in problemFrom.literals){
            if(!variables.Contains(literal.Replace("!",""))){
                variables.Add(literal.Replace("!",""));
            }
        }

        // mapping of solution to variable gadgets
        foreach(string variable in variables){
            if(solutionList.Contains(variable)){
                for(int i=0; i<problemFrom.clauses.Count; i++){
                    mappedSolutionList.Add(string.Format("{{a[{0}][{1}],b[{0}][{1}],[!{0}][{1}]}}",variable,i+1));
                    inverseGC.Add(string.Format("[!{0}][{1}]",variable,i+1));
                }
            }
            else {
                for(int i=0; i<problemFrom.clauses.Count; i++){
                    mappedSolutionList.Add(string.Format("{{a[{0}][{1}],b[{0}][{2}],[{0}][{2}]}}",variable,((i+1)%problemFrom.clauses.Count)+1,i+1));
                    inverseGC.Add(string.Format("[{0}][{1}]",variable,i+1));
                }
            }
        }

        // mapping solution to clause gadgets
        for(int i=0; i<problemFrom.clauses.Count; i++){
            foreach(string variable in solutionList){
                if (problemFrom.clauses[1].Contains(variable)){
                    mappedSolutionList.Add(string.Format("{{s1[{0}],s2[{0}],[{1}][{0}]}}",i+1,variable));
                    inverseGC.Add(string.Format("[{0}][{1}]",variable,i+1));
                    break;
                }
            }
        }

        // mapping solution to garbage collection gadgets
        List<string> garbage = new List<string>();
        for(int i=0; i<problemFrom.clauses.Count; i++){
            foreach(string variable in variables){
                string vTrue = string.Format("[{0}][{1}]",variable,i+1);
                if(!inverseGC.Contains(vTrue)){
                    garbage.Add(vTrue);
                }
                string vFalse = string.Format("[!{0}][{1}]",variable,i+1);
                if(!inverseGC.Contains(vFalse)){
                    garbage.Add(vFalse);
                }
            }
        }
        for(int i=0; i<garbage.Count; i++){
            mappedSolutionList.Add(string.Format("{{g1[{0}],g2[{0}],{1}}}",i+1,garbage[i]));
        }

        //convert mappedSolutionList to one string
        string problemToSolution = "";
        foreach(string hyperEdge in mappedSolutionList){
            problemToSolution += hyperEdge + ',';
        }
        return '{' + problemToSolution.TrimEnd(',') + '}';
    }
}
