using API.Interfaces;
using API.Interfaces.Steps;
using API.Interfaces.Tripartite;
using API.Problems.NPComplete.NPC_DM3.Visualizations;

namespace API.Problems.NPComplete.NPC_DM3.Solvers;

// Not incremental: every step judges a whole set of |X| triples.
class ThreeDimensionalMatchingBruteForce : ISolver<DM3, ChosenTriples> {

    // --- Fields ---
    public string solverName { get; } = "3-Dimensional Matching Brute Force";
    public string solverDefinition { get; } = "This is a generic local search solver for 3-Dimensional Matching, which, while possible, removes one constraint from the current solution, and swaps in two more constraints.";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Caleb Eardley" };
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Unpruned exhaustive enumeration.
    public SolverType solverType { get; } = SolverType.BruteForce;
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Exponential;
    // SURPRISING: the number of combinations tried is at most the binomial coefficient C(|M|,|X|)
    // (Combinations below hands them out one at a time) -- NOT a flat 2^n or n! bound. |M|
    // (candidate triples) is an input size independent of |X|, |Y|, |Z|; when |M| is large relative
    // to n = |X| (up to n^3 possible triples), C(|M|,n) grows asymptotically worse than n! (e.g.
    // n=3, |M|=27 already gives C(27,3)=2925 > 3!=6). Exponential is kept as the closest available
    // bucket -- same reasoning as CliqueCoverBruteForce's (K+1)^n case, this combinatorial growth
    // isn't cleanly "exponential" or "factorial" in a single variable either. Each combination costs
    // O(n^2) to verify (GenericVerifierDM3's parse plus per-coordinate List.Contains membership scans).
    public string complexity { get; } = "O(C(m, n) * n^2), m = |M| (candidate triples), n = |X| = |Y| = |Z|";

    // --- Methods Including Constructors ---
    public ThreeDimensionalMatchingBruteForce() {

    }


    private string indexListToCertificate(List<int> indecies, List<List<string>> M) {
        string certificate = "";
        foreach (int i in indecies) {
            string set = "";
            foreach (string e in M[i]) {
                set += "," + e;
            }
            set = "{" + set.Substring(1) + "}";
            certificate += "," + set;
        }
        return "{" + certificate.Substring(1) + "}";
    }

    /// <summary>
    /// Every way to pick <paramref name="size"/> of <paramref name="count"/> triples, one at a time, as ascending positions
    /// in lexicographic order ({0,1,2}, {0,1,3}, ...), the order the solver always tried them in. Each is a new list.
    /// Nothing when <paramref name="size"/> is more than <paramref name="count"/>.
    /// </summary>
    public static IEnumerable<List<int>> Combinations(int count, int size) {
        if (size > count) yield break;
        var combination = Enumerable.Range(0, size).ToList();
        while (true) {
            yield return new List<int>(combination);
            int i = size - 1;
            while (i >= 0 && combination[i] == count - size + i) i--;
            if (i < 0) yield break;
            combination[i]++;
            for (int j = i + 1; j < size; j++) combination[j] = combination[j - 1] + 1;
        }
    }

    public string solve(DM3 problem) => Solve(problem, StepRecorder<ChosenTriples>.Off);

    // Steps: one per set of |X| triples tried, in the order Combinations hands them out; the first set the verifier accepts
    // is the answer.
    public string Solve(DM3 problem, StepRecorder<ChosenTriples> rec) {
        int n = problem.X.Count, m = problem.M.Count;
        // A matching needs |X| triples; with fewer in M there is none (#745).
        if (m < n) {
            rec.Done(new ChosenTriples([]), false, m == 0
                ? $"M has no triples, so nothing can match the {Plural(n, "element")} of X."
                : $"M has only {Plural(m, "triple")}, but a matching needs {n}, one for each element of X. There is nothing to try.");
            return "{}";
        }
        // Read only inside recorder lambdas (and for Done), so it costs nothing when recording is off.
        TripartiteSpec? spec = null;
        TripartiteSpec Spec() => spec ??= DM3Visualization.Spec(problem);
        long tried = 0;
        foreach (List<int> combination in Combinations(m, n)) {
            tried++;
            string certificate = indexListToCertificate(combination, problem.M);
            if (problem.defaultVerifier.verify(problem, certificate)) {
                rec.Accept(() => TripartiteFrames.Chosen(combination), () => TryCaption(Spec(), combination, accepted: true));
                rec.Done(TripartiteFrames.Chosen(combination), true, DoneCaption(Spec(), combination, tried));
                return certificate;
            }
            rec.Reject(() => TripartiteFrames.Chosen(combination), () => TryCaption(Spec(), combination, accepted: false),
                () => Repeated(Spec(), combination).Select(r => r.Id).ToArray());
        }
        rec.Done(new ChosenTriples([]), false, tried == 1
            ? "There is only one way to pick the triples, and the verifier rejects it, so there is no perfect matching."
            : $"Checked all {TripartiteFrames.Count(tried)} ways to pick {n} of the {m} triples. The verifier rejects every one, so there is no perfect matching.");
        return "{}";
    }

