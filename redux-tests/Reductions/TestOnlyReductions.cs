using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUBSETSUM;

namespace redux_tests;
#pragma warning disable CS1591

// Test-only reductions for the endpoints' answers about a reduction that cannot be shown. Every shipped reduction records its
// construction steps and maps answers back since #695, so these stand in for one that does neither (no_build_steps) and one that
// records steps but has no backward map (no_backward_map). Both reduce Subset Sum to itself, unchanged.
//
// ProblemProvider scans every loaded assembly, so it registers these under their class names, and the endpoints construct them
// with their (string) constructor as they do any reduction. They have no default constructor on purpose: the catalogs, the
// navigation graph and the tests that default-construct every reduction skip them, as they skip ReductionType_Tests.FakeReduction,
// and the tests that walk the API's own reductions filter on the API assembly. [NotAGeneralReduction] keeps them out of the
// navigable reduction graph, which reads the type's IReduction<,> arguments without constructing it.

/// <summary>A reduction with neither construction steps nor a backward map.</summary>
[NotAGeneralReduction]
class TestOnlyReductionWithoutSteps : IReduction<SUBSETSUM, SUBSETSUM> {
    public TestOnlyReductionWithoutSteps(string instance) {
        reductionFrom = new SUBSETSUM(instance);
        reductionTo = reduce();
    }

    public string reductionName => "Test-only reduction without construction steps";
    public string reductionDefinition => "Subset Sum to itself, for tests.";
    public string source => "";
    public string sourceFile => "";
    public string[] contributors => [];
    public SUBSETSUM reductionFrom { get; }
    public SUBSETSUM reductionTo { get; }
    public SUBSETSUM reduce() => new SUBSETSUM(reductionFrom.instance);
    public string mapSolutions(string problemFromSolution) => problemFromSolution;
}

/// <summary>A reduction that records construction steps (none but Done) and has no backward map.</summary>
[NotAGeneralReduction]
class TestOnlyReductionWithStepsButNoMap : IRecordedReduction<SUBSETSUM, SUBSETSUM> {
    public TestOnlyReductionWithStepsButNoMap(string instance) : this(instance, StepRecorder<BuildDelta>.Off) { }

    public TestOnlyReductionWithStepsButNoMap(string instance, StepRecorder<BuildDelta> rec) {
        reductionFrom = new SUBSETSUM(instance);
        reductionTo = reduce();
        rec.Done(new BuildDelta(), true, "Subset Sum stays as it is.");
    }

    public string reductionName => "Test-only reduction with construction steps but no backward map";
    public string reductionDefinition => "Subset Sum to itself, for tests.";
    public string source => "";
    public string sourceFile => "";
    public string[] contributors => [];
    public SUBSETSUM reductionFrom { get; }
    public SUBSETSUM reductionTo { get; }
    public SUBSETSUM reduce() => new SUBSETSUM(reductionFrom.instance);
    public string mapSolutions(string problemFromSolution) => problemFromSolution;

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new TestOnlyReductionWithStepsButNoMap(reductionFrom.instance, rec);
        return new ConstructionRun(copy.reductionTo, ((IReduction)copy).gadgets, rec.Steps);
    }
}
