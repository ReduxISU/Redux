using System.Reflection;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Reduction validity static pre-check (plan: Redux Tag System, Section 3 / Part B).
//
// A genuine polynomial reduction A -> B means "B is at least as hard as A" (that's what
// a reduction proves). So a reduction that goes from a harder declared ComplexityClass to
// an easier one is a red flag — for a *correct* reduction, it would imply the easier class
// is at least as hard as the harder one (e.g. a P -> NPComplete-shaped edge implies P = NP).
// It is almost certainly a mislabeled endpoint, not a proof of a Millennium Prize result.
// One-directional only: hard -> easy is flagged, easy -> hard is completely fine (that is
// the normal shape of these reductions).
//
// *** Do not weaken this test or add a skip-list to force green. A violation here is
// either a real mislabel bug or a legitimately new complexity relationship someone needs
// to reason about — report it, don't silently patch the assertion or the underlying
// ComplexityClass declaration to make the failure disappear. ***
public class ReductionValidity_Tests {
        // Explicit rank table — deliberately NOT the enum's ordinal position (ComplexityClass
        // declares NPHard after NPComplete, which would rank them differently by ordinal even
        // though they're the same "hard" tier and no violation should be flagged between them
        // in either direction).
        //
        // Unclassified and QuantumOracle are both excluded from ranking entirely (absent from
        // this table, not mapped to a rank): Unclassified has no declared claim to check, and
        // QuantumOracle's doc comment (Interfaces/ComplexityClass.cs) explicitly says
        // reduction-validity checks must skip members of this class rather than compare
        // against them (query/oracle-complexity promise problems are incomparable with the
        // classical P/NP hierarchy).
        private static readonly Dictionary<ComplexityClass, int> Rank = new() {
                [ComplexityClass.P] = 0,
                [ComplexityClass.NPIntermediate] = 1,
                [ComplexityClass.NPComplete] = 2,
                [ComplexityClass.NPHard] = 2,
        };

        // One [Theory] case per graph edge whose BOTH endpoints have a ranked ComplexityClass
        // — filtered before yielding so an Unclassified/QuantumOracle endpoint doesn't show up
        // as a vacuously-passing case, it simply isn't a case at all. Also excludes anything
        // already on ViolationAllowlist below: an allowlisted edge is a known, sign-off-pending
        // violation — it stays visible via AllowlistHasNoStaleEntries instead of failing this
        // Theory on every run once it has already been reported.
        public static IEnumerable<object[]> RankedReductionEdges() {
                var allowlist = new HashSet<string>(ViolationAllowlist, StringComparer.OrdinalIgnoreCase);
                foreach (var (_, tos) in ReductionGraphData.Graph)
                        foreach (var (_, edges) in tos)
                                foreach (var edge in edges) {
                                        if (!TryGetEndpointRanks(edge.className, out _, out _, out _, out _))
                                                continue;
                                        if (allowlist.Contains(edge.className))
                                                continue;
                                        yield return new object[] { edge.className };
                                }
        }

        // Resolves a reduction class name to its IReduction<,> generic type args, then looks
        // up each endpoint's declared ComplexityClass via MetadataReflection.Instances (the
        // typed enum, not the pre-stringified fromComplexity/toComplexity on ReductionEdge —
        // avoids a string round-trip). Returns false if either endpoint's rank is undefined
        // (Unclassified, QuantumOracle, or the type couldn't be resolved/instantiated at all).
        private static bool TryGetEndpointRanks(
            string reductionClassName,
            out ComplexityClass fromClass,
            out ComplexityClass toClass,
            out int fromRank,
            out int toRank) {
                fromClass = toClass = ComplexityClass.Unclassified;
                fromRank = toRank = -1;

                if (!ProblemProvider.Reductions.TryGetValue(reductionClassName.ToLower(), out var reductionType))
                        return false;

                var generic = reductionType!.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReduction<,>));
                if (generic == null) return false;

                var typeArgs = generic.GetGenericArguments();
                string fromName = typeArgs[0].Name;
                string toName = typeArgs[1].Name;

                if (!MetadataReflection.Instances.TryGetValue(fromName, out var fromInstance)) return false;
                if (!MetadataReflection.Instances.TryGetValue(toName, out var toInstance)) return false;

                fromClass = fromInstance.complexityClass;
                toClass = toInstance.complexityClass;

                if (!Rank.TryGetValue(fromClass, out fromRank)) return false;
                if (!Rank.TryGetValue(toClass, out toRank)) return false;

                return true;
        }

        [Theory]
        [MemberData(nameof(RankedReductionEdges))]
        public void Reduction_DoesNotGoFromHarderToEasierComplexityClass(string reductionClassName) {
                bool ok = TryGetEndpointRanks(reductionClassName, out var fromClass, out var toClass, out var fromRank, out var toRank);
                Assert.True(ok,
                    $"{reductionClassName}: RankedReductionEdges() yielded this case, but its endpoints could not be " +
                    "re-resolved to ranked ComplexityClass values on the second pass — investigate before anything else here.");

                Assert.True(fromRank <= toRank,
                    $"{reductionClassName} reduces a {fromClass} problem to a {toClass} problem — that's backwards. " +
                    $"A genuine polynomial reduction from A to B proves B is at least as hard as A, so a reduction " +
                    $"declared to go from {fromClass} (rank {fromRank}) to {toClass} (rank {toRank}) implies {toClass} " +
                    $"is no harder than {fromClass}, i.e. it implies {fromClass} = {toClass} for the classical hierarchy " +
                    "(e.g. a P -> NPComplete-shaped violation implies P = NP). This is almost certainly a mislabeled " +
                    "ComplexityClass on one of the two endpoints, not a proof of a Millennium Prize result — go check " +
                    "which one is wrong. If you're certain both declarations are correct and this really is a new " +
                    "relationship someone needs to reason about, do NOT weaken this assertion — add the edge to " +
                    "ViolationAllowlist below with a one-line rationale and flag it for advisor sign-off.");
        }

        // ── Ratchet-style allowlist for violations that need human sign-off ──────────────
        //
        // Empty by default. If running this test after writing it turns up a real violation
        // against current data, do not silently patch the assertion or the underlying
        // ComplexityClass/reduction declaration — add the specific reduction class name here
        // with a one-line rationale, and call it out prominently in the report to the user
        // (name the reduction class and both ComplexityClass values involved). Declaring or
        // re-declaring a complexity relationship is a correctness claim that needs advisor
        // sign-off before merge, same precedent as ComplexityClass_Tests.cs's own header.
        private static readonly string[] ViolationAllowlist =
        {
    };

        [Fact]
        public void AllowlistHasNoStaleEntries() {
                var actuallyViolating = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (_, tos) in ReductionGraphData.Graph)
                        foreach (var (_, edges) in tos)
                                foreach (var edge in edges) {
                                        if (!TryGetEndpointRanks(edge.className, out _, out _, out var fromRank, out var toRank))
                                                continue;
                                        if (fromRank > toRank)
                                                actuallyViolating.Add(edge.className);
                                }

                var stale = ViolationAllowlist.Where(c => !actuallyViolating.Contains(c)).ToList();
                Assert.True(stale.Count == 0,
                    $"Allowlist entry no longer a hard->easy violation (fixed, renamed, or removed) — delete from " +
                    $"ViolationAllowlist in ReductionValidity_Tests.cs: {string.Join(", ", stale)}.");
        }
}
