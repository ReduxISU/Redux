using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SUDOKU;
using API.Problems.NPComplete.NPC_SUDOKU.Solvers;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_SUDOKU.Visualizations;

class SudokuVisualization : IVisualization<SUDOKU>
{
    public string visualizationName { get; } = "TODO";
    public string visualizationDefinition { get; } = "TODO";
    public string source { get; } = "TODO";
    public string[] contributors { get; } = { "Eric Hill" };
    public string visualizationType { get; } = "TODO";

    // --- Methods Including Constructors ---
    public SudokuVisualization()
    {
    }
    public API_JSON visualize(SUDOKU instance)
    {
        var qc = new API_QUANTUMCIRCUIT();
        // var solvers = new UnstructuredGroverSolver();
        // qc.solution = solvers.solve(instance);
        // qc.circuit = instance.circuit;
        // return qc;
        return qc;
    }
}
