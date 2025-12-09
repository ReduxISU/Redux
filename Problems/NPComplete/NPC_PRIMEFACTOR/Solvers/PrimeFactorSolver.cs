using API.Interfaces;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;
class PrimeFactorSolver : ISolver<PRIMEFACTOR> {

    // --- Fields ---
    public string solverName {get;} = "ProblemSolver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "https://doi.org/10.1137/S0036144598347011"; // A bone for the solutions team! ;)
    public string[] contributors {get;} = { "Paul Gilbreath", "Alex Svancara" };

    // --- Methods Including Constructors ---
    public PrimeFactorSolver() {}

    public string solve(PRIMEFACTOR problem){
        // TODO: implement {SOLVER} for {PROBLEM}
        return "{}";
    }
}
