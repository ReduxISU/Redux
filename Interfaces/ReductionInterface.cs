using API.Interfaces.JSON_Objects;

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
