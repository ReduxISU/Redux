using API.Interfaces.JSON_Objects;
using API.Interfaces.Steps;

namespace API.Interfaces;

// Whether a reduction is valid for ANY instance of its FROM problem (the actual
// mathematical definition of a reduction) is a fact about the CLASS, not about any
// particular instance -- so this is a type-level attribute, not an instance property.
// That matters concretely: a class whose default constructor is itself only valid on
// specially-shaped input (e.g. SipserReduceToSAT3, whose default ctor chains through
// `new CLIQUE()` and immediately calls reduce() on it) can't safely be
// Activator.CreateInstance'd just to ask it "are you general?" -- an attribute answers
// that via pure reflection over the type, no construction required. Apply to a
// concrete IReduction<,> class that implements the interface for some reason other than
// "this reduces any FROM instance to a valid TO instance" -- e.g. a solution-mapping
// companion to one specific other reduction (see SipserReduceToSAT3). Gates whether
// ReductionGraphData.Build() (Nav_Reductions.cs) advertises the class as a navigable
// /Navigation/Reductions edge; it does NOT remove the class from ProblemProvider.Reductions
// or /ProblemProvider/reduce?reduction=<name>, which construct by class name directly.
/// <summary>
/// Marks an <see cref="IReduction{T,U}"/> class as not a general reduction: its
/// <c>reduce()</c> is only valid for specially-shaped instances of the FROM problem, not
/// any instance. Excludes it from the navigable graph built by
/// <c>ReductionGraphData.Build()</c> (Nav_Reductions.cs) without needing to construct an
/// instance to check.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NotAGeneralReductionAttribute : Attribute { }

interface IReduction {
    string reductionName { get; }
    string reductionDefinition { get; }
    string source { get; }
    // Repo-relative path of the file declaring this class; see SourceFile.
    string sourceFile { get; }
    string[] contributors { get; }
    IVisualization visualization { get; }
    List<Gadget> gadgets { get; }
    IProblem reductionFrom { get; }
    IProblem reductionTo { get; }
    IProblem reduce();
    string mapSolutions(string problemFromSolution);

    // Declared, not derived — same "default interface member, override on the concrete
    // class" shape as IProblem.complexityClass. See ReductionCost.cs for what this is
    // (and isn't: it's not the pre-existing ad-hoc `complexity` string field some
    // reduction classes already carry).
    ReductionCost cost { get => ReductionCost.Unclassified; }

    // Same shape as `cost` above, on two further independent axes: `reductionType` is HOW
    // the transformation is constructed, `complexityBucket` is how long constructing it
    // takes. Kept as separate members rather than folded into `cost` because a reduction
    // can be cheap on one axis and expensive on another — see the class docs on each enum.
    ReductionType reductionType { get => ReductionType.Unclassified; }

    ReductionComplexityBucket complexityBucket { get => ReductionComplexityBucket.Unclassified; }

    // Backward mapping: an answer for the target problem (B) turned back into an answer for the
    // source problem (A), the direction students need after solving B. Reductions that have one
    // implement IReversibleReduction<T, U, TPartial>; everything else keeps these defaults.
    // Deliberately METHODS, not properties: /reduce serializes through this interface, and a
    // property here (even a default one) would appear in every reduction's JSON. Cheap to ask, no solving.
    bool hasBackwardMap() => false;

    // The bare backward answer (A's certificate text), or null when this reduction has no backward map.
    string? mapSolutionBack(string problemToSolution) => null;

    // The backward answer plus, when asked, the steps that produced it; null when there is no backward map.
    SolveRun? MapBackRun(string problemToSolution, bool withSteps) => null;

    // Construction steps: what the reduction built, piece by piece (one step per gadget, in both problems'
    // words). Reductions that record them implement IRecordedReduction<T, U>. Methods, not properties, for the
    // same reason as the backward map above.
    bool hasBuildSteps() => false;

