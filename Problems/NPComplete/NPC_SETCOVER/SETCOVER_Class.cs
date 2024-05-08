using API.Interfaces;
using API.Problems.NPComplete.NPC_SETCOVER.Solvers;
using API.Problems.NPComplete.NPC_SETCOVER.Verifiers;

namespace API.Problems.NPComplete.NPC_SETCOVER;

class SETCOVER : IProblem<SetCoverBruteForce,SetCoverVerifier> {

    // --- Fields ---
    private string _problemName = "Set Cover";
    private string _formalDefinition = "Sub Cover = {<S,T,k> | S is a set of elements, and there exists a grouping of k T subsetse equal to S}";
    private string _problemDefinition = "Given a set of elements and a collection S of m sets whose union equals the universe, the set cover problem is to identify the smallest sub-collection of S whose union equals the universe";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";

    private string _defaultInstance = "{{1,2,3,4,5},{{1,2,3},{2,4},{3,4},{4,5}},3}";
    private string _instance = string.Empty;
    private List<string> _universal = new List<string>();
    private List<List<string>> _subsets = new List<List<string>>();

    private string _wikiName = "";
    private int _K = 3;
    private SetCoverBruteForce _defaultSolver = new SetCoverBruteForce();
    private SetCoverVerifier _defaultVerifier = new SetCoverVerifier();
    private string[] _contributors = { "Andrija Sevaljevic" };

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
    public string instance {
        get {
            return _instance;
        }
        set {
            _instance = value;
        }
    }
    public string wikiName {
        get {
            return _wikiName;
        }
    }

    public List<string> universal {
        get {
            return _universal;
        }
        set {
            _universal = value;
        }
    }

    public List<List<string>> subsets {
        get {
            return _subsets;
        }
        set {
            _subsets = value;
        }
    }

    public int K {
        get {
            return _K;
        }
        set {
            _K = value;
        }
    }
    public SetCoverBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public SetCoverVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }


    // --- Methods Including Constructors ---
    public SETCOVER() {
        _instance = defaultInstance;
        universal = getUniversalSet(_instance);
        subsets = getSubsets(_instance);
        _K = getK(_instance);
    }
    public SETCOVER(string GInput) {
        _instance = GInput;
        universal = getUniversalSet(_instance);
        subsets = getSubsets(_instance);
        _K = getK(_instance);
    }


    public List<string> getUniversalSet(string Ginput) {

        List<string> allElements = new List<string>();
        List<string> seperation = Ginput.Split("},{{").ToList();
        string sections = seperation[0];
        allElements = sections.Replace("{","").Split(',').ToList();

        return allElements;
    }
    public List<List<string>> getSubsets(string Ginput) {
        List<string> allSets = new List<string>();
        List<string> seperation = Ginput.Split("},{{").ToList();
        string sections = seperation[1];
        List<string> seperation2 = sections.Split("}},").ToList();
        string sections2 = seperation2[0];
        allSets = sections2.Split("},{").ToList();

        List<List<string>> subsets = new List<List<string>>();
        foreach(var i in allSets) {
            subsets.Add(i.Split(',').ToList());
        }

        return subsets;      
    }

    public int getK(string Ginput) {
        List<string> sections = Ginput.Split("},{{").ToList();
        sections = sections[1].Split("}},").ToList();
        return Int32.Parse(sections[1].Replace("}",""));
    }


}