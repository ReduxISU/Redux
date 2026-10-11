using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SIMON.Solvers;

namespace API.Problems.NPComplete.NPC_SIMON.Visualizations;

/// <summary>
/// Simon's Problem as the Quantum Circuit picture (format=frames): Simon's circuit for the table, and the chosen solver's
/// steps. Its list format is the quantum circuit payload with an empty QASM string, as Unstructured Search's is: no quantum
/// server endpoint builds Simon's circuit. The problem's default visualization is still <c>DummyVisualization</c>.
/// </summary>
class SimonVisualization : IVisualization<SIMON, API_QUANTUMCIRCUIT>, IQuantumCircuitVisualization<SIMON> {
    public string visualizationName { get; } = "Simon's Problem Quantum Circuit";
    public string visualizationDefinition { get; } = "Draws Simon's circuit for the table: Hadamards on the query qubits, one call to the oracle writing f(x) into the output qubits, Hadamards again, and a measurement whose every outcome y satisfies y · s = 0.";
    public string source { get; } = "Simon, Daniel R. On the power of quantum computation. SIAM journal on computing, 1997, 26. Jg., Nr. 5, S. 1474-1483.";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Michael Trosper" };
    public VisualizationType visualizationType { get; } = VisualizationType.QuantumCircuitQjs;
    public ISolver solver { get; } = new SimonSolver();

    // The Quantum Circuit picture (format=frames only). Implemented explicitly so it is not part of /info.
    Type? IVisualization.StepShape => typeof(QuantumProgress);
    QuantumCircuitSpec IQuantumCircuitVisualization<SIMON>.Describe(SIMON problem) => QuantumCircuits.Simon(problem.funcValues);

    // --- Methods Including Constructors ---
    public SimonVisualization() {
    }

    public API_QUANTUMCIRCUIT visualize(SIMON instance) {
        return new API_QUANTUMCIRCUIT {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = ""
        };
    }

    public API_JSON SolvedVisualization(SIMON instance, string solution) {
        return new API_QUANTUMCIRCUIT {
            format = QuantumCircuitFormat.QASM,
            qasm = "",
            solution = solution
        };
    }
}
