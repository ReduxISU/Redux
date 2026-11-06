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
// The steps the Bernstein-Vazirani Algorithm records, for the Quantum Circuit picture:
//   - an Update per column of the Bernstein-Vazirani circuit, in order: the circuit the server runs, drawn and simulated
//     here (the server is never asked for these). Applying gates has no verdict;
//   - an Accept with the bits the server measured, which are s, or a Reject when its answer is not a bit string;
//   - the Done, with the verifier's verdict.
// With no answer from the server there is no answer at all: Ask throws SolverUnavailableException (HTTP 503
// solver_unavailable). Incremental: the circuit is applied one column at a time, and then read.
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

    // How the steps show the measurement (the answer read, null when the server's reply is not one): read off the end of
    // the whole circuit.
    private static RunSteps<string?, QuantumProgress> NewSteps(StepRecorder<QuantumProgress> rec, int columns, long? outcome,
        List<KeyValuePair<string, long>> counts) => new(rec,
        // What every picture shows: the whole circuit applied, the bits measured, how often the server saw each outcome,
        // and the answer.
        partial: answer => new QuantumProgress(columns, outcome: outcome, counts: counts, result: answer),
        // The circuit's last column, where the answer is read (nothing is read from a reply that is not an answer).
        focus: answer => answer != null && columns > 0 ? [QuantumCircuits.ColumnId(columns - 1)] : []);

    public string solve(BERNSTEINVAZIRANI problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Asks the quantum server to run the Bernstein-Vazirani circuit: one oracle call, after which the input register reads
    // s.
    public string Solve(BERNSTEINVAZIRANI problem, StepRecorder<QuantumProgress> rec) {
        // The circuit is built and simulated only for the steps: a plain solve never builds it.
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.BernsteinVazirani(problem.funcValues) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        var counts = new List<KeyValuePair<string, long>>();
        string answer = Ask(problem, counts);
        if (spec != null) {
            int n = problem.NBits;
            // The certificate lists s's bits, "(1,0,1)": they are what the server measured.
            string bits = answer.Length >= 2 && answer[0] == '(' && answer[^1] == ')' ? answer[1..^1].Replace(",", "") : "";
            long? outcome = n > 0 && bits.Length == n && n <= 62 && bits.All(c => c is '0' or '1') ? Convert.ToInt64(bits, 2) : null;
            var steps = NewSteps(rec, spec.Columns.Count, outcome, counts);
            if (outcome != null)
                steps.Accept(answer, $"The quantum server measured {bits}{QuantumSteps.Shots(counts, bits)}, so s = {bits}. One oracle call.");
            else
                steps.Reject(null, $"The quantum server gave no answer: {answer}");

            var final = new QuantumProgress(spec.Columns.Count, outcome: outcome, counts: counts, result: answer);
            if (QuantumSteps.Verdict(() => problem.defaultVerifier.verify(problem, answer)))
                steps.Done(final, true, $"s = {bits}, found with one call to the oracle.");
            else if (outcome == null)
                steps.Done(final, false, $"No answer: the quantum server did not give one.");
            else
                steps.Done(final, false, $"s = {bits} does not fit every input: f is not s · x for any s.");
        }
        return answer;
    }

    // The server's answer; its counts, when it sends them, go into counts. On any failure of the quantum service this
    // throws SolverUnavailableException (HTTP 503 solver_unavailable), never an error text in place of the answer.
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
                // Anything but exactly NBits binary digits is not an answer: fail as a missing one does, not pass it on (#625).
                if (answer.Length != problem.NBits || answer.Any(c => c != '0' && c != '1'))
                    throw new InvalidDataException($"its 'answer' is not {problem.NBits} binary digits: {response}");
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
