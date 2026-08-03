using API.Interfaces;

namespace API.Problems.NPComplete.NPC_EXACTCOVER.Solvers;
class ExactCoverRecursive : ISolver<EXACTCOVER> {

    // --- Fields ---
    public string solverName {get;} = "Exact Cover Recursive Solver";
    public string solverDefinition {get;} = "This is a optimized recursive solver for Exact Cover";
    public string source {get;} = "";
    public string[] contributors {get;} = { "Russell Phillips"};
    public bool timerHasExpired { get; set; }
    // Declared, not derived. Exact recursive search WITH pruning -- distinct from ExactCoverBruteForce,
    // which does not prune.
    public SolverType solverType { get; } = SolverType.Backtracking;
    // Corrected from Exponential: solve_r has no index cursor to fix a selection order -- at every
    // level it re-scans the full remaining `possibleSubsets` list and recurses once per candidate, so
    // the exact same final combination of subsets can be assembled via any of its orderings, all of
    // which get explored separately (classic combination-search-without-a-cursor blowup). When no two
    // subsets in S share elements (so shareElememnts never prunes a branch), this degenerates into a
    // full permutation tree of the s = |S| subsets: O(s!) recursive calls in the worst case, matching
    // the Backtracking + Factorial pairing already used by NQueensBacktracking.
    public SolverComplexityBucket complexityBucket { get; } = SolverComplexityBucket.Factorial;
    // Per node: building possibleSubsets scans up to s remaining subsets, each checked via
    // shareElememnts against up to s chosen subsets, O(n^2) per pairwise check (n = |X|, bounding
    // subset size) -- O(s^2 * n^2) per node. Combined with the O(s!) node count above.
    public string complexity { get; } = "O(s! * s^2 * n^2), s = |S| (candidate subsets), n = |X| (bounds subset-comparison cost)";

    // --- Methods Including Constructors ---
    public ExactCoverRecursive() {
        
    }
    private string BinaryToCertificate(List<int> binary,  List<List<string>> S ){
        string certificate = "";
        for(int i = 0; i< binary.Count; i++){
            if(binary[i] == 1){
                certificate += "{";
                foreach(var element in S[i]){
                    certificate += element+",";
                }
                certificate = certificate.TrimEnd(',') + "},";
            }
        }
        return "{" + certificate.TrimEnd(',') + "}";

    }

    private string subsetsToCertificate(List<List<string>> solution)
    {
        if (solution.Count == 0)
        {
            return "";
        }

        string certificate = "{";

        foreach (List<string> set in solution)
        {
            certificate += "{";
            foreach (string item in set)
            {
                certificate += item.ToString() + ',';
            }
            certificate = certificate.TrimEnd(',') + "},";
        }
        certificate = certificate.TrimEnd(',') + "}";
        return certificate;

    }

    private bool shareElememnts(List<string> a, List<string> b)
    {
        foreach (string item in a)
        {
            if (b.Contains(item))
                return true;
        }
        return false;
    }

    private bool shareElememnts(List<string> set, List<List<string>> sets)
    {
        foreach (List<string> set2 in sets)
        {
            if (shareElememnts(set, set2))
            {
                return true;
            }
        }
        return false;
    }

    public string solve(EXACTCOVER exactCover)
    {
        List<string> uSet = new List<string>(exactCover.X);
        List<List<string>> subsets = new List<List<string>>(exactCover.S);
        List<List<string>> choosenSubsets = new List<List<string>>();
        return subsetsToCertificate(solve_r(exactCover, uSet, subsets, choosenSubsets));
    }

    public List<List<string>> solve_r(EXACTCOVER exactCover, List<string> uSet, List<List<string>> subsetList, List<List<string>> choosenSubsets) 
    {
        //check if choosen subsets is a solution, if it is, return it
        if (exactCover.defaultVerifier.verify(exactCover, subsetsToCertificate(choosenSubsets)))
            return choosenSubsets;

        //only look into subsets that don't already share an element with a choosen subset
        List<List<string>> possibleSubsets = new List<List<string>>();
        foreach (List<string> possibleset in subsetList)
        {
            if (!shareElememnts(possibleset, choosenSubsets))
            {
                possibleSubsets.Add(possibleset);
            }
        }

        //foreach remaining good set in subsets, add it to choosen subsets and recurse.
        foreach (List<string> set in new List<List<string>>(possibleSubsets))
        {
            //if one returns a solution return it, otherwise return empty
            possibleSubsets.Remove(set);
            choosenSubsets.Add(set);
            List<List<string>> result = solve_r(exactCover, uSet, possibleSubsets, choosenSubsets);
            if (result.Count > 0)
                return result;
            choosenSubsets.Remove(set);
            possibleSubsets.Add(set);
        }
        return new List<List<string>>();
        
    } //doesnt return null for some reason

}
