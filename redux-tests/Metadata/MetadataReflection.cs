using API.Interfaces;

namespace redux_tests;
#pragma warning disable CS1591

// Shared reflection cache for the redux-tests/Metadata/ suite (plan: Redux Tag System,
// Phase 4.5). Instantiates every ProblemProvider.Problems value exactly once, per-type
// try/catch — mirrors ComplexityClassCatalog / VisualizationTypeCatalog in the API
// project (Nav_Problems.cs / Nav_Visualizations.cs): one problem with a
// throwing/missing default constructor must not take down every test that reads
// declared metadata off problem instances.
//
// Other metadata tests (ProblemInstantiation_Tests, ComplexityClass_Tests, and the
// characterization report) all read from this cache instead of re-instantiating.
internal static class MetadataReflection {
        internal sealed class Result {
                // className -> constructed instance, for every ProblemProvider.Problems entry
                // that could be default-constructed.
                public Dictionary<string, IProblem> Instances { get; } = new(StringComparer.OrdinalIgnoreCase);

                // className -> the exception thrown while default-constructing that type.
                // Disjoint from Instances by construction (every entry lands in exactly one).
                public Dictionary<string, Exception> Failures { get; } = new(StringComparer.OrdinalIgnoreCase);

                // className of every TOP-LEVEL problem, i.e. those matching the namespace
                // shape API.Problems.<Folder>.<ProblemFolder> that ProblemNavigationData.Build
                // (Nav_Problems.cs) lists. Excludes nested helper problems such as
                // API.Problems.NPComplete.NPC_CLIQUE.Inherited.SipserClique.
                public HashSet<string> TopLevelClassNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly Lazy<Result> _result = new(Build);

        internal static Dictionary<string, IProblem> Instances => _result.Value.Instances;
        internal static Dictionary<string, Exception> Failures => _result.Value.Failures;
        internal static HashSet<string> TopLevelClassNames => _result.Value.TopLevelClassNames;

        private static Result Build() {
                var result = new Result();
                foreach (var (_, type) in ProblemProvider.Problems) {
                        // Same namespace-shape test as ProblemNavigationData.Build. Duplicated
                        // rather than shared because that method is private; both must be kept
                        // in sync with the source-of-truth comment in Nav_Problems.cs if the
                        // folder layout convention ever changes.
                        string[] ns = (type.Namespace ?? "").Split('.');
                        int i = Array.IndexOf(ns, "Problems");
                        if (i >= 0 && ns.Length == i + 3)
                                result.TopLevelClassNames.Add(type.Name);

                        try {
                                if (Activator.CreateInstance(type) is IProblem instance) {
                                        result.Instances[type.Name] = instance;
                                }
                                else {
                                        result.Failures[type.Name] = new InvalidOperationException(
                                            $"{type.Name} default-constructed but the result did not implement IProblem.");
                                }
                        }
                        catch (Exception ex) {
                                result.Failures[type.Name] = ex;
                        }
                }
                return result;
        }
}
