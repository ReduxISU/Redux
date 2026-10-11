using API.Problems.NPComplete.NPC_DM3;
using API.Problems.NPComplete.NPC_SAT3;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_DM3;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// Garey and Johnson's 3SAT -> 3-Dimensional Matching must keep the answer: the formula is satisfiable exactly when the
// reduced instance has a perfect matching. The 3DM brute force is far too slow for these targets (it tries every set of |X|
// triples), so a perfect matching is searched here with a small exact-cover backtracking search instead.
public class GareyJohnson_Soundness_Tests {
    // Whether some assignment satisfies every clause (brute force over the variables).
    internal static bool Satisfiable(SAT3 formula) {
        var variables = formula.literals.Select(l => l.TrimStart('!')).Distinct().ToList();
        for (long mask = 0; mask < 1L << variables.Count; mask++) {
            bool Value(string literal) {
                bool positive = !literal.StartsWith('!');
                bool v = (mask >> variables.IndexOf(literal.TrimStart('!')) & 1) == 1;
                return positive ? v : !v;
            }
            if (formula.clauses.All(c => c.Any(Value))) return true;
        }
        return false;
    }

    // Whether some triples of M cover every element of X, Y and Z exactly once: pick the uncovered element with the fewest
    // triples left, try each.
    internal static bool HasPerfectMatching(DM3 problem) {
        var elements = problem.X.Select(x => "X:" + x).Concat(problem.Y.Select(y => "Y:" + y)).Concat(problem.Z.Select(z => "Z:" + z)).ToList();
        var triples = problem.M.Select(t => new[] { "X:" + t[0], "Y:" + t[1], "Z:" + t[2] }).ToList();
        var covered = new HashSet<string>();
        bool Search() {
            string? best = null;
            List<string[]>? options = null;
            foreach (string e in elements) {
                if (covered.Contains(e)) continue;
                var fits = triples.Where(t => t.Contains(e) && t.All(x => !covered.Contains(x))).ToList();
                if (options == null || fits.Count < options.Count) { best = e; options = fits; }
                if (fits.Count == 0) return false;
            }
            if (best == null) return true;
            foreach (var t in options!) {
                foreach (var x in t) covered.Add(x);
                if (Search()) return true;
                foreach (var x in t) covered.Remove(x);
            }
            return false;
        }
        return Search();
    }

    public static TheoryData<string> Formulas() => new() {
        "(x1)",
        "(x1) & (!x1)",
        "(x1 | x2) & (!x1 | x2) & (!x2)",
        "(x1 | !x2 | x3) & (!x1 | x3 | x1) & (x2 | !x3 | !x1)",
        // u occurs four times, so its wheel has four spokes; a shift of 2 splits it into two halves that can disagree (#693).
        "(u) & (u | q) & (!u | q) & (!u)",
        "(u | q) & (!u | q) & (u | !q) & (!u | !q)",
        // x1 is a prefix of x10: clause (x1) must not take one of x10's elements (#693).
        "(!x10) & (x10 | y) & (x1)",
        "(!a_1) & (a_1 | y) & (a)",
    };

    [Theory]
    [MemberData(nameof(Formulas))]
    public void Satisfiable_ExactlyWhen_TheTargetHasAPerfectMatching(string formula) {
        var sat = new SAT3(formula);
        var target = new GareyJohnson(sat).reductionTo;
        Assert.Equal(Satisfiable(sat), HasPerfectMatching(target));
    }
}
