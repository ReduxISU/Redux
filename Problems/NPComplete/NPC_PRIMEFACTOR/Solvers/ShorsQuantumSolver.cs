using API.Interfaces;
using API.Tools;
using System.Text.Json;

namespace API.Problems.NPComplete.NPC_PRIMEFACTOR.Solvers;

/// <summary>
/// Shor's algorithm solver that uses an external quantum computing API for prime factorization.
/// Supports both ISU AWS server and local server configurations.
/// </summary>
class ShorsQuantumSolver : ISolver<PRIMEFACTOR> {

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

    // On any failure of the quantum service this throws SolverUnavailableException (HTTP 503 solver_unavailable), never
    // an error text in place of the factors (#625).
    public string solve(PRIMEFACTOR problem) {
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
