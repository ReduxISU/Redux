using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Problems.NPComplete.NPC_SAT;
using API.Problems.NPComplete.NPC_SAT.Solvers;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_SAT.Visualizers;

class SATGroverVisualization : IVisualization<SAT>
{
    public string visualizationName { get; } = "SAT Quantum Solver";
    public string visualizationDefinition { get; } = "This visualization builds a quantum circuit from the Boolean expression and then uses Grover's algorithm to find a bit string x such that f(x) = 1.";
    public string source { get; } = "Brassard, G., Hoyer, P., Mosca, M., & Tapp, A. (2000), Quantum Amplitude Amplification and Estimation";
    public string[] contributors { get; } = { "Jason L. Wright", "Alex Svancara" };
    public string visualizationType { get; } = "Quantum Circuit";

    // --- Methods Including Constructors ---
    public SATGroverVisualization()
    {
    }
    public API_JSON visualize(SAT instance)
    {
        var qc = new API_QUANTUMCIRCUIT();
        var solvers = new SATGroverSolver();
        qc.solution = solvers.solve(instance);
        qc.circuit = instance.circuit;
        return qc;
    }
}
