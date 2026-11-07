using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Graphs;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_SUBSETSUM;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.ReduceTo.NPC_SUBSETSUM;

// Karp's Exact Cover to Subset Sum: every set of S becomes a number in base |S| + 1, with a 1 in the digit of each element
// it holds, and the target T has a 1 in every digit. It records the construction steps (one per set, then T; see Build) and
// the backward map's steps (one per number of the Subset Sum instance; see MapBack).
class KarpExactCoverToSubsetSum : IReversibleReduction<EXACTCOVER, SUBSETSUM, ChosenSets>, IRecordedReduction<EXACTCOVER, SUBSETSUM> {

    // --- Fields ---
    public string reductionName { get; } = "Karp's Subset Sum Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Exact Cover to Subset Sum";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // The e[j,i] loop is |S| x |X|, and each of the |S| output numbers needs a full
    // |X|-digit base-(|S|+1) positional encoding regardless of how sparse each set is
    // — total output (string) size is O(|S| * |X|), even though the element COUNT of
    // the output (|S| numbers) is only O(|S|).
    public ReductionCost cost { get; } = ReductionCost.Quadratic;

    // Declared, not derived. The base-(|S|+1) positional encoding requires every
    // digit position to share a common base so sums can't carry into each other --
    // a global coordination constraint, not an independent per-element replacement.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    // Declared, not derived. The e[j,i] loop is |S| x |X|.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(|S| * |X|), S = subsets, X = universal set";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private EXACTCOVER _reductionFrom;
    private SUBSETSUM _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public EXACTCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public SUBSETSUM reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public KarpExactCoverToSubsetSum(EXACTCOVER from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private KarpExactCoverToSubsetSum(EXACTCOVER from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public KarpExactCoverToSubsetSum(string instance) : this(new EXACTCOVER(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public KarpExactCoverToSubsetSum(string instance, StepRecorder<BuildDelta> rec) : this(new EXACTCOVER(instance), rec) { }
    public KarpExactCoverToSubsetSum() : this(new EXACTCOVER()) { }

    /// <summary>
    /// The gadgets: per set of S, an element (Exact Cover's set S{j}, in the set problems' piece ids, to Subset Sum's item
    /// I{j}, its number); and the bound, every element of X (one digit each) to Subset Sum's container, whose target T has a 1 in
    /// every digit.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new KarpExactCoverToSubsetSum(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public SUBSETSUM reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show a set of S: the set in Exact Cover, the item (its number) it becomes in Subset Sum,
    // and the gadget that makes it. The state is (Set, Gadget): the set's index and its gadget's index.
    private static RunSteps<(int Set, int Gadget), BuildDelta> NewSetSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the set, its item, and the gadget.
        partial: s => new BuildDelta { SourceFocus = [SetPieces.SetId(s.Set)], Nodes = [PackingFrames.ItemId(s.Set)], Gadgets = [s.Gadget] },
        focus: s => [PackingFrames.ItemId(s.Set)]);

    // A number written in base d, one digit per element of X, its highest digit (the last element of X) first: a 1 where
    // the set holds the element, or in every digit when there is no set (the target T). With no elements it is "0".
    private static string Digits(List<string> x, List<string>? set) =>
        x.Count == 0 ? "0" : string.Concat(Enumerable.Range(0, x.Count).Reverse().Select(i => set == null || set.Contains(x[i]) ? '1' : '0'));

    // Builds the Subset Sum instance, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept per set of S, in order: the set becomes a number in base |S| + 1 with a 1 in the digit of each element
    //     it holds;
    //   - the Done: the target T has a 1 in every digit.
    // A construction has no verdicts, so Accept means "these pieces go into the target". The steps come from the very
    // decisions below, so what is recorded is what is built.
    // Gadgets: an element per set of S (the set to its item), then the bound (T).
    private SUBSETSUM Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        SUBSETSUM reducedSUBSETSUM = new SUBSETSUM();

        // Each set is a number in base d with one digit per element of X: a 1 where the set holds that element. d = |S| + 1
        // keeps the digits apart however many sets are added (a digit sum never reaches d, so nothing carries); with no sets
        // any base does, and base 1 has no digits, so it is at least 2.
        int r = reductionFrom.S.Count;
        int d = Math.Max(r + 1, 2);
        int digits = reductionFrom.X.Count;

        // The numbers grow as d^|X|; Subset Sum holds ints. T, the sum of every digit's 1, is the largest number written and
        // bounds every sum a solver or verifier adds up, so the instance is refused (400) when T does not fit.
        long K;
        var place = new long[digits];
        try {
            long power = 1;
            K = 0;
            for (int i = 0; i < digits; i++) {
                place[i] = power;
                K = checked(K + power);
                if (i < digits - 1) power = checked(power * d);
            }
        } catch (OverflowException) {
            throw TooBig(digits, d);
        }
        if (K > int.MaxValue) throw TooBig(digits, d);

        List<string> subsetSumS = new List<string>();
        for (int j = 0; j < reductionFrom.S.Count; j++) {
            long sum = 0;
            for (int i = 0; i < reductionFrom.X.Count; i++) {
                if (reductionFrom.S[j].Contains(reductionFrom.X[i])) sum += place[i];
            }
            subsetSumS.Add(sum.ToString());
        }

        string instance = "({" + string.Join(",", subsetSumS) + "}," + K + ")";

        reducedSUBSETSUM.S = subsetSumS;
        reducedSUBSETSUM.T = (int)K;
        reducedSUBSETSUM.instance = instance;

        // One gadget per set, from the set to its item; each set is then one step.
        var X = reductionFrom.X;
        var setSteps = NewSetSteps(rec);
        for (int j = 0; j < reductionFrom.S.Count; j++) {
            string set = SetPieces.SetId(j);
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { set }, new List<string> { PackingFrames.ItemId(j) }));
            setSteps.Accept((j, gadget),
                $"Set {set} {SetPieces.Braces(reductionFrom.S[j])} becomes the number {subsetSumS[j]}: {Digits(X, reductionFrom.S[j])} in base {d}, one digit per element of X{(digits == 0 ? "" : $" ({BuildCaptions.Names(Enumerable.Range(0, digits).Reverse().Select(i => X[i]).ToList())} from the left)")}, a 1 where {set} holds that element.");
        }
        // Then the bound: every element of X (one digit each) to Subset Sum's container, whose target T has a 1 in every digit.
        int bound = gadgets.Count;
        string container = PackingFrames.ContainerId(0);
        var elementIds = X.Select(SetPieces.ElementId).ToList();
        gadgets.Add(new Gadget(GadgetKind.Bound, elementIds, new List<string> { container }, note: $"T = {K}, a 1 in each of the {digits} digits"));

        // The closing step.
        int t = (int)K;
        setSteps.Done(new BuildDelta { SourceFocus = elementIds.ToArray(), Nodes = [container], Gadgets = [bound], Bound = t }, true,
            $"The target T is {Digits(X, null)} in base {d} = {t}: a 1 for every element of X. At most {r} sets add to any digit and {r} < {d}, so nothing carries, and numbers add up to T exactly when their sets hold every element once.");

        reductionTo = reducedSUBSETSUM;
        return reducedSUBSETSUM;
    }

    private ReductionInputException TooBig(int digits, int d) =>
        new(this, _reductionFrom.instance, _reductionFrom.instanceFormat,
            $"the sets become numbers with {digits} digits in base {d} (one digit per element of X, base |S| + 1), and the target T = 11...1 in base {d} is past Subset Sum's largest integer ({int.MaxValue}); use fewer elements or sets");

    /// <summary>
    /// Maps an Exact Cover answer to a Subset Sum answer: each chosen set becomes its number. The sets of an exact
    /// cover hold every element once, so their numbers have a 1 in every digit and add up to T. A set S does not have is a 400.
    /// </summary>
    public string mapSolutions(string reductionFromSolution) {
        var used = new bool[reductionFrom.S.Count];
        var numbers = new List<string>();
        foreach (var listed in ReadSets(reductionFromSolution)) {
            int j = Enumerable.Range(0, reductionFrom.S.Count).FirstOrDefault(k => !used[k] && reductionFrom.S[k].ToHashSet(StringComparer.Ordinal).SetEquals(listed), -1);
            if (j < 0)
                throw new ReductionInputException(this, reductionFromSolution, reductionFrom.certificateFormat, $"'{{{string.Join(",", listed)}}}' is not a set of S");
            used[j] = true;
            numbers.Add(reductionTo.S[j]);
        }
        return "{" + string.Join(",", numbers) + "}";
    }

    public ChosenSets EmptyAnswer() => new ChosenSets([]);

    // "{}" on either side keeps the default (the verifiers): with X empty, T = 0, and the empty subset and the empty cover are
    // both answers.

    // How the map-back steps show an item read: the sets of the exact cover so far, and the item.
    private static RunSteps<int, ChosenSets> NewMapSteps(StepRecorder<ChosenSets> rec, List<string> picked) => new(rec,
        // What every picture shows: the cover so far, and the item the step is about.
        partial: _ => new ChosenSets(picked),
        focus: j => [PackingFrames.ItemId(j)]);

    // Backward map: item I{j} is the number of the j-th set of S, so the numbers of the subset are the sets of the exact
    // cover.
    // Its steps, one per item of the Subset Sum instance in order: an Accept when its number is in the subset (its set is in
    // the cover), a Reject when it is not (its set is left out). The caller adds the Done.
    public (string Answer, ChosenSets Partial) MapBack(string problemToSolution, StepRecorder<ChosenSets> rec) {
        var keys = reductionTo.S.Select(PackingFrames.Key).ToList();
        var groups = ReadNumbers(problemToSolution);
        foreach (string number in groups)
            if (!keys.Contains(number)) throw new FormatException($"'{number}' is not a number of the Subset Sum instance");
        Placement chosen = PackingFrames.FromGroups(keys, [groups]);
        var picked = new List<string>();
        var steps = NewMapSteps(rec, picked);
        for (int j = 0; j < keys.Count; j++) {
            string set = SetPieces.SetId(j), number = reductionTo.S[j];
            if (chosen.Of(j) == 0) {
                picked.Add(set);
                steps.Accept(j, $"Number {number} is in the subset, so set {set} {SetPieces.Braces(reductionFrom.S[j])} is in the exact cover: {number} is {set} written in base {Math.Max(reductionFrom.S.Count + 1, 2)}.");
            } else {
                steps.Reject(j, $"Number {number} is not in the subset, so set {set} {SetPieces.Braces(reductionFrom.S[j])} is left out of the exact cover.");
            }
        }
        var cover = Enumerable.Range(0, keys.Count).Where(j => chosen.Of(j) == 0).Select(j => SetPieces.Braces(reductionFrom.S[j]));
        return ("{" + string.Join(",", cover) + "}", new ChosenSets(picked));
    }

    // A Subset Sum answer, {21,65}: the chosen numbers. Malformed text throws FormatException, which the endpoint reports as a 400.
    private static List<string> ReadNumbers(string certificate) {
        string text = certificate.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new FormatException($"'{certificate}' is not a set of numbers in braces like {{21,65}}");
        try {
            // Repeats kept: two sets of S can be the same number.
            return RepeatedElements.Parse(text).Select(PackingFrames.Key).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not a set of numbers like {{21,65}}", ex);
        }
    }

    // An Exact Cover answer, {{2,3},{4,1}}: the chosen sets by their elements.
    private List<HashSet<string>> ReadSets(string certificate) {
        string text = certificate.Trim();
        try {
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}') throw new FormatException();
            return new SPADE.UtilCollection(text).ToList().Select(s => s.ToList().Select(e => e.ToString()).ToHashSet(StringComparer.Ordinal)).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(this, certificate, reductionFrom.certificateFormat, $"'{certificate}' is not a set of sets like {{{{2,3}},{{4,1}}}}", ex);
        }
    }

}
