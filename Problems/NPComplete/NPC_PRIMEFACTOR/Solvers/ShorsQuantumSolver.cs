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
    public string solverName { get; } = "Shor's Quantum API Solver";
    public string solverDefinition { get; } =
        "Calls external quantum computing API to solve prime factorization using Shor's algorithm. " +
        "Shor's algorithm uses quantum phase estimation to efficiently find the period of a " +
        "modular exponential function, which can then be used to determine the prime factors of a " +
        "composite number through classical number theory operations like the greatest common divisor.";
    public string source { get; } = "https://arxiv.org/abs/quant-ph/9708016";
    public string[] contributors { get; } = { "Grant Gardner", "George Lake", "Jason Wright" };
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
    public string complexity { get; } = "O((log n)^3)";

    // --- Constructors ---

    /// <summary>
    /// Creates a new ShorsQuantumSolver
    /// </summary>
    public ShorsQuantumSolver() {
    }

    // --- Methods ---

    public string solve(PRIMEFACTOR problem) {
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
