using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Tools;
using SPADE;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Solvers;

/// <summary>
/// Bernstein-Vazirani solver that uses an external quantum computing API.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
// Incremental: the circuit is applied one column at a time, and then read.
class BernsteinVaziraniQuantumSolver : ISolver<BERNSTEINVAZIRANI, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    public string solverName { get; } = "Bernstein-Vazirani Algorithm";
    public string solverDefinition { get; } = "Calls external quantum computing API to solve Bernstein-Vazirani's algorithm";
    public string source { get; } = "External API: towel.aws.cose.isu.edu:8080 or localhost:5000";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Grant Gardner" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service.
    public SolverType solverType { get; } = SolverType.Quantum;
    // The Bernstein-Vazirani algorithm recovers the full n-bit secret string with exactly
    // one query to the oracle (a single call to the external quantum endpoint here).
    public MathematicalFunction timeComplexity { get; } = new("1", " oracle queries");

    // --- Constructors ---

    /// <summary>
    /// Creates a new BernsteinVaziraniQuantumSolver
    /// </summary>
    public BernsteinVaziraniQuantumSolver() {
    }

    // --- Methods ---

    public string solve(BERNSTEINVAZIRANI problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps: a Try per column of the Bernstein-Vazirani circuit (the one the server runs, drawn and simulated here, never
    // asked of the server), then an Accept with what the server measured (a Reject when its answer is not one; with no
    // answer at all the solver throws SolverUnavailableException, #625), and Done with the verdict.
    public string Solve(BERNSTEINVAZIRANI problem, StepRecorder<QuantumProgress> rec) {
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.BernsteinVazirani(problem.funcValues) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        var counts = new List<KeyValuePair<string, long>>();
        string answer = Ask(problem, counts);
        if (spec != null) {
            int all = spec.Columns.Count, n = problem.NBits;
            // The certificate lists s's bits, "(1,0,1)": they are what the server measured.
            string bits = answer.Length >= 2 && answer[0] == '(' && answer[^1] == ')' ? answer[1..^1].Replace(",", "") : "";
            long? outcome = n > 0 && bits.Length == n && n <= 62 && bits.All(c => c is '0' or '1') ? Convert.ToInt64(bits, 2) : null;
            string[] read = all > 0 ? [QuantumCircuits.ColumnId(all - 1)] : [];
            if (outcome != null)
                rec.Accept(() => new QuantumProgress(all, outcome: outcome, counts: counts, result: answer),
                    () => $"The quantum server measured {bits}{QuantumSteps.Shots(counts, bits)}, so s = {bits}. One oracle call.", read);
            else
                rec.Reject(() => new QuantumProgress(all, counts: counts), () => $"The quantum server gave no answer: {answer}");
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(all, outcome: outcome, counts: counts, result: answer),
                ok => ok ? $"s = {bits}, found with one call to the oracle."
                    : outcome == null ? "No answer: the quantum server did not give one." : $"s = {bits} does not fit every input: f is not s · x for any s.");
        }
        return answer;
    }

    // The server's answer; its counts, when it sends them, go into counts. On any failure of the quantum service this
    // throws SolverUnavailableException (HTTP 503 solver_unavailable), never an error text in place of the answer (#625).
    private string Ask(BERNSTEINVAZIRANI problem, List<KeyValuePair<string, long>> counts) {
        // Get the function values as a boolean array
        bool[] requestBody = problem.funcValues.ToArray();

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the quantum endpoint
            string response = client.PostAsync("/bernstein-vazirani-quantum", requestBody).Result;

            // Parse the JSON response and extract just the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            counts.AddRange(QuantumSteps.Counts(root));
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer", out JsonElement answerElement)
                && answerElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerElement.GetString())) {
                string answer = answerElement.GetString()!;
                // Quantum server returns a bare bit string ("101"); translate to certificate format
                UtilCollection result = new("()");
                foreach (char c in answer)
                    result.Add(new UtilCollection(c.ToString()));
                return result.ToString();
            }

            // No answer in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer", response);
        });
    }
}
