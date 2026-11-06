namespace API.Interfaces.Steps;

/// <summary>
/// What <see cref="RunSteps{TState, TPartial}"/> and <see cref="SearchSteps{TState, TChoice, TPartial}"/> share: the recorder the
/// steps go to, the gate their text handlers ask before formatting anything, and handing a step to the recorder built only
/// when it will be kept (and counted, so the clock is still checked, when it will not).
/// </summary>
abstract class StepsCore<TPartial> : IStepGate {
    protected StepsCore(StepRecorder<TPartial> rec) => Rec = rec;

    /// <summary>The recorder the steps go to.</summary>
    protected StepRecorder<TPartial> Rec { get; }

    bool IStepGate.Recording => ((IStepGate)Rec).Recording;
    bool IStepGate.ShowsNextStep => ((IStepGate)Rec).ShowsNextStep;

    /// <summary>Whether the recorder is recording (an Accept and a Done are built whenever it is).</summary>
    protected bool Recording => ((IStepGate)Rec).Recording;

    /// <summary>Hands a built step to the recorder: its partial, caption, focus and (for a search) its tree nodes.</summary>
    protected void Add(StepEvent kind, TPartial partial, string? why, string[] focus, SearchNode[]? tree) =>
        Rec.AddBuilt(kind, partial, why, focus, tree);

    /// <summary>Reports a step the recorder will not build: it is counted (and the clock checked), and nothing is made for it.</summary>
    protected virtual void Skip(StepEvent kind) => Rec.AddBuilt(kind, default!, null, null, null);
}
