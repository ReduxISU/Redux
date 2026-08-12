using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Every top-level problem type must be default-constructible (plan: Redux Tag System,
// Phase 4.5). This is a hard prerequisite for reading declared metadata off problem
// instances (ComplexityClassCatalog, VisualizationTypeCatalog, etc. all instantiate via
// Activator.CreateInstance(type)) and is worth having on its own merits: Nav_Batch.cs's
// allInfo endpoint (InfoJson, :56-60) silently swallows exactly this class of failure
// today via a bare try/catch, so a problem whose default constructor throws simply
// vanishes from every reflection-based endpoint with no signal to anyone.
public class ProblemInstantiation_Tests {
        [Fact]
        public void EveryTopLevelProblem_IsDefaultConstructible() {
                var failedTopLevel = MetadataReflection.TopLevelClassNames
                    .Where(name => MetadataReflection.Failures.ContainsKey(name))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();

                Assert.True(failedTopLevel.Count == 0,
                    "The following top-level problem types could not be default-constructed " +
                    "(Activator.CreateInstance(type) threw, or did not produce an IProblem):\n" +
                    string.Join("\n", failedTopLevel.Select(name =>
                        $"  {name}: {MetadataReflection.Failures[name].GetType().Name}: {MetadataReflection.Failures[name].Message}")));
        }

        [Fact]
        public void AtLeastOneTopLevelProblem_WasFound() {
                // Guards against MetadataReflection.TopLevelClassNames silently becoming empty
                // (e.g. the namespace-shape filter breaking), which would make the assertion
                // above a no-op that always passes.
                Assert.True(MetadataReflection.TopLevelClassNames.Count > 0,
                    "MetadataReflection.TopLevelClassNames is empty — the namespace-shape " +
                    "filter (mirroring ProblemNavigationData.Build in Nav_Problems.cs) found no " +
                    "top-level problems. This test would silently pass everything if that " +
                    "filter broke.");
        }
}
