using API.Interfaces;
using API.DummyClasses;
using API.Problems.NPComplete.NPC_JOBSEQ.Solvers;
using API.Problems.NPComplete.NPC_JOBSEQ.Verifiers;
using SPADE;

namespace API.Problems.NPComplete.NPC_JOBSEQ;

class JOBSEQ : IProblem<JobSeqBruteForce,JobSeqVerifier, DummyVisualization> {

    // --- Fields ---
    public string problemName {get;} = "Job Sequencing";
    public string problemLink { get; } = "https://en.wikipedia.org/wiki/Optimal_job_scheduling";
    public string formalDefinition {get;} = "JobSeq = <T, D, P, K> is a vecter T of execution times, vector D of deadlines, vector P of penalties, and integer k where there exists a permutation pi of {1,2,3...,p} such that the sum of the penalties of every job that was not finished before the deadline is less than equal to k.";
    public string problemDefinition {get;} = "Job sequencing is the task of deciding in what order to do a series of jobs. Each job has a length of time it takes, a deadline, and a penalty that is applied if the deadline is missed. The task is to find an ordering of the jobs that results in a penalty that is less than k.";
    public string source {get;} = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors {get;} = {"Russell Phillips"};

    public static string _defaultInstance { get; } = "((4,2,5,9,4,3),(9,13,2,17,21,16),(1,4,3,2,5,8),4)";
    public string defaultInstance { get; } = _defaultInstance;
    public string instance {get;set;} = string.Empty;
    private List<int> _T = new List<int>();
    private List<int> _D = new List<int>();
    private List<int> _P = new List<int>();
    private int _K;

    

    public string wikiName {get;} = "";
    public JobSeqBruteForce defaultSolver {get;} = new JobSeqBruteForce();
    public JobSeqVerifier defaultVerifier { get; } = new JobSeqVerifier();
    public DummyVisualization defaultVisualization { get; } = new DummyVisualization();

    // --- Properties ---
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

    // --- Methods Including Constructors ---
    public JOBSEQ() : this(_defaultInstance) {

    }
    public JOBSEQ(string input) {
        instance = input;

        StringParser jobSeq = new("{(T,D,P,K) | T is list, D is list, P is list, k is int}");
        jobSeq.parse(input);
        _T = jobSeq["T"].ToList().Select(node => Int32.Parse(node.ToString())).ToList();
        _D = jobSeq["D"].ToList().Select(node => Int32.Parse(node.ToString())).ToList();
        _P = jobSeq["P"].ToList().Select(node => Int32.Parse(node.ToString())).ToList();
        _K = int.Parse(jobSeq["K"].ToString());
    }
}