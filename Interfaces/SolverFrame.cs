namespace API.Interfaces;

/// <summary>
/// Standardized animation frame envelope shared across all animatable problems.
/// Mirrors the Python backend's {action, metrics, state} contract so SARE 2026's
/// frontend can consume frames from either backend without format differences.
/// </summary>
public record SolverFrame(string Action, object Metrics, object State);
