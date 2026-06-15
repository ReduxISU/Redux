namespace API.Interfaces;

/// <summary>
/// Non-generic marker interface used for reflection-based discovery in ProblemProvider.
/// </summary>
interface IAnimatable
{
    List<SolverFrame> GetAnimationFrames(string instance);
}

/// <summary>
/// Typed animation interface. Mirrors the IVisualization&lt;T&gt; pattern:
/// the default string overload constructs the typed problem and delegates,
/// so implementing classes only need to provide GetAnimationFrames(T problem).
/// </summary>
interface IAnimatable<T> : IAnimatable where T : IProblem
{
    List<SolverFrame> IAnimatable.GetAnimationFrames(string instance)
        => GetAnimationFrames((T)Activator.CreateInstance(typeof(T), instance)!);

    new List<SolverFrame> GetAnimationFrames(T problem);
}
