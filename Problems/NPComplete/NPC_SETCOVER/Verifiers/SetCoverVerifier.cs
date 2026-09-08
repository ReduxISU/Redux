using API.Interfaces;
using API.Interfaces.Graphs.GraphParser;
using SPADE;

namespace API.Problems.NPComplete.NPC_SETCOVER.Verifiers;

class SetCoverVerifier : IVerifier<SETCOVER> {

    // --- Fields ---
    public string verifierName { get; } = "Set Cover Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Set Cover";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };


    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public SetCoverVerifier() {

    }
    private List<List<string>> parseCertificate(string certificate) {

        UtilCollection parsed = new UtilCollection(certificate);
        List<List<string>> chosenSubsets = new List<List<string>>();

        foreach (UtilCollection subset in parsed) {
            chosenSubsets.Add(subset.ToList().Select(item => item.ToString()!.Trim()).ToList());
        }

        return chosenSubsets;

    }


    public bool verify(SETCOVER problem, string certificate) {

        List<List<string>> chosenSubsets;
        try {
            chosenSubsets = parseCertificate(certificate);
        } catch {
            return false;
        }

        if (chosenSubsets.Count > problem.K) {
            return false;
        }

        List<HashSet<string>> validSubsets = problem.subsets.Select(s => new HashSet<string>(s)).ToList();
        HashSet<string> coveredElements = new HashSet<string>();

        foreach (List<string> chosen in chosenSubsets) {
            HashSet<string> chosenSet = new HashSet<string>(chosen);
            if (!validSubsets.Any(s => s.SetEquals(chosenSet))) {
                return false;
            }
            coveredElements.UnionWith(chosenSet);
        }

        return coveredElements.IsSupersetOf(problem.universal);
    }
}