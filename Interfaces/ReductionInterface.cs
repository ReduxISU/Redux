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
}

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
/// element of B mapped back. <typeparamref name="TPartial"/> is the SOURCE problem's answer shape
/// (Shapes.cs): the steps show A's answer growing as B's answer is read. Mirrors
/// <see cref="ISolver{T, TPartial}"/>: implement <see cref="MapSolutionBack"/>; the untyped members
/// run it with recording off or on.
/// </summary>
interface IReversibleReduction<T, U, TPartial> : IReduction<T, U> where T : IProblem where U : IProblem {
    /// <summary>
    /// Maps B's answer (<paramref name="problemToSolution"/>, never "{}" - that is handled before this is
    /// called) back to A's answer as certificate text. Throws a parse-failure exception (see
    /// <see cref="ParseGuard.IsCertificateParseFailure"/>) on malformed text; the endpoint turns that into a 400.
    /// </summary>
    string MapSolutionBack(string problemToSolution, StepRecorder<TPartial> rec);

    /// <summary>A's empty answer, shown by the Done step when B had no answer to map.</summary>
    TPartial EmptyAnswer();

    bool IReduction.hasBackwardMap() => true;

    string? IReduction.mapSolutionBack(string problemToSolution) => MapBackRun(problemToSolution, false)!.Answer;

    SolveRun? IReduction.MapBackRun(string problemToSolution, bool withSteps) {
        var rec = withSteps ? new StepRecorder<TPartial>() : StepRecorder<TPartial>.Off;
        string answer;
        if (ReductionBack.IsNoAnswer(problemToSolution)) {
            // "No solution" answers are "{}"; there is nothing to map, and A has none either.
            answer = ReductionBack.NoAnswer;
            rec.Done(EmptyAnswer(), false, $"{reductionTo.problemName} has no answer, so {reductionFrom.problemName} has none to map back to.");
        } else {
            answer = MapSolutionBack(problemToSolution, rec);
        }
        return new SolveRun(answer, rec.Steps.Cast<object>().ToList(), typeof(TPartial));
    }
}
