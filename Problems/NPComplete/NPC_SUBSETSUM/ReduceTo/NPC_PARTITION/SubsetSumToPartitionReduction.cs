using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.JSON_Objects;
using API.Interfaces.Packing;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_PARTITION;
using API.Problems.NPComplete.NPC_PARTITION.Verifiers;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;

// Subset Sum to Partition: the numbers of S, unchanged, plus two added numbers, T + 1 and sum - T + 1, so each side of an
// even split holds sum + 1 and the numbers of S beside sum - T + 1 add up to T. It records the construction steps (one per
// number of S, one per added number, then the sides; see Build) and the backward map's steps (where sum - T + 1 is, then
// one per number of S; see MapBack).
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
    /// The gadgets, in the Packing picture's ids on both sides: per number of S, an element (Subset Sum's item I{i} to
    /// Partition's item I{i}); the two added numbers, palette pieces made from Subset Sum's target (its container C1); and the
    /// bound, that container to Partition's two sides, which each hold sum + 1.
    /// </summary>
    public List<Gadget> gadgets { get; } = new();

    public ConstructionRun Rebuild(StepRecorder<BuildDelta> rec) {
        var copy = new SubsetSumToPartitionReduction(reductionFrom, rec);
        return new ConstructionRun(copy.reductionTo, copy.gadgets, rec.Steps);
    }

    public PARTITION reduce() => Build(StepRecorder<BuildDelta>.Off);

    // How the construction steps show a number of S: the number in Subset Sum, the same number in Partition, and the gadget
    // that copies it. The state is (Item, Gadget): the number's item id (the same on both sides) and its gadget's index.
    private static RunSteps<(string Item, int Gadget), BuildDelta> NewNumberSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the number on both sides, and the gadget.
        partial: s => new BuildDelta { SourceFocus = [s.Item], Nodes = [s.Item], Gadgets = [s.Gadget] },
        focus: s => [s.Item]);

    // How the construction steps show an added number: Subset Sum's target (its container), the number added to Partition,
    // and the gadget that makes it from the target. The state is (Item, Gadget): the added number's item id and its gadget's
    // index.
    private static RunSteps<(string Item, int Gadget), BuildDelta> NewAddedSteps(StepRecorder<BuildDelta> rec) => new(rec,
        // What the Reduction View shows: the target, the added number, and the gadget.
        partial: s => new BuildDelta { SourceFocus = [PackingFrames.ContainerId(0)], Nodes = [s.Item], Gadgets = [s.Gadget] },
        focus: s => [s.Item]);

    // Builds the Partition instance, recording the construction steps as it goes (see IRecordedReduction). Its steps:
    //   - an Accept per number of S, in order: it is a number of Partition, unchanged;
    //   - an Accept for each added number, T + 1 and then sum - T + 1;
    //   - the Done: each side must hold sum + 1, and the two added numbers always sit on different sides.
    // A construction has no verdicts, so Accept means "these pieces go into the target". The steps come from the very
    // decisions below, so what is recorded is what is built.
    // Gadgets: an element per number of S, a palette piece per added number (from the target), then the bound (the sides).
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
        // T + 1 or sum - T + 1 can be a number S already has, or both can be the same number (T = sum / 2): Partition keeps
        // every copy of a repeated number, so such a Partition is still the one built here and has the same answer, and
        // nothing is refused.

        List<string> partitionNumbers = new List<string>(SUBSETSUMInstance.S) { withT.ToString(), withRest.ToString() };
        string instance = "{" + string.Join(",", partitionNumbers) + "}";

        reducedPARTITION.S = partitionNumbers;
        reducedPARTITION.instance = instance;

        // One gadget per number of S, copying it; each number is then one step.
        int n = SUBSETSUMInstance.S.Count, t = SUBSETSUMInstance.T;
        string target = PackingFrames.ContainerId(0);
        var numberSteps = NewNumberSteps(rec);
        for (int i = 0; i < n; i++) {
            string id = PackingFrames.ItemId(i), number = SUBSETSUMInstance.S[i];
            int gadget = gadgets.Count;
            gadgets.Add(new Gadget(GadgetKind.Element, new List<string> { id }, new List<string> { id }));
            numberSteps.Accept((id, gadget), $"Number {number} of S becomes the number {number} of Partition, unchanged.");
        }
        // The two added numbers, each made from the target T, and one step each.
        var addedSteps = NewAddedSteps(rec);
        string tItem = PackingFrames.ItemId(n), restItem = PackingFrames.ItemId(n + 1);
        int tGadget = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Palette, new List<string> { target }, new List<string> { tItem }, note: $"T + 1 = {withT}"));
        addedSteps.Accept((tItem, tGadget),
            $"Partition gets the number T + 1 = {withT}. On its side it needs numbers of S adding up to (sum + 1) - (T + 1) = {sum - t}: the ones left out of a subset that adds up to T = {t}.");
        int restGadget = gadgets.Count;
        gadgets.Add(new Gadget(GadgetKind.Palette, new List<string> { target }, new List<string> { restItem }, note: $"sum - T + 1 = {withRest}"));
        addedSteps.Accept((restItem, restGadget),
            $"Partition gets the number sum - T + 1 = {withRest}. On its side it needs numbers of S adding up to (sum + 1) - (sum - T + 1) = {t}: a subset that adds up to T.");
        // Then the bound: the target to Partition's two sides, which each hold sum + 1.
        int sides = gadgets.Count;
        long half = sum + 1;
        gadgets.Add(new Gadget(GadgetKind.Bound, new List<string> { target }, new List<string> { PackingFrames.ContainerId(0), PackingFrames.ContainerId(1) },
            note: $"each side holds sum + 1 = {half}"));

        // The closing step, on either recorder (both record to rec).
        addedSteps.Done(new BuildDelta { SourceFocus = [target], Nodes = [PackingFrames.ContainerId(0), PackingFrames.ContainerId(1)], Gadgets = [sides] }, true,
            $"Partition's numbers add up to 2 * {sum} + 2 = {2 * sum + 2}, so each side must hold {half}. The two added numbers add up to {withT + withRest}, more than a side holds, so they always sit on different sides.");

        reductionTo = reducedPARTITION;
        return reducedPARTITION;
    }

    /// <summary>
    /// Maps a Subset Sum answer to a Partition answer: the chosen numbers, which add up to T, go on one side with
    /// sum - T + 1, and the rest of S, which adds up to sum - T, goes on the other with T + 1, so both sides add up to sum + 1.
    /// A number S does not have (or has fewer times than the answer uses it) is a 400.
    /// </summary>
    public string mapSolutions(string problemFromSolution) {
        string text = problemFromSolution.Trim();
        if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{problemFromSolution}' is not a set of numbers like {{1,12,15}}");
        List<string> chosen;
        try {
            // Repeats kept: {7,7} chooses two 7s.
            chosen = RepeatedElements.Parse(text).Select(x => x.ToString()).ToList();
        } catch (Exception ex) when (ParseGuard.IsCertificateParseFailure(ex)) {
            throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{problemFromSolution}' is not a set of numbers like {{1,12,15}}", ex);
        }
        // Each chosen number takes a copy of it in S not yet taken, by value, so a number can be chosen as many times as S has
        // it and no more.
        var rest = new List<string>(reductionFrom.S);
        foreach (string number in chosen) {
            int at = int.TryParse(number, out int value) ? rest.FindIndex(x => int.Parse(x) == value) : -1;
            if (at < 0)
                throw new ReductionInputException(this, problemFromSolution, reductionFrom.certificateFormat, $"'{number}' is not a number of S");
            rest.RemoveAt(at);
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

    // How the map-back steps show a number read: the subset so far (the numbers of S in it), and the number's item.
    private static RunSteps<string, Placement> NewMapSteps(StepRecorder<Placement> rec, int[] assign) => new(rec,
        // What every picture shows: the subset so far, and the number the step is about.
        partial: _ => new Placement(assign),
        focus: id => [id]);

    // Backward map: the numbers of S on the same side as sum - T + 1 add up to (sum + 1) - (sum - T + 1) = T, so they are
    // the subset.
    // Its steps: a Try that reads which side sum - T + 1 is on; then one per number of S, in order: an Accept when it is on
    // that side (it is in the subset), a Reject when it is on the other side, with T + 1, or on neither (it is left out).
    // The caller adds the Done.
    public (string Answer, Placement Partial) MapBack(string problemToSolution, StepRecorder<Placement> rec) {
        int n = reductionFrom.S.Count;
        var keys = reductionTo.S.Select(PackingFrames.Key).ToList();
        Placement split = PackingFrames.FromGroups(keys, ReadSides(problemToSolution));
        int restSide = split.Of(n + 1);
        if (restSide < 0)
            throw new FormatException($"'{problemToSolution}' does not put the number {reductionTo.S[n + 1]} (sum - T + 1) on either side");
        string restNumber = reductionTo.S[n + 1], tNumber = reductionTo.S[n];
        var assign = Enumerable.Repeat(-1, n).ToArray();
        var steps = NewMapSteps(rec, assign);
        steps.Try(PackingFrames.ItemId(n + 1),
            $"Number {restNumber} (sum - T + 1) is on side S{restSide + 1}, so the numbers of S beside it add up to T: they are the subset.");
        for (int i = 0; i < n; i++) {
            string number = reductionFrom.S[i], id = PackingFrames.ItemId(i);
            int side = split.Of(i);
            if (side == restSide) {
                assign[i] = 0;
                steps.Accept(id, $"Number {number} is on side S{side + 1} with {restNumber}, so {number} is in the subset.");
            } else if (side < 0) {
                steps.Reject(id, $"Number {number} is on neither side, so {number} is left out of the subset.");
            } else {
                steps.Reject(id, $"Number {number} is on side S{side + 1} with {tNumber} (T + 1), so {number} is left out of the subset.");
            }
        }
        var subset = Enumerable.Range(0, n).Where(i => assign[i] == 0).Select(i => reductionFrom.S[i]);
        return ("{" + string.Join(",", subset) + "}", new Placement(assign));
    }

    // A Partition answer: two sides, "{(1,12,15,8),(7,29)}" or "(1,12,15,8),(7,29)", as Partition's verifier reads them.
    // Malformed text throws FormatException, which the endpoint reports as a 400.
    // Read as Partition's verifier reads it, repeats included: {(10,4,5),(10,3,6)} has a 10 on each side.
    private static List<List<string>> ReadSides(string certificate) {
        List<List<string>>? sides = PartitionVerifier.ReadSides(certificate.Trim());
        if (sides == null)
            throw new FormatException($"'{certificate}' is not two sides like {{(1,12,15,8),(7,29)}}");
        return sides.Select(side => side.Select(PackingFrames.Key).ToList()).ToList();
    }
}
