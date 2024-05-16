using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_KNAPSACK.Solvers;
using API.Problems.NPComplete.NPC_KNAPSACK.Verifiers;
using API.Tools.UtilCollection;

namespace API.Problems.NPComplete.NPC_KNAPSACK;

class KNAPSACK : IProblem<KnapsackBruteForce, KnapsackVerifier>{

    // --- Fields ---
    private string _problemName = "Knapsack (Binary)";

    private string _formalDefinition = "KNAPSACK = {<H, W, V> | H is a set of items (w,v) and there is a subset of items in H whose collective weight is less than or equal to W and whose collective value is equal or greater than V.}";

    private string _problemDefinition = "The 0-1 KNAPSACK decision problem is given a knapsack with a maximum capacity W and target value V and a set of n items x_1, x_2,... x_n with weights w_1,w_2,... w_n and values v_1,v_2,... v_n find the combination of singular items that provide greater than V value while staying under W. ";

    // How we want format
    private string _source = "";

    private string[] _contributors = { "Garret Stouffer", "Daniel Igbokwe"};
    
    private string _instance = string.Empty;


    private static string _defaultInstance = "({(10,60),(20,100),(30,120)},50,220)";
 

    private string _wikiName = "Knapsack";

    public UtilCollection items { get; set; }

    private int _W = 0;

    public int V { get; set; }


    private KnapsackBruteForce _defaultSolver = new KnapsackBruteForce();
    private KnapsackVerifier _defaultVerifier = new KnapsackVerifier();

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

    public string[] contributors{
        get{
            return _contributors;
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


    public int W {
        get {
            return _W;
        }
        set {
            _W = value;
        }
    }
    
    public KnapsackBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public KnapsackVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }


    // --- Methods Including Constructors ---
    public KNAPSACK() : this(_defaultInstance) {

    }

    public KNAPSACK(string HWVInput) {
        UtilCollection collection = new UtilCollection(HWVInput);
        instance = collection.ToString();
        collection.assertPair(3);
        items = collection[0];
        items.assertUnordered();
        W = int.Parse(collection[1].ToString());
        V = int.Parse(collection[2].ToString());
        foreach (UtilCollection item in items) item.assertPair();
    }
}