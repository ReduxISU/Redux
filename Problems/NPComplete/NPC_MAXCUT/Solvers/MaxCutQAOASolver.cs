using API.Interfaces;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_MAXCUT.Solvers;

/// <summary>
/// Max Cut solver that uses an external quantum computing API which in turn uses the Quantum Approximate
/// Optimization Algorithm (QAOA) to find a heavy cut. QAOA is approximate: the cut it returns is not
/// guaranteed to be a maximum one, so its answer may not pass MaxCutVerifier.
/// </summary>
class MaxCutQAOASolver : ISolver<MAXCUT> {
    // --- Fields ---
    public string solverName { get; } = "Max Cut QAOA";
    public string solverDefinition { get; } =
        "Sends the weighted graph to the quantum computing API, which encodes the cut weight as an Ising cost " +
        "Hamiltonian and runs the Quantum Approximate Optimization Algorithm (QAOA): a classical optimizer tunes " +
        "the angles of a layered quantum circuit to minimize the Hamiltonian's expected energy, and the heaviest " +
        "cut among the optimized circuit's samples is returned as the S side. The answer is approximate, not " +
        "guaranteed to be a maximum cut.";
    public string source { get; } = "https://arxiv.org/abs/1411.4028";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service, and QAOA carries no
    // approximation-ratio guarantee at the fixed depth the service uses, so it is a heuristic.
    public SolverType solverType { get; } = SolverType.Quantum | SolverType.Heuristic;
    // Each QAOA layer applies one ZZ rotation per edge and one X rotation per node, and the service uses
    // a fixed 2 layers. The number of circuit evaluations its classical optimizer (COBYLA) makes has no
    // fixed bound, so this is the size of one circuit, not the total work.
    public MathematicalFunction timeComplexity { get; } = new("n + m", " gates per QAOA layer, n = |nodes|, m = |edges|");

    // --- Constructors ---
    public MaxCutQAOASolver() { }

    public class JSON_MaxCut_Problem {
        public List<string> nodes { get; set; }
        public List<object[]> edges { get; set; }
        public JSON_MaxCut_Problem(MAXCUT problem) {
            nodes = problem.nodes;
            edges = problem.edges.Select(e => new object[] { e.source, e.destination, e.weight }).ToList();
        }
    }

    // --- Methods ---

    // Returns the S side of the cut, {node,...}. On any failure of the quantum service, including a response
    // whose answer is not a valid S side, this throws SolverUnavailableException (HTTP 503 solver_unavailable),
    // never an error text in place of the answer (#625). The request is one blocking call, so the timer is
    // checked once, before it is made.
    public string solve(MAXCUT problem) {
        if (timerHasExpired) return SolveRun.TimedOutAnswer;

        var requestBody = new JSON_MaxCut_Problem(problem);

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            string response = client.PostAsync("/maxcut-quantum", requestBody).Result;

            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer", out JsonElement answerElement)
                && answerElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerElement.GetString())) {
                string answer = answerElement.GetString()!;
                // Anything but a non-empty proper subset of the nodes is not an answer: fail as a missing one does,
                // not pass it on (#625).
                if (!IsProperSubset(problem, answer))
                    throw new InvalidDataException($"its 'answer' is not a non-empty proper subset of the nodes: {response}");
                return answer;
            }

            // No answer in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer", response);
        });
    }

    private static bool IsProperSubset(MAXCUT problem, string answer) {
        string trimmed = answer.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}')) return false;
        List<string> S = trimmed[1..^1].Split(',').Select(s => s.Trim()).ToList();
        return S.All(problem.nodes.Contains) && S.Distinct().Count() == S.Count
            && S.Count > 0 && S.Count < problem.nodes.Count;
    }
}
