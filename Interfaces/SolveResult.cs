using System.Text.Json.Serialization;

namespace API.Interfaces;

/// <summary>
/// What a solve run concluded. Declared by the solver, not inferred from its output string:
/// "{}" can mean "no solution exists", "this solver gave up", or a genuinely empty answer,
/// and only the solver knows which.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SolveStatus>))]
public enum SolveStatus {
    /// <summary>The solver has not been updated to report a status; read <see cref="SolveResult.certificate"/> as its raw output.</summary>
    Unclassified = 0,
    /// <summary>A solution was found; <see cref="SolveResult.certificate"/> holds it.</summary>
    Solved,
    /// <summary>The solver proved that no solution exists. Only exact solvers can report this.</summary>
    NoSolution,
    /// <summary>The solver did not find a solution, which does not mean none exists (approximation, heuristic, or stochastic solvers).</summary>
    NotFound,
    /// <summary>The solver stopped because its time limit ran out.</summary>
    Timeout,
}

/// <summary>
/// A solve run's answer plus what it means. <see cref="certificate"/> is only ever a certificate the
/// problem's verifier can read (or null when there is none); any explanation goes in
/// <see cref="message"/>, never in the certificate.
/// </summary>
public record SolveResult(SolveStatus status, string? certificate, string message) {
    /// <summary>A solution was found.</summary>
    public static SolveResult Solved(string certificate, string message = "") =>
        new(SolveStatus.Solved, certificate, message);

    /// <summary>The solver proved no solution exists; <paramref name="message"/> says why.</summary>
    public static SolveResult NoSolution(string message) =>
        new(SolveStatus.NoSolution, null, message);
}
