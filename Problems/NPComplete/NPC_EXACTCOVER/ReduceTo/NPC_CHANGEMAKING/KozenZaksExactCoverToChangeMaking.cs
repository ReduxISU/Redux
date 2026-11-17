using System.Text.Json.Serialization;
using API.Interfaces;
using API.Interfaces.Steps;
using API.Problems.NPComplete.NPC_CHANGEMAKING;
using SPADE;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.ReduceTo.NPC_CHANGEMAKING;

class KozenZaksExactCoverToChangeMaking : IReversibleReduction<EXACTCOVER, CHANGEMAKING, ChosenSets> {

    // --- Fields ---
    public string reductionName { get; } = "Kozen-Zaks Change-Making Reduction";
    public string reductionDefinition { get; } = "Number the elements of U as 0..n-1 and let p = n + 1. Each nonempty set A in S becomes the coin c_A = sum of p^i over the elements i of A, the target is X = sum of p^i for i = 0..n-1 (every element once), and the coin limit is K = n. An exact cover uses at most n sets, and its coins add up to X. Conversely, with at most n coins no base-p digit can exceed n < p, so nothing carries, and matching X digit by digit forces every element to be covered exactly once. Adapted from the exact-cover-by-3-sets encoding in Kozen and Zaks' Theorem 5.1, which drops the extra 1 in each coin, the penny and the coin c_X that their greedy question needs.";
    public string source { get; } = "Kozen, Dexter and Zaks, Shmuel. Optimal bounds for the change-making problem. Theoretical Computer Science 123(2), 1994. 377-388. Theorem 5.1.";
    public string sourceFile { get; } = SourceFile.Path();
    public string sourceLink { get; } = "https://www.cs.cornell.edu/~kozen/Papers/change";
    public string[] contributors { get; } = { "Jason L. Wright" };
    // Each of the |S| coins is a base-(n+1) number with n digits, so the written output is O(|S| * n)
    // digits even though it has only |S| + 2 numbers.
    public ReductionCost cost { get; } = ReductionCost.Quadratic;
    // Declared, not derived. One shared base keeps the digit positions from carrying into each other:
    // a global coordination constraint, as in Karp's Exact Cover to Subset Sum.
    public ReductionType reductionType { get; } = ReductionType.ComponentDesign;
    public ReductionComplexityBucket complexityBucket { get; } = ReductionComplexityBucket.Polynomial;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(|S| * |U|), S = subsets, U = universe";

    // Instance used when U is empty: the empty subcollection is an exact cover, and Change-Making needs
    // X >= 1, so any yes-instance will do.
    private const string EmptyUniverseInstance = "({1},1,1)";

    private EXACTCOVER _reductionFrom;
    private CHANGEMAKING _reductionTo;
    // coins[j] is the coin for S[j], or 0 for an empty set (which gets no coin).
    private List<int> _coins = new List<int>();

    // --- Properties ---
    public EXACTCOVER reductionFrom {
        get {
            return _reductionFrom;
        }
        set {
            _reductionFrom = value;
        }
    }
    public CHANGEMAKING reductionTo {
        get {
            return _reductionTo;
        }
        set {
            _reductionTo = value;
        }
    }

    // --- Methods Including Constructors ---
    public KozenZaksExactCoverToChangeMaking(EXACTCOVER from) {
        _reductionFrom = from;
        _reductionTo = reduce();
    }
    public KozenZaksExactCoverToChangeMaking(string instance) : this(new EXACTCOVER(instance)) { }
    public KozenZaksExactCoverToChangeMaking() : this(new EXACTCOVER()) { }

