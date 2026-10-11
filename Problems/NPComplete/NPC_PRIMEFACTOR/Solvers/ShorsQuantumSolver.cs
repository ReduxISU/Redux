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

    // Steps: a Try that sends the number to the quantum server, an Accept with the factors it returns (a Reject when it
    // gives none), and Done with the verdict. The server picks its own base a and circuit, and reports neither, so the
    // steps apply none of the circuit the picture draws for the number.
    public string Solve(PRIMEFACTOR problem, StepRecorder<QuantumProgress> rec) {
        string number = problem.instance.Trim();
        rec.Try(() => new QuantumProgress(), () => $"Send {number} to the quantum server, which runs Shor's algorithm: a quantum circuit finds a period, and classical steps turn it into factors.");
        string answer = Ask(problem);
        if (QuantumSteps.Recording(rec)) {
            bool factored = answer.StartsWith('(') && answer.EndsWith(')');
            if (factored)
                rec.Accept(() => new QuantumProgress(result: answer), () => $"The quantum server answered {answer}.");
            else
                rec.Reject(() => new QuantumProgress(), () => $"The quantum server gave no factors: {answer}");
            QuantumSteps.Done(rec, () => problem.defaultVerifier.verify(problem, answer), new QuantumProgress(result: answer),
                ok => ok ? $"{number} = {string.Join(" × ", answer.Trim('(', ')').Split(','))}." : factored ? $"The answer {answer} is not correct." : "No answer: the quantum server did not give one.");
        }
        return answer;
    }

    // The server's answer, as solve has always returned it.
    private static string Ask(PRIMEFACTOR problem) {
        try {
            // Parse the integer from the problem instance
            int numberToFactor = int.Parse(problem.instance);

            // Create the API client
            var client = new QuantumServerAPI();

            // Make the API call to the prime-factorization-quantum endpoint
            // The API expects just the raw number as the body (e.g., "15")
            string response = client.PostRawJsonAsync("/prime-factorization-quantum", numberToFactor.ToString()).Result;

            // Parse the JSON response and extract the answer
            using JsonDocument doc = JsonDocument.Parse(response);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("answer", out JsonElement answerElement)) {
                // Check if answer is a string or an array
                if (answerElement.ValueKind == JsonValueKind.String) {
                    return answerElement.GetString() ?? "No answer found";
                } else if (answerElement.ValueKind == JsonValueKind.Array) {
                    // If it's an array of factors, serialize it back to a JSON string
                    return "(" + string.Join(",", answerElement.EnumerateArray().Select(e => e.GetInt32())) + ")";
                } else {
                    // For any other type, convert to string
                    return answerElement.ToString();
                }
            }

            // If no answer field, return the whole response
            return response;
        } catch (Exception ex) {
            // Return error information in case of failure
            return $"{{\"error\": \"{ex.Message}\"}}";
        }
    }
}