    // Builds the target again from reductionFrom, recording the steps when asked; null when this reduction
    // does not record them. The reduction itself is untouched: the build runs on a fresh copy.
    ConstructionRun? BuildRun(bool withSteps) => null;
}

/// <summary>
/// The result of <see cref="IReduction.BuildRun"/>: the target and gadgets of a fresh build, and the steps
/// recorded while building them.
/// </summary>
/// <param name="Target">The target problem, equal to the reduction's own <c>reductionTo</c>.</param>
/// <param name="Gadgets">The gadget list, equal to the reduction's own <c>gadgets</c>. Steps refer to it by index.</param>
/// <param name="Steps">The steps in order, empty when recording was off.</param>
sealed record ConstructionRun(IProblem Target, IReadOnlyList<Gadget> Gadgets, IReadOnlyList<SolverStep<BuildDelta>> Steps);

interface IReduction<T, U> : IReduction where T : IProblem where U : IProblem {
    IVisualization IReduction.visualization {
        get {
            return reductionTo.defaultVisualization;
        }
    }

    IProblem IReduction.reductionFrom {
        get {
            return reductionFrom;
        }
    }
    new T reductionFrom { get; }
    IProblem IReduction.reductionTo {
        get {
            return reductionTo;
        }
    }
    new U reductionTo { get; }

    IProblem IReduction.reduce() {
        return reduce();
    }
    new U reduce();

    List<Gadget> IReduction.gadgets {
        get {
            return new List<Gadget>();
        }
    }
}

/// <summary>
/// A reduction that can also map an answer for B back to an answer for A, recording one step per
/// element of B mapped back. <typeparamref name="TPartial"/> is the SOURCE problem's (A's) answer shape
/// (Shapes.cs): every step's partial shows A's answer growing as B's answer is read. Each step's
/// <c>focus</c> names elements of B, in the TARGET instance's id space (the node of B being read), not A's.
/// Mirrors <see cref="ISolver{T, TPartial}"/>: implement <see cref="MapBack"/>; the untyped members run it
/// with recording off or on and add the Done step.
/// </summary>
interface IReversibleReduction<T, U, TPartial> : IReduction<T, U> where T : IProblem where U : IProblem {
    /// <summary>
    /// Maps B's answer (<paramref name="problemToSolution"/>) back to A's answer, recording one step per
    /// element of B read (focus = B's ids, partial = A's answer so far). Returns A's answer as certificate
    /// text and as <typeparamref name="TPartial"/>; do not record Done, the caller does. Receives "{}" only
    /// when <see cref="TargetAcceptsEmpty"/> says the empty set is a real answer for B. Throws a parse-failure
    /// exception (see <see cref="ParseGuard.IsCertificateParseFailure"/>) on malformed text; the endpoint turns
    /// that into a 400.
    /// </summary>
    (string Answer, TPartial Partial) MapBack(string problemToSolution, StepRecorder<TPartial> rec);

    /// <summary>A's empty answer, shown by the Done step when B had no answer to map.</summary>
    TPartial EmptyAnswer();

    /// <summary>
    /// "{}" is the no-answer convention (#605), but for a set-valued B it is also the empty set, which can be
    /// a real answer (an empty cover of an edgeless graph, a clique of size 0). Returns whether it is one for
    /// THIS target instance; when it is, "{}" is mapped like any other answer. Default: B's verifier. Override
    /// when B's verifier refuses empty text outright.
    /// </summary>
    bool TargetAcceptsEmpty() => ReductionBack.Accepts(reductionTo, ReductionBack.NoAnswer);

    /// <summary>Same question as <see cref="TargetAcceptsEmpty"/> for A, asked when the mapped answer is "{}".</summary>
    bool SourceAcceptsEmpty() => ReductionBack.Accepts(reductionFrom, ReductionBack.NoAnswer);

    bool IReduction.hasBackwardMap() => true;

    string? IReduction.mapSolutionBack(string problemToSolution) => MapBackRun(problemToSolution, false)!.Answer;

