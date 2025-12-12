using API.Interfaces;

namespace API.Problems.NPComplete.NPC_UNSTRUCTUREDSEARCH.Solvers;
class UnstructuredSearchSolver : ISolver<UNSTRUCTUREDSEARCH> {

    // --- Fields ---
    public string solverName { get; } = "Clasical unstructured search solver";
    public string solverDefinition {get;} = "TODO";
    public string source {get;} = "TODO";
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };

    // --- Methods Including Constructors ---
    public UnstructuredSearchSolver() {}

    public string solve(UNSTRUCTUREDSEARCH problem){
        // For a classical solution, we just need to loop over the values and
        // find the first non-zero value.
        foreach (var bit in problem.funcValues)
        {
            Console.WriteLine($"boop={bit}");
        }

        for (int i = 0; i < problem.funcValues[i]; i++)
        {
            if (problem.funcValues[i] != 0)
                return Convert.ToString(i);
        }
        return "no solution";
    }
}
