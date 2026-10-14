using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Tests that assert on wall-clock time go in this collection (#837). xUnit runs a collection that disables
// parallelization on its own, after the parallel tests have finished, so the rest of the suite does not compete
// for the CPU while these are timed. The budgets are unchanged: a real slowdown still fails.
[CollectionDefinition(Name, DisableParallelization = true)]
public class TimingCollection {
    public const string Name = "Timing";
}
