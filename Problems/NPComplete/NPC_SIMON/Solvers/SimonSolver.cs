using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SIMON.Solvers;

class SimonSolver : ISolver<SIMON>
{

    // --- Fields ---
    public string solverName { get; } = "Simon's Problem - Classical Solver";
    public string solverDefinition { get; } = "This solver classically solves Simon's Problem by analyzing the provided function values to determine the secret string.";
    public string source { get; } = "Simon, Daniel R. (1997-10-01). \"On the Power of Quantum Computation\". SIAM Journal on Computing. 26 (5): 1474–1483. doi:10.1137/S0097539796298637. ISSN 0097-5397";
    public string[] contributors { get; } = { "Jason L. Wright", "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };
    public bool timerHasExpired { get; set; }

    // --- Methods Including Constructors ---
    public SimonSolver() { }

    public string solve(SIMON problem)
    {
        int nbits = SIMON.PowerOfTwo(problem.funcValues.Length);

        var fmap = new Dictionary<int, HashSet<int>>();
        for (int s = 0; s < problem.funcValues.Length; s++)
        {
            int y = problem.Func(s);
            if (!fmap.ContainsKey(y))
                fmap[y] = new HashSet<int>();
            fmap[y].Add(s);
        }

        if (fmap.Count == problem.funcValues.Length)
        {
            // one to one mapping, secret is all zeros
            return string.Concat(Enumerable.Repeat("0", nbits));
        }

        var candidates = new HashSet<int>();
        foreach (var ys in fmap.Values)
        {
            if (ys.Count != 2)
                return "invalid func: not 2-to-1 mapping";
            int candidate = 0;
            foreach (var c in ys)
                candidate ^= c;
            candidates.Add(candidate);
        }

        if (candidates.Count > 1)
            return "invalid func: no secret string";

        return Convert.ToString(candidates.First(), 2).PadLeft(nbits, '0');
    }
}
