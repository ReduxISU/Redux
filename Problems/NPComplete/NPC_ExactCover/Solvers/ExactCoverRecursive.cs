using API.Interfaces;

namespace API.Problems.NPComplete.NPC_ExactCover.Solvers;
class ExactCoverRecursive : ISolver {

    // --- Fields ---
    private string _solverName = "Exact Cover Recursive solver";
    private string _solverDefinition = "This is a optimized recursive solver for Exact Cover";
    private string _source = "This person Russell Phillips";
    private string[] _contributers = { "Russell Phillips"};


    // --- Properties ---
    public string solverName {
        get {
            return _solverName;
        }
    }
    public string solverDefinition {
        get {
            return _solverDefinition;
        }
    }
    public string source {
        get {
            return _source;
        }
    }
    public string[] contributers{
        get{
            return _contributers;
        }
    }
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

    public string solve(ExactCover exactCover)
    {
        List<string> uSet = new List<string>(exactCover.X);
        List<List<string>> subsets = new List<List<string>>(exactCover.S);
        List<List<string>> choosenSubsets = new List<List<string>>();
        return subsetsToCertificate(solve_r(exactCover, uSet, subsets, choosenSubsets, 0));
    }

    public void printStuff(List<List<string>> subsetList, List<List<string>> choosenSubsets, List<List<string>> possibleSubsets, int depth){
        Console.WriteLine(depth);
        Console.WriteLine("subsetList");
        Console.WriteLine(subsetsToCertificate(subsetList));
        Console.WriteLine("choosenSubets");
        Console.WriteLine(subsetsToCertificate(choosenSubsets));
        Console.WriteLine("possibleSubsets");
        Console.WriteLine(subsetsToCertificate(possibleSubsets));


    }

    public List<List<string>> solve_r(ExactCover exactCover, List<string> uSet, List<List<string>> subsetList, List<List<string>> choosenSubsets, int depth) 
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

        //printStuff(subsetList, choosenSubsets, possibleSubsets, depth);

        //foreach remaining good set in subsets, add it to choosen subsets and recurse.
        foreach (List<string> set in new List<List<string>>(possibleSubsets))
        {
            //if one returns a solution return it, otherwise return empty
            possibleSubsets.Remove(set);
            choosenSubsets.Add(set);
            List<List<string>> result = solve_r(exactCover, uSet, possibleSubsets, choosenSubsets, depth+1);
            if (result.Count > 0)
                return result;
            choosenSubsets.Remove(set);
            possibleSubsets.Add(set);
        }
        return new List<List<string>>();
        
    } //doesnt return null for some reason

}