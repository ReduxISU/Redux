using System.Runtime.CompilerServices;

namespace API.Interfaces.Steps;

/// <summary>
/// Records the steps of a solver that does not branch: a brute force trying one candidate after another, a greedy making one
/// move after another, a local search. The solver says, once, how to show what a step is about (its <typeparamref name="TState"/>):
/// the converters given to the constructor turn it into the partial answer and the focus. Each step is then one call that names
/// the event, what it is about, and why: <c>steps.Reject(candidate, $"Try {...}: clause C2 has no true literal.")</c>.
/// <para>
/// The state is whatever the solver has at hand: the candidate it judges, the route so far, or a small tuple of locals
/// (<c>(assignment, failedClause)</c>) when the picture needs more than one of them. A step that shows its state another way
/// than the converters do (a closed tour, the clause that failed) gives <c>partial:</c> or <c>focus:</c> as well; those are
/// called only for a step that is kept.
/// </para>
/// <para>
/// Costs nothing when the recorder is <see cref="StepRecorder{TPartial}.Off"/>: no converter is called, no text formatted. A
/// <see cref="StepRecorder{TPartial}.DeadlineOnly"/> recorder counts every step (to check the clock) and builds nothing.
/// Recording, a Try, Reject or Backtrack is built only within the cap; an Accept is built whenever the recorder is recording,
/// since it keeps the last Accept past the cap (see <see cref="StepRecorder{TPartial}"/>).
/// </para>
/// </summary>
sealed class RunSteps<TState, TPartial> : StepsCore<TPartial> {
    private readonly Func<TState, TPartial> _partial;
    private readonly Func<TState, string[]>? _focus;

    /// <param name="rec">The recorder the steps go to.</param>
    /// <param name="partial">A state's partial answer.</param>
    /// <param name="focus">The ids a step about a state is about; none by default.</param>
    public RunSteps(StepRecorder<TPartial> rec, Func<TState, TPartial> partial, Func<TState, string[]>? focus = null) : base(rec) {
        _partial = partial;
        _focus = focus;
    }

    /// <summary>A Try: the solver tries <paramref name="state"/>.</summary>
    public void Try(TState state, [InterpolatedStringHandlerArgument("")] ShownText why, Func<string[]>? focus = null, Func<TPartial>? partial = null) {
        if (!Rec.CountsSteps) return;
        Step(StepEvent.Try, state, why.ToStringAndClear(), focus, partial);
    }

    /// <summary>An Accept: the solver keeps <paramref name="state"/> (a move made, the best so far, the answer).</summary>
    public void Accept(TState state, [InterpolatedStringHandlerArgument("")] KeptText why, Func<string[]>? focus = null, Func<TPartial>? partial = null) {
        if (!Rec.CountsSteps) return;
        Step(StepEvent.Accept, state, why.ToStringAndClear(), focus, partial);
    }

    /// <summary>A Reject: <paramref name="state"/> fails.</summary>
    public void Reject(TState state, [InterpolatedStringHandlerArgument("")] ShownText why, Func<string[]>? focus = null, Func<TPartial>? partial = null) {
        if (!Rec.CountsSteps) return;
        Step(StepEvent.Reject, state, why.ToStringAndClear(), focus, partial);
    }

    /// <summary>A Backtrack: the solver undoes a move and goes back to <paramref name="state"/>.</summary>
    public void Backtrack(TState state, [InterpolatedStringHandlerArgument("")] ShownText why, Func<string[]>? focus = null, Func<TPartial>? partial = null) {
        if (!Rec.CountsSteps) return;
        Step(StepEvent.Backtrack, state, why.ToStringAndClear(), focus, partial);
    }

    /// <summary>The final step (<see cref="StepRecorder{TPartial}.Done(TPartial, bool, string)"/>): the answer, whether there is one, and why.</summary>
    public void Done(TPartial answer, bool ok, [InterpolatedStringHandlerArgument("")] KeptText why) {
        if (!Recording) return;
        Rec.Done(answer, ok, why.ToStringAndClear()!);
    }

    private void Step(StepEvent kind, TState state, string? why, Func<string[]>? focus, Func<TPartial>? partial) {
        if (Rec.Builds(kind))
            Add(kind, partial != null ? partial() : _partial(state), why, focus != null ? focus() : _focus != null ? _focus(state) : [], null);
        else
            Skip(kind);
    }
}
