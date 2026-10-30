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
    // Link to the formal citation in `source`. Optional, so it defaults to "". [JsonIgnore] keeps it out
    // of JSON written through an interface-typed member (e.g. /ProblemProvider/reduce), which never had
    // it; a class that declares its own sourceLink still shows it in /info.
    [System.Text.Json.Serialization.JsonIgnore]
    string sourceLink { get => ""; }
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
    // reductionType is a tag set (see Tags): declare every technique the construction uses. The JSON
    // lists them as reductionTypes; the single reductionType field is deprecated and holds the first.
    ReductionType reductionType { get => ReductionType.Unclassified; }

    ReductionComplexityBucket complexityBucket { get => ReductionComplexityBucket.Unclassified; }

    // Free-text Big-O note on how long the reduction takes, e.g. "O(n^2), n = |CLIQUE.nodes|". Every
    // reduction declares it (the metadata tests require it non-empty), so it is required here. JsonIgnore
    // as for sourceLink: /ProblemProvider/reduce, which writes the reduction as IReduction, never had it.
    // #615 replaces this string with timeComplexity/spaceComplexity (MathematicalFunction), as #614 did for
    // solvers.
    [System.Text.Json.Serialization.JsonIgnore]
    string? complexity { get; }

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
