using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUDOKU.Solvers;
class SudokuSolver : ISolver<SUDOKU> {

    // --- Fields ---
    public string solverName { get; } = "TODO";
    public string solverDefinition { get; } = "TODO";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Eric Hill" };

    // --- Methods Including Constructors ---
    public SudokuSolver() {}

    public string solve(SUDOKU problem){
        // For a classical solution, we just need to loop over the values and
        // find the first non-zero value.
        // foreach (var bit in problem.funcValues)
        // {
        //     Console.WriteLine($"boop={bit}");
        // }

        // for (int i = 0; i < problem.funcValues[i]; i++)
        // {
        //     if (problem.funcValues[i] != 0)
        //         return Convert.ToString(i);
        // }
        // return "no solution";
        return "TODO";
    }
}