    public CHANGEMAKING reduce() {
        List<string> universe = _reductionFrom.X.Distinct().ToList();
        int n = universe.Count;
        _coins = new List<int>();

        if (n == 0) {
            _coins.AddRange(_reductionFrom.S.Select(_ => 0));
            _reductionTo = new CHANGEMAKING(EmptyUniverseInstance);
            return _reductionTo;
        }

        Dictionary<string, int> position = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
            position[universe[i]] = i;

        long p = n + 1;
        long x;
        List<long> coins = new List<long>();
        try {
            // The largest power is p^(n-1); X is the largest number produced, so it alone must fit.
            long[] power = new long[n];
            power[0] = 1;
            for (int i = 1; i < n; i++)
                power[i] = checked(power[i - 1] * p);
            x = 0;
            foreach (long pw in power)
                x = checked(x + pw);

            foreach (List<string> set in _reductionFrom.S)
                coins.Add(set.Distinct().Sum(element => power[position[element]]));
        } catch (OverflowException) {
            x = long.MaxValue;
        }
        if (x > int.MaxValue)
            throw new ReductionInputException(this, _reductionFrom.instance, _reductionFrom.instanceFormat,
                $"|U| = {n} is too large: the target sum of {p}^i for i < {n} does not fit in a Change-Making instance (at most 9 elements)");

        _coins = coins.Select(c => (int)c).ToList();
        string instance = "({" + string.Join(",", _coins.Where(c => c > 0)) + "}," + x + "," + n + ")";
        _reductionTo = new CHANGEMAKING(instance);
        return _reductionTo;
    }

    // Forward map: each set of the cover becomes one of its coin.
    public string mapSolutions(string problemFromSolution) {
        List<List<string>> cover;
        try {
            // StringParser, not a bare UtilCollection, which accepts unbalanced text such as "{{".
            StringParser parser = new("{T | T is set}");
            parser.parse(problemFromSolution);
            cover = parser["T"].ToList()
                .Select(set => set.ToList().Select(element => element.ToString()).ToList()).ToList();
        } catch (Exception ex) {
            throw new ReductionInputException(this, problemFromSolution, _reductionFrom.certificateFormat, ex.Message, ex);
        }

        if (_reductionFrom.X.Count == 0)
            return "{(1,1)}";

        Dictionary<int, int> counts = new Dictionary<int, int>();
        foreach (List<string> set in cover) {
            int j = IndexOfSet(set);
            if (j < 0)
                throw new ReductionInputException(this, problemFromSolution, _reductionFrom.certificateFormat,
                    $"{{{string.Join(",", set)}}} is not a set in S");
            if (_coins[j] == 0) continue;
            counts[_coins[j]] = counts.GetValueOrDefault(_coins[j]) + 1;
        }
        return "{" + string.Join(",", counts.Select(kv => $"({kv.Key},{kv.Value})")) + "}";
    }

    public ChosenSets EmptyAnswer() => new ChosenSets([]);

    // Backward map: every coin used names the set it was made from. With at most K coins no coin can be
    // used twice (its digits would be 2), so each coin used gives one set of the cover.
    public (string Answer, ChosenSets Partial) MapBack(string problemToSolution, StepRecorder<ChosenSets> rec) {
        List<(int coin, int count)> pairs;
        try {
            StringParser parser = new("{P | P subset int cross int}");
            parser.parse(problemToSolution);
            pairs = parser["P"].ToList()
                .Select(pair => (int.Parse(pair[0].ToString()), int.Parse(pair[1].ToString()))).ToList();
        } catch (Exception ex) {
            throw new CertificateParseException(_reductionTo, problemToSolution, ex.Message);
        }

        List<string> chosen = new List<string>();
        if (_reductionFrom.X.Count == 0)
            return ("{}", new ChosenSets(chosen));

        foreach ((int coin, int count) in pairs) {
            int j = _coins.IndexOf(coin);
            if (j < 0 || coin == 0)
                throw new CertificateParseException(_reductionTo, problemToSolution, $"{coin} is not a coin of this instance");
            string set = "{" + string.Join(",", _reductionFrom.S[j]) + "}";
            if (count <= 0) {
                rec.Reject(() => new ChosenSets(chosen), () => $"Coin {coin} is used {count} times, so {set} is not chosen.", set);
                continue;
            }
            chosen.Add(set);
            rec.Accept(() => new ChosenSets(chosen), () => $"Coin {coin} was made from {set}, so {set} is in the cover.", set);
        }
        return ("{" + string.Join(",", chosen) + "}", new ChosenSets(chosen));
    }

    // Index of the first set in S with the same elements as set, ignoring order; -1 if there is none.
    private int IndexOfSet(List<string> set) {
        HashSet<string> wanted = new HashSet<string>(set);
        for (int j = 0; j < _reductionFrom.S.Count; j++) {
            if (wanted.SetEquals(_reductionFrom.S[j]))
                return j;
        }
        return -1;
    }
}
