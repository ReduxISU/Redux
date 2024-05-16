using API.Interfaces;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;
using API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;

namespace API.Problems.NPComplete.NPC_JOBSEQ;

class JOBSEQ : IProblem<JobSeqBruteForce,JobSeqVerifier> {

    // --- Fields ---
    private string _problemName = "Job Sequencing";
    private string _formalDefinition = "JobSeq = <T, D, P, K> is a vecter T of execution times, vector D of deadlines, vector P of penalties, and integer k where there exists a permutation pi of {1,2,3...,p} such that the sum of the penalties of every job that was not finished before the deadline is less than equal to k.";
    private string _problemDefinition = "Job sequencing is the task of deciding in what order to do a series of jobs. Each job has a length of time it takes, a deadline, and a penalty that is applied if the deadline is missed. The task is to find an ordering of the jobs that results in a penalty that is less than k.";
    private string _source = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    private string[] _contributors = {"Russell Phillips"};



    private string _defaultInstance = "((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)";

    private string _instance = string.Empty;
    private List<int> _T = new List<int>();
    private List<int> _D = new List<int>();
    private List<int> _P = new List<int>();
    private int _K;

    

    private string _wikiName = "";
    private JobSeqBruteForce _defaultSolver = new JobSeqBruteForce();
    private JobSeqVerifier _defaultVerifier = new JobSeqVerifier();

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

    public string[] contributors{
        get{
            return _contributors;
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

    public string wikiName {
        get {
            return _wikiName;
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
    public List<int> T {
        get {
            return _T;
        }
        set {
            _T = value;
        }
    }

    public List<int> D {
        get {
            return _D;
        }
        set {
            _D = value;
        }
    }

    public List<int> P {
        get {
            return _P;
        }
        set {
            _P = value;
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
    
    public JobSeqBruteForce defaultSolver {
        get {
            return _defaultSolver;
        }
    }
    public JobSeqVerifier defaultVerifier {
        get {
            return _defaultVerifier;
        }
    }
    // --- Methods Including Constructors ---
    public JOBSEQ() {
        _instance = defaultInstance;
        T = getT(_instance);
        D = getD(_instance);
        P = getP(_instance);
        K = getK(_instance);
    }
    public JOBSEQ(string instance) {
        _instance = instance;
        T = getT(_instance);
        D = getD(_instance);
        P = getP(_instance);
        K = getK(_instance);
    }
    private List<int> getT(string instance)
    {
        return instance.TrimStart('(')
                            .TrimStart('(')
                            .Split("),(")[0]
                            .Split(',')
                            .Select(int.Parse)
                            .ToList();
        
    }

    private List<int> getD(string instance)
    {
        return instance.TrimStart('(')
                            .TrimStart('(')
                            .Split("),(")[1]
                            .Split(',')
                            .Select(int.Parse)
                            .ToList();
    }

    private List<int> getP(string instance)
    {
        return instance.TrimStart('(')
                            .TrimStart('(')
                            .Split("),(")[2]
                            .Split("),")[0]
                            .Split(',')
                            .Select(int.Parse)
                            .ToList();
    }

    private int getK(string instance) {
        return Int32.Parse(instance.TrimStart('(')
                            .TrimStart('(')
                            .Split("),(")[2]
                            .Split("),")[1]
                            .TrimEnd(')'));
        
    }

}