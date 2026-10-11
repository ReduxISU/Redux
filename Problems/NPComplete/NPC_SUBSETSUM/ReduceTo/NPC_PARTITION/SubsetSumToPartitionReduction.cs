using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_PARTITION;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;

class SubsetSumToPartitionReduction : IReversibleReduction<SUBSETSUM, PARTITION, Placement>, IRecordedReduction<SUBSETSUM, PARTITION> {

    // --- Fields ---
    public string reductionName { get; } = "Partition Reduction";
    public string reductionDefinition { get; } = "Karp's Reduction from Subset Sum to Partition";
    public string source { get; } = "Karp, Richard M. Reducibility among combinatorial problems. Complexity of computer computations. Springer, Boston, MA, 1972. 85-103.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://cgi.di.uoa.gr/~sgk/teaching/grad/handouts/karp.pdf";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };
    // reduce() copies S (one pass) and appends exactly 2 derived numbers — O(n).
    public ReductionCost cost { get; } = ReductionCost.Linear;

    // Declared, not derived. Reuses S essentially unchanged, appending two padding
    // numbers to force the partition point -- a minimal augmentation, not a gadget
    // construction.
    public ReductionType reductionType { get; } = ReductionType.Restriction;
    // Declared, not derived. One pass copying S plus two appended derived numbers.
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Linear;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n), n = |SUBSETSUM.S|";
    private Dictionary<Object, Object> _gadgetMap = new Dictionary<Object, Object>();

    private SUBSETSUM _reductionFrom;
    private PARTITION _reductionTo;


    // --- Properties ---
    public Dictionary<Object, Object> gadgetMap {
        get {
            return _gadgetMap;
        }
        set {
            _gadgetMap = value;
        }
    }
    public SUBSETSUM reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public PARTITION reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }



    // --- Methods Including Constructors ---
    public SubsetSumToPartitionReduction(SUBSETSUM from) : this(from, StepRecorder<BuildDelta>.Off) { }

    // The one place the target is built; rec is Off except when the construction steps are asked for.
    private SubsetSumToPartitionReduction(SUBSETSUM from, StepRecorder<BuildDelta> rec) {
        _reductionFrom = from;
        _reductionTo = Build(rec);

    }
    public SubsetSumToPartitionReduction(string instance) : this(new SUBSETSUM(instance)) { }
    // Builds from the instance with rec recording the construction as it goes (IRecordedReduction).
    public SubsetSumToPartitionReduction(string instance, StepRecorder<BuildDelta> rec) : this(new SUBSETSUM(instance), rec) { }
    public SubsetSumToPartitionReduction() : this(new SUBSETSUM()) { }

    /// <summary>
    /// The gadgets (#695), in the Packing picture's ids on both sides: per number of S, an element (Subset Sum's item I{i} to
    /// Partition's item I{i}); the two added numbers, palette pieces made from Subset Sum's target (its container C1); and the
    /// bound, that container to Partition's two sides, which each hold sum + 1.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new SubsetSumToPartitionReduction(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public PARTITION reduce() => Build(StepRecorder<BuildDelta>.Off);

    // Construction steps (see IRecordedReduction): one per number of S, one per added number, then the two sides.
    private PARTITION Build(StepRecorder<BuildDelta> rec) {
        // Built afresh on every call, so calling reduce() again does not add a second copy of each gadget.
        gadgets.Clear();
        SUBSETSUM SUBSETSUMInstance = _reductionFrom;
        PARTITION reducedPARTITION = new PARTITION();

        // The two added numbers, T + 1 and sum - T + 1, worked out in long so a large S or T cannot wrap around.
        long sum = SUBSETSUMInstance.S.Sum(x => (long)int.Parse(x));
        long withT = (long)SUBSETSUMInstance.T + 1, withRest = sum - SUBSETSUMInstance.T + 1;
        if (withT > int.MaxValue || withT < int.MinValue || withRest > int.MaxValue || withRest < int.MinValue)
            throw new ReductionInputException(this, SUBSETSUMInstance.instance, SUBSETSUMInstance.instanceFormat,
                $"the added numbers T + 1 = {withT} and sum - T + 1 = {withRest} must fit Partition's integers (at most {int.MaxValue})");
        // Partition reads its numbers as a set, so a number written twice is read once (#628) and the Partition would not be
        // the one built here: its answer could differ from Subset Sum's (#831). Until Partition keeps repeated numbers, such
        // an instance is refused rather than reduced to a different question.
        var numbers = SUBSETSUMInstance.S.Select(int.Parse).ToList();
        string? repeat = numbers.Contains((int)withT) ? $"T + 1 = {withT} is already a number of S"
            : numbers.Contains((int)withRest) ? $"sum - T + 1 = {withRest} is already a number of S"
            : withT == withRest ? $"T + 1 and sum - T + 1 are both {withT}"
            : null;
        if (repeat != null)
            throw new ReductionInputException(this, SUBSETSUMInstance.instance, SUBSETSUMInstance.instanceFormat,
                $"{repeat}, and Partition reads a repeated number once (#628), so the Partition would not have the same answer as this Subset Sum");

        List<string> partitionNumbers = new List<string>(SUBSETSUMInstance.S) { withT.ToString(), withRest.ToString() };
        string instance = "{" + string.Join(",", partitionNumbers) + "}";

        reducedPARTITION.S = partitionNumbers;
        reducedPARTITION.instance = instance;

        int n = SUBSETSUMInstance.S.Count, t = SUBSETSUMInstance.T;
        string target = PackingFrames.ContainerId(0);
        for (int i = 0; i < n; i++) {
            string id = PackingFrames.ItemId(i), number = SUBSETSUMInstance.S[i];
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { id }, new List<string> { id }));
            rec.Accept(() => new BuildDelta { SourceFocus = [id], Nodes = [id], Gadgets = [gadget] },
                () => $"Number {number} of S becomes the number {number} of Partition, unchanged.", id);
        }
        string tItem = PackingFrames.ItemId(n), restItem = PackingFrames.ItemId(n + 1);
        int tGadget = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Palette, new List<string> { target }, new List<string> { tItem }, note: $"T + 1 = {withT}"));
        int restGadget = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Palette, new List<string> { target }, new List<string> { restItem }, note: $"sum - T + 1 = {withRest}"));
        int sides = gadgets.Count;
        long half = sum + 1;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string> { target }, new List<string> { PackingFrames.ContainerId(0), PackingFrames.ContainerId(1) },
            note: $"each side holds sum + 1 = {half}"));
        if (rec.Enabled) {
            rec.Accept(() => new BuildDelta { SourceFocus = [target], Nodes = [tItem], Gadgets = [tGadget] },
                () => $"Partition gets the number T + 1 = {withT}. On its side it needs numbers of S adding up to (sum + 1) - (T + 1) = {sum - t}: the ones left out of a subset that adds up to T = {t}.",
                tItem);
            rec.Accept(() => new BuildDelta { SourceFocus = [target], Nodes = [restItem], Gadgets = [restGadget] },
                () => $"Partition gets the number sum - T + 1 = {withRest}. On its side it needs numbers of S adding up to (sum + 1) - (sum - T + 1) = {t}: a subset that adds up to T.",
                restItem);
            rec.Done(new BuildDelta { SourceFocus = [target], Nodes = [PackingFrames.ContainerId(0), PackingFrames.ContainerId(1)], Gadgets = [sides] }, true,
                $"Partition's numbers add up to 2 * {sum} + 2 = {2 * sum + 2}, so each side must hold {half}. The two added numbers add up to {withT + withRest}, more than a side holds, so they always sit on different sides.");
        }

        reductionTo = reducedPARTITION;
        return reducedPARTITION;
    }

    /// <summary>
    /// Maps a Subset Sum answer to a Partition answer (#817): the chosen numbers, which add up to T, go on one side with
    /// sum - T + 1, and the rest of S, which adds up to sum - T, goes on the other with T + 1, so both sides add up to sum + 1.
    /// A number S does not have (or has fewer times than the answer uses it) is a 400.
    /// </summary>
    public string mapSolutions(string problemFromSolution) {
        string text = problemFromSolution.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{problemFromSolution}' is not a set of numbers like {{1,12,15}}");
        List<string> chosen;
        try {
            chosen = new SPADE.UtilCollection(text).ToList().Select(x => x.ToString()).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{problemFromSolution}' is not a set of numbers like {{1,12,15}}", ex);
        }
        var rest = new List<string>(reductionFrom.S);
        foreach (string number in chosen) {
            if (!rest.Remove(number))
                throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{number}' is not a number of S");
        }
        int count = reductionTo.S.Count;
        chosen.Add(reductionTo.S[count - 1]);
        rest.Add(reductionTo.S[count - 2]);
        // Written as Partition Brute Force writes its answers, which both Partition's verifier and its visualization read.
        return "{(" + string.Join(",", chosen) + "),(" + string.Join(",", rest) + ")}";
    }

    public Placement EmptyAnswer() => new Placement([]);

    // "{}" on either side keeps the default (the verifiers): Partition never takes "{}" as a split, and the empty subset is an
    // answer exactly when T = 0.

    /// <summary>
    /// Backward map (#695): the numbers of S on the same side as sum - T + 1 add up to (sum + 1) - (sum - T + 1) = T, so they
    /// are the subset. The first step reads where sum - T + 1 is; then one step per number of S, in order: on that side
    /// (Accept, in the subset) or on the other, with T + 1 (Reject, left out).
    /// </summary>
    public (string Answer, Placement Partial) MapBack(string problemToSolution, StepRecorder<Placement> rec) {
        int n = reductionFrom.S.Count;
        var keys = reductionTo.S.Select(PackingFrames.Key).ToList();
        Placement split = PackingFrames.FromGroups(keys, ReadSides(problemToSolution));
        int restSide = split.Of(n + 1);
        if (restSide < 0)
            throw new FormatException($"'{problemToSolution}' does not put the number {reductionTo.S[n + 1]} (sum - T + 1) on either side");
        string restNumber = reductionTo.S[n + 1], tNumber = reductionTo.S[n];
        var assign = Enumerable.Repeat(-1, n).ToArray();
        rec.Try(() => new Placement(assign),
            () => $"Number {restNumber} (sum - T + 1) is on side S{restSide + 1}, so the numbers of S beside it add up to T: they are the subset.",
            PackingFrames.ItemId(n + 1));
        for (int i = 0; i < n; i++) {
            string number = reductionFrom.S[i], id = PackingFrames.ItemId(i);
            int side = split.Of(i);
            if (side == restSide) {
                assign[i] = 0;
                rec.Accept(() => new Placement(assign), () => $"Number {number} is on side S{side + 1} with {restNumber}, so {number} is in the subset.", id);
            } else {
                rec.Reject(() => new Placement(assign),
                    () => side < 0 ? $"Number {number} is on neither side, so {number} is left out of the subset."
                        : $"Number {number} is on side S{side + 1} with {tNumber} (T + 1), so {number} is left out of the subset.", id);
            }
        }
        var subset = Enumerable.Range(0, n).Where(i => assign[i] == 0).Select(i => reductionFrom.S[i]);
        return ("{" + string.Join(",", subset) + "}", new Placement(assign));
    }

    // A Partition answer: two sides, "{(1,12,15,8),(7,29)}" or "(1,12,15,8),(7,29)", as Partition's verifier reads them.
    // Malformed text throws FormatException, which the endpoint reports as a 400.
    private static List<List<string>> ReadSides(string certificate) {
        SPADE.UtilCollection pair;
        try {
            try {
                pair = new SPADE.UtilCollection(certificate.Trim());
                pair.assertCount(2);
            } catch {
                pair = new SPADE.UtilCollection("(" + certificate.Trim() + ")");
                pair.assertCount(2);
            }
            return pair.ToList().Select(side => side.ToList().Select(PackingFrames.Key).ToList()).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new FormatException($"'{certificate}' is not two sides like {{(1,12,15,8),(7,29)}}", ex);
        }
    }
}