    // The elements a set of triples uses more than once, in X, Y, Z order, with how many times.
    private static List<(string Id, int Uses)> Repeated(TripartiteSpec spec, List<int> combination) {
        var uses = TripartiteFrames.Uses(spec, combination);
        return Order(spec).Where(id => uses.GetValueOrDefault(id) > 1).Select(id => (id, uses[id])).ToList();
    }

    // The elements of Y and Z a set of triples leaves unused, in Y, Z order.
    private static List<string> Unused(TripartiteSpec spec, List<int> combination) {
        var uses = TripartiteFrames.Uses(spec, combination);
        return Order(spec).Where(id => id[0] != 'x' && !uses.ContainsKey(id)).ToList();
    }

    private static IEnumerable<string> Order(TripartiteSpec spec) =>
        spec.X.Select((_, i) => TripartiteFrames.XId(i))
            .Concat(spec.Y.Select((_, i) => TripartiteFrames.YId(i)))
            .Concat(spec.Z.Select((_, i) => TripartiteFrames.ZId(i)));

    private static string Names(TripartiteSpec spec, IEnumerable<string> ids, int most = 4) {
        var names = ids.Select(id => TripartiteFrames.ElementName(spec, id)).ToList();
        return names.Count <= most ? TripartiteFrames.AndList(names) : string.Join(", ", names.Take(most)) + $" and {names.Count - most} more";
    }

    // "Try T1, T2 and T5: Paul and Chloe are used twice."
    private static string TryCaption(TripartiteSpec spec, List<int> combination, bool accepted) {
        string which = TripartiteFrames.AndList(combination.Select(TripartiteFrames.TripleId).ToList());
        if (accepted)
            return Unused(spec, combination).Count == 0
                ? $"Try {which}: every element of X, Y and Z is used exactly once, so the verifier accepts it."
                : $"Try {which}: no element is used twice, so the verifier accepts it.";
        var repeated = Repeated(spec, combination);
        if (repeated.Count > 0) {
            // "a is used 3 times, and b and c twice": the most used first.
            var groups = repeated.GroupBy(r => r.Uses).OrderByDescending(g => g.Key).ToList();
            static string Times(int uses) => uses == 2 ? "twice" : $"{uses} times";
            var first = groups[0].Select(r => r.Id).ToList();
            string text = $"{Names(spec, first)} {(first.Count == 1 ? "is" : "are")} used {Times(groups[0].Key)}";
            foreach (var g in groups.Skip(1))
                text += $", and {Names(spec, g.Select(r => r.Id))} {Times(g.Key)}";
            return $"Try {which}: {text}.";
        }
        var unused = Unused(spec, combination);
        return unused.Count > 0
            ? $"Try {which}: no element is used twice, but {Names(spec, unused)} {(unused.Count == 1 ? "is" : "are")} never used, so the verifier rejects it."
            : $"Try {which}: no element is used twice, but the verifier rejects it.";
    }

    // "T2 (Paul, Austin, Jake), T4 (Sally, Madison, Frank) and T6 (Dave, Bob, Chloe) use every element of X, Y and Z exactly once."
    private static string DoneCaption(TripartiteSpec spec, List<int> combination, long tried) {
        string triples = TripartiteFrames.AndList(combination.Select(i => TripartiteFrames.TripleLabel(spec, i)).ToList());
        string after = tried == 1 ? "on the first try" : $"after {TripartiteFrames.Count(tried)} tries";
        var unused = Unused(spec, combination);
        return unused.Count == 0
            ? $"{triples} {(combination.Count == 1 ? "uses" : "use")} every element of X, Y and Z exactly once. Found {after}."
            : $"The verifier accepts {triples}, found {after}, though {Names(spec, unused)} {(unused.Count == 1 ? "is" : "are")} never used.";
    }

    private static string Plural(int n, string one) => $"{n} {(n == 1 ? one : one + "s")}";
}
