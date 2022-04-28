using API.Interfaces;
using API.Problems.NPComplete.NPC_DM3.Solvers;
using API.Problems.NPComplete.NPC_DM3.Verifiers;
using System.Collections;

namespace API.Problems.NPComplete.NPC_DM3;

class DM3 : IProblem<HurkensShrijver,GenericVerifierDM3> {

    // --- Fields ---
    private string _problemName = "3-Dimensional Matching";
    private string _formalDefinition = "<M,X,Y,Z> | M is a subset of X*Y*Z,|X|=|Y|=|Z| and a subset of M, M', exists, where |M'| = |A|,|B|,|C|, and no two elements of M' agree in any cooridinate" ;
    private string _problemDefinition = "The 3-DImensional Matching problem, is when, given 3 equally sived sets, X, Y, and Z, and a set of constraints M, which is a subset of XxYxZ, are you able to create a set of 3-tuples, which contains each element of X, Y, and Z in one and only one 3-tuple, while following the constraints M. ";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string _defaultInstance = "{x1,x2,x3,x4}{y1,y2,y3,y4}{z1,z2,z3,z4}{x1,y2,z1}{x1,y2,z4}{x2,y1,z1}{x2,y1,z2}{x2,y2,z1}{x2,y2,z4}{x2,y4,z3}{x3,y3,z2}{x3,y3,z3}{x4,y1,z1}{x4,y1,z2}"; // simply a list of sets with the elements divided by commas, the first three are asumed to be X, Y, and Z, and all subsequent sets are sets in M
    private string _G = string.Empty;
    private List<List<List<string>>> _problem;
    private List<string> _X;
    private List<string> _Y;
    private List<string> _Z;
    private List<List<string>> _M;
    private HurkensShrijver _defaultSolver = new HurkensShrijver();
    private GenericVerifierDM3 _defaultVerifier = new GenericVerifierDM3();

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
    public string defaultInstance {
        get {
            return _defaultInstance;
        }
    }
    public string G {
        get {
            return _G;
        }
        set {
            _G = value;
        }
    }
    public List<string> X {
        get {
            return _X;
        }
        set {
            _X = value;
            _problem[0][0]=_X;
        }
    }
    public List<string> Y {
        get {
            return _Y;
        }
        set {
            _Y = value;
            _problem[0][1]=_Y;
        }
    }
    public List<string> Z {
        get {
            return _Z;
        }
        set {
            _Z = value;
            _problem[0][2]=_Z;
        }
    }
    public List<List<string>> M {
        get {
            return _M;
        }
        set {
            _M = value;
            _problem[1]=_M;
        }
    }
    public List<List<List<string>>> problem {
        get {
            return _problem;
        }
    }
    public HurkensShrijver defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public GenericVerifierDM3 defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }


    // --- Methods Including Constructors ---
    public DM3() {
        _G = defaultInstance;
        _problem = ParseProblem(_G);
        _X = _problem[0][0];
        _Y = _problem[0][1];
        _Z = _problem[0][2];
        _M = _problem[1];
    }
    public DM3(string GInput) {
        _G = GInput;
        _problem = ParseProblem(_G);
        _X = _problem[0][0];
        _Y = _problem[0][1];
        _Z = _problem[0][2];
        _M = _problem[1];
    }

/*************************************************
parseSet(List<string> Set,string GInput,int start), is meant to take one set inside of a string, and put it into an array
it is refferenced in ParseProblem, The Set should be an empty List<string>, created by ParseProblem, GInput should be 
input of PareseProblem, and start should be the index of the '{' at the begining of the set in the string. It works by
iterating through each charecter from the start index, until it reaches the end of the set '}'. and crestes string of anything
between ','s excluding spaces, and places those strings inside Set.
**************************************************/
    private void parseSet(List<string> Set,string GInput,int start){
        int i = start + 1;
        string temp = "";
        while(GInput[i]!= '}'){
            if(GInput[i] == ','){
                if(temp != ""){Set.Add(temp);}
                temp = "";
                i++;
            }
            else if(GInput[i] != ' '){
                temp += GInput[i];
                i++;
            }
            else{i++;}
        }
        Set.Add(temp);
        return;
    }
    /*************************************************
   ParseProblem(string GInput) takes the string representation of the 3-Dimensional Matching problem, and returns a 
   3 dimensional list, the first depths of list, contains two lists, one with the sets X,Y,and Z, and the other containing all the sets in M.
   ***************************************************/
    public List<List<List<string>>> ParseProblem(string GInput) {
        List<List<List<string>>> Problem = new List<List<List<string>>>(){new List<List<string>>(), new List<List<string>>()};
        int setIndex = 0;
        for(int i = 0; i< GInput.Length; i++){
            if(GInput[i] == '{'){ // at each occurence of {parseSet is called to put each element in the set, divided by commas, into the Problem list.
                List<string> Set = new List<string>();
                parseSet(Set,GInput,i);
                Problem[setIndex].Add(Set);
            }
            if(Problem[0].Count == 3){setIndex = 1;}
        }
        return Problem;
    }
}

