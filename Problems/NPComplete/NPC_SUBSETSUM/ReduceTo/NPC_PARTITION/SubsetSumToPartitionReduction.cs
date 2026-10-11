using System.Text.Json.Serialization;
using API.Interfaces;
using API.Problems.NPComplete.NPC_PARTITION;

namespace API.Problems.NPComplete.NPC_SUBSETSUM.ReduceTo.NPC_PARTITION;

class SubsetSumToPartitionReduction : IReduction<SUBSETSUM, PARTITION> {

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
    public SubsetSumToPartitionReduction(SUBSETSUM from) {
        _reductionFrom = from;
        _reductionTo = reduce();

    }
    public SubsetSumToPartitionReduction(string instance) : this(new SUBSETSUM(instance)) { }
    public SubsetSumToPartitionReduction() : this(new SUBSETSUM()) { }
    public PARTITION reduce() {
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
}
