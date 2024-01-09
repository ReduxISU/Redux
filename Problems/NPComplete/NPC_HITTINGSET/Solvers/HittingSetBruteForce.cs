using API.Interfaces;
using API.Problems.NPComplete.NPC_HITTINGSET;
using API.Tools.UtilCollection;

namespace API.Problems.NPComplete.NPC_HITTINGSET.Solvers;

class HittingSetBruteForce : ISolver {

    // --- Fields ---
    private string _solverName = "Hitting Set Brute Force";
    private string _solverDefinition = "This is a brute force solver for Hitting Set";
    private string _source = "This person is Russell Phillips";
    private string[] _contributers = {"Russell Phillips"};


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
    public HittingSetBruteForce() {
        
    }

    public IEnumerable<List<int>> possibleSolutions(int len)
    {
        for (int i = 0; i < Math.Pow(2, len); i++) 
        {
            List<int> solution = new List<int>(); 
            for (int solBin = i + (int)Math.Pow(2, len); solBin != 1; solBin >>= 1)
            {
                if ((solBin & 1) == 0)
                    solution.Add(0);
                else
                    solution.Add(1);
            }
            yield return solution;
        }
    }

    public string solve(HITTINGSET hittingSet)
    {
        List<UtilCollection> items = hittingSet.universalSet.ToList();
        foreach (List<int> possibleSolution in possibleSolutions(items.Count()))
        {
            UtilCollection certificate = new UtilCollection("{}");
            for (int i = 0; i < hittingSet.subSets.Count() + 1; i++)
            {
                if (possibleSolution[i] == 1) certificate.Add(items[i]);
            }
            string strCertificate = certificate.ToString();
            if (hittingSet.defaultVerifier.verify(hittingSet, strCertificate)) return strCertificate;
        }

        return "";

    }

    /// <summary>
    /// Given Independent Set instance in string format and solution string, outputs a solution dictionary with 
    /// true values mapped to nodes that are in the solution set else false. 
    /// </summary>
    /// <param name="problemInstance"></param>
    /// <param name="solutionString"></param>
    /// <returns></returns>
    public Dictionary<string,bool> getSolutionDict(string problemInstance, string solutionString)
    {
        throw new NotImplementedException();
    }
}