    SolveRun? IReduction.MapBackRun(string problemToSolution, bool withSteps) {
        var rec = withSteps ? new StepRecorder<TPartial>() : StepRecorder<TPartial>.Off;
        string a = reductionFrom.problemName, b = reductionTo.problemName;
        bool emptyTarget = ReductionBack.IsNoAnswer(problemToSolution);
        if (emptyTarget && !TargetAcceptsEmpty()) {
            // "No solution" answers are "{}"; there is nothing to map, and A has none either.
            rec.Done(EmptyAnswer(), false, $"{b} has no answer, so {a} has none to map back to.");
            return new SolveRun(ReductionBack.NoAnswer, rec.Steps.Cast<object>().ToList(), typeof(TPartial));
        }

        // B's answer is checked first so a wrong one is named as the cause; A's verifier alone decides ok,
        // since a mapped answer can be valid for A even when B's was not (a cover larger than B allows).
        bool targetValid = emptyTarget || ReductionBack.Accepts(reductionTo, problemToSolution);
        (string answer, TPartial partial) = MapBack(problemToSolution, rec);
        bool ok = ReductionBack.IsNoAnswer(answer) ? SourceAcceptsEmpty() : ReductionBack.Accepts(reductionFrom, answer);

        string bAnswer = problemToSolution.Trim();
        string caption = (targetValid, ok) switch {
            (true, true) => $"{answer} is a valid {a} answer.",
            (false, true) => $"{bAnswer} is not a valid {b} answer, but the mapped answer {answer} is still a valid {a} answer.",
            (false, false) => $"{bAnswer} is not a valid {b} answer, and the mapped answer {answer} is not a valid {a} answer either.",
            (true, false) => $"The mapped answer {answer} is not a valid {a} answer.",
        };
        rec.Done(partial, ok, caption);
        return new SolveRun(answer, rec.Steps.Cast<object>().ToList(), typeof(TPartial));
    }
}

/// <summary>
/// A reduction whose construction code records steps while it really builds the target. Implement
/// <see cref="Rebuild"/>: build a fresh copy of the reduction from <c>reductionFrom</c> with the recorder
/// passed to its constructor (the build code itself calls the recorder; never re-derive the steps from the
/// finished target elsewhere), and return that copy's target and gadgets. The normal constructor builds with
/// <see cref="StepRecorder{TPartial}.Off"/>, so recording costs nothing unless asked for.
/// <para>
/// Also give the class a public constructor <c>(string instance, StepRecorder&lt;BuildDelta&gt; rec)</c> that builds from the
/// instance with <c>rec</c>. <c>/buildSteps</c> and <c>/reductionView</c> construct the reduction with it, so the one build
/// gives the reduction (its target, gadgets and backward map) and its steps; without it they build twice (the constructor,
/// then <see cref="Rebuild"/>). A test checks every recorded reduction has it.
/// </para>
/// <para>
/// The step shape is fixed (<see cref="BuildDelta"/>) rather than a type parameter: every construction adds
/// pieces to a target, so one shape lets the Reduction View replay any reduction's Build phase the same way.
/// </para>
/// </summary>
interface IRecordedReduction<T, U> : IReduction<T, U> where T : IProblem where U : IProblem {
    ConstructionRun Rebuild(StepRecorder<BuildDelta> rec);

    bool IReduction.hasBuildSteps() => true;

    // Construction steps are deltas replayed in order, so none may be dropped: every step is recorded (no cap), and
    // ConstructionSteps.Limit then folds the steps past the usual cap into one combined step.
    ConstructionRun? IReduction.BuildRun(bool withSteps) {
        if (!withSteps) return Rebuild(StepRecorder<BuildDelta>.Off);
        ConstructionRun run = Rebuild(new StepRecorder<BuildDelta>(cap: int.MaxValue));
        return run with { Steps = ConstructionSteps.Limit(run.Steps) };
    }
}
