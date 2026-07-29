using System.Reflection;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Reduction smoke tests (plan: Redux Tag System, Phase 5.1 — do this before 5.2).
//
// For every type in ProblemProvider.Reductions (driven off ReductionGraphData.Graph,
// AdditionalControllers/Navigation/Nav_Reductions.cs:43-83): construct it from
// reductionFrom's defaultInstance, call reduce(), and assert the output instance is
// non-empty and round-trips through reductionTo's string constructor.
//
// Needs zero declared metadata. This is expected to find real bugs: today
// mapSolutions correctness is verified for exactly ONE reduction out of ~20
// (redux-tests/Problems/NPC_SAT3/SAT3_Tests.cs:190), and
// redux-tests/Problems/NPC_CLIQUE/CLIQUE_Tests.cs is 94 lines entirely commented out.
//
// *** A failing case here is the deliverable working correctly. Do not weaken these
// assertions, do not add a skip list to force green, and do not silently patch the
// underlying reduce() implementation to make the failure disappear. Report it. ***
public class ReductionSmoke_Tests
{
    // One [Theory] case per graph edge (not per reduction type — a reduction type
    // could in principle appear on multiple edges if it implemented IReduction<,>
    // more than once, though none currently do). Driving this off
    // ReductionGraphData.Graph rather than ProblemProvider.Reductions directly keeps
    // it exercising exactly what /Navigation/Reductions advertises to callers.
    public static IEnumerable<object[]> AllReductionEdges()
    {
        foreach (var (_, tos) in ReductionGraphData.Graph)
            foreach (var (_, edges) in tos)
                foreach (var edge in edges)
                    yield return new object[] { edge.className };
    }

    // Unwraps the TargetInvocationException Activator.CreateInstance / reflection
    // invocation wraps real exceptions in, so failure messages name the actual
    // exception thrown by problem/reduction code instead of a generic wrapper.
    private static Exception Unwrap(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    [Theory]
    [MemberData(nameof(AllReductionEdges))]
    public void Reduce_ProducesNonEmptyInstance_ThatRoundTripsThroughTargetConstructor(string reductionClassName)
    {
        Assert.True(ProblemProvider.Reductions.TryGetValue(reductionClassName.ToLower(), out var reductionType),
            $"{reductionClassName}: not found in ProblemProvider.Reductions even though it appears in " +
            "ReductionGraphData.Graph. The two are built from the same reflection scan (IReduction), so " +
            "this should be impossible — investigate before anything else here.");

        var generic = reductionType!.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReduction<,>));
        Assert.True(generic != null, $"{reductionClassName}: does not implement IReduction<,>.");
        var typeArgs = generic!.GetGenericArguments();
        Type fromType = typeArgs[0];
        Type toType = typeArgs[1];

        // reductionFrom's defaultInstance is the only instance every problem is
        // guaranteed to have (ProblemInstantiation_Tests already asserts every
        // top-level problem default-constructs), so it's the one fixed point this
        // smoke test can drive every reduction from without any declared metadata.
        var fromDefault = Activator.CreateInstance(fromType) as IProblem;
        Assert.True(fromDefault != null,
            $"{reductionClassName}: {fromType.Name} did not default-construct into an IProblem.");
        string defaultInstance = fromDefault!.defaultInstance;

        // Every reduction class follows the template's constructor chain:
        // Reduction(string instance) : this(new FROM(instance)) { } , and the
        // FROM-typed constructor sets _reductionTo = reduce(). So this single call
        // both builds the reduction AND runs reduce() once already — mirrors exactly
        // how ProblemProvider.reduce() (AdditionalControllers/ProblemProvider.cs:281)
        // constructs reductions from a caller-supplied instance string.
        IReduction reduction;
        try
        {
            reduction = (Activator.CreateInstance(reductionType, defaultInstance) as IReduction)!;
        }
        catch (Exception ex)
        {
            var real = Unwrap(ex);
            Assert.Fail($"{reductionClassName}: constructing from {fromType.Name}.defaultInstance " +
                $"('{defaultInstance}') threw {real.GetType().Name}: {real.Message}");
            return;
        }
        Assert.True(reduction != null, $"{reductionClassName}: constructed instance was not an IReduction.");

        // Call reduce() explicitly (plan 5.1) rather than relying solely on the
        // constructor's internal call, so a reduce() that isn't safely re-callable
        // (e.g. depends on constructor-only side effects) also surfaces as a failure
        // here rather than only showing up via the constructor path.
        IProblem output;
        try
        {
            output = reduction!.reduce();
        }
        catch (Exception ex)
        {
            var real = Unwrap(ex);
            Assert.Fail($"{reductionClassName}: reduce() threw {real.GetType().Name}: {real.Message} " +
                $"(input {fromType.Name}.defaultInstance = '{defaultInstance}')");
            return;
        }
        Assert.True(output != null, $"{reductionClassName}: reduce() returned null.");

        Assert.False(string.IsNullOrWhiteSpace(output!.instance),
            $"{reductionClassName}: reduce() produced an empty/whitespace {toType.Name} instance from " +
            $"{fromType.Name}.defaultInstance = '{defaultInstance}'.");

        try
        {
            var roundTripped = Activator.CreateInstance(toType, output.instance) as IProblem;
            Assert.True(roundTripped != null,
                $"{reductionClassName}: {toType.Name}(string) constructed but did not produce an IProblem " +
                $"from reduce()'s output instance '{output.instance}'.");
        }
        catch (Exception ex)
        {
            var real = Unwrap(ex);
            Assert.Fail($"{reductionClassName}: reduce()'s output instance did not round-trip through " +
                $"{toType.Name}'s string constructor. Output instance: '{output.instance}'. " +
                $"{real.GetType().Name}: {real.Message}");
        }
    }
}
