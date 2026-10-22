using API.Interfaces;
using API.Interfaces.Quantum;
using API.Interfaces.Steps;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;

/// <summary>
/// Shor's algorithm solver that uses an external quantum computing API for prime factorization.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
// Incremental: the number is sent once and the answer read once.
class ShorsQuantumSolver : ISolver<PRIMEFACTOR, QuantumProgress>, IIncrementalSolver {

    // --- Fields ---
    // Solver metadata
    public string solverName { get; } = "Shor's Algorithm";
    public string solverDefinition { get; } =
        "Calls external quantum computing API to solve prime factorization using Shor's algorithm. " +
        "Shor's algorithm uses quantum phase estimation to efficiently find the period of a " +
        "modular exponential function, which can then be used to determine the prime factors of a " +
        "composite number through classical number theory operations like the greatest common divisor.";
    public string source { get; } = "https://arxiv.org/abs/quant-ph/9708016";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Grant Gardner", "George Lake", "Jason L. Wright" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Delegates to the external quantum-simulator service. complexityBucket is
    // intentionally left Unclassified, not just an unfilled gap: tagging Shor's algorithm with any
    // classical growth bucket would be a category error -- its entire point is a proven quantum-vs-
    // classical complexity separation (exponential classical factoring vs. polynomial quantum).
    public SolverType solverType { get; } = SolverType.Quantum;
    // Shor's algorithm's quantum period-finding (phase estimation + QFT) plus the classical
    // GCD post-processing described in solverDefinition runs in time polynomial in the number
    // n's bit-length -- the textbook O((log n)^3) bound -- versus the exponential-in-bit-length
    // cost of classical trial division (PrimeFactorSolver).
    public MathematicalFunction timeComplexity { get; } = new("(log n)^3", "");

    // --- Constructors ---

    /// <summary>
    /// Creates a new ShorsQuantumSolver
    /// </summary>
    public ShorsQuantumSolver() {
    }

    // --- Methods ---

    public string solve(PRIMEFACTOR problem) => Solve(problem, StepRecorder<QuantumProgress>.Off);

    // Steps: a Try that sends the number to the quantum server, a Try per column of the period-finding circuit the picture
    // draws for the number (drawn, not simulated, and never asked of the server), an Accept with the factors the server
    // returns (a Reject when they are not factors; with no answer at all the solver throws SolverUnavailableException,
    // #625), and Done with the verdict, so the finished picture shows the circuit as run (#903). The server picks its own base a and reports it not, so the drawn circuit is the textbook one for the
    // smallest a that works; a number with no circuit to draw has no column steps.
    public string Solve(PRIMEFACTOR problem, StepRecorder<QuantumProgress> rec) {
        string number = problem.instance.Trim();
        rec.Try(() => new QuantumProgress(), () => $"Send {number} to the quantum server, which runs Shor's algorithm: a quantum circuit finds a period, and classical steps turn it into factors.");
        var spec = QuantumSteps.Recording(rec) ? QuantumCircuits.PrimeFactorization(problem.instance) : null;
        if (spec != null) QuantumSteps.Columns(rec, spec);
        string answer = Ask(problem);
        if (spec != null) {
            int all = spec.Columns.Count;
            string[] read = all > 0 ? [QuantumCircuits.ColumnId(all - 1)] : [];
            bool factored = answer.StartsWith('(') && answer.EndsWith(')');
            if (factored)
                rec.Accept(() => new QuantumProgress(all, result: answer), () => $"The quantum server answered {answer}.", read);
            else
                rec.Reject(() => new QuantumProgress(all), () => $"The quantum server gave no factors: {answer}");
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(all, result: answer),
                ok => ok ? $"{number} = {string.Join(" × ", answer.Trim('(', ')').Split(','))}." : factored ? $"The answer {answer} is not correct." : "No answer: the quantum server did not give one.");
        }
        return answer;
    }

    // The server's answer. On any failure of the quantum service this throws SolverUnavailableException (HTTP 503
    // solver_unavailable), never an error text in place of the factors (#625).
    private string Ask(PRIMEFACTOR problem) {
        // Parse the integer from the problem instance
        int numberToFactor = int.Parse(problem.instance);

        return QuantumServerAPI.Ask(this, problem.problemName, client => {
            // Make the API call to the prime-factorization-quantum endpoint
            // The API expects just the raw number as the body (e.g., "15")
            string response = client.PostRawJsonAsync("/prime-factorization-quantum", numberToFactor.ToString()).Result;

            // Parse the JSON response and extract the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("answer", out JsonElement answerElement)) {
                // Check if answer is a string or an array
                if (answerElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(answerElement.GetString())) {
                    return answerElement.GetString()!;
                } else if (answerElement.ValueKind == JsonValueKind.Array) {
                    // If it's an array of factors, write it in the certificate format
                    return "(" + string.Join(",", answerElement.EnumerateArray().Select(e => e.GetInt32())) + ")";
                } else if (answerElement.ValueKind == JsonValueKind.Number) {
                    // A bare number is passed on as the server sent it, as it always has been
                    return answerElement.GetRawText();
                }
            }

            // No answer in the response: not an answer to pass on.
            throw QuantumServerAPI.MissingAnswer("answer", response);
        });
    }
}
