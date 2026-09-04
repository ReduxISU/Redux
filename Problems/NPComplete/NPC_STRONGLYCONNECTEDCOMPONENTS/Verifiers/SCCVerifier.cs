using API.Interfaces;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS;
using API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Solvers;
using SPADE;

namespace API.Problems.NPComplete.NPC_STRONGLYCONNECTEDCOMPONENTS.Verifiers;

class SCCVerifier : IVerifier<STRONGLYCONNECTEDCOMPONENTS> {
    public const string CertificateGrammar = "{c1,...,cM} | one set per strongly connected component, order does not matter";
    public const string CertificateExample = "{{1,2,3},{4,5}}";

    public string verifierName { get; } = "Strongly Connected Components Verifier";

    public string verifierDefinition { get; } =
        "Verifies an SCC certificate by comparing components as unordered sets, since SCC ordering is not unique.";

    public string source { get; } =
        "Swati Dhingra, Poorvi S. Dodwad, and Meghna Madan, \"Finding Strongly Connected Components in a Social Network Graph,\" International Journal of Computer Applications, Volume 136, No. 7, February 2016.";

    public string sourceLink { get; } =
        "https://ijcaonline.org/research/volume136/number7/dhingra-2016-ijca-908481.pdf";

    public string[] contributors { get; } = { "Surendra Thapa", "Rohan Shrestha" };

    public string certificate { get; set; } = string.Empty;

    public bool verify(STRONGLYCONNECTEDCOMPONENTS problem, string certificate) {
        if (string.IsNullOrWhiteSpace(certificate))
            return false;

        var solver = new KosarajuSolver();
        string expected = solver.solve(problem);

        HashSet<string> given;
        try {
            given = NormalizeSccs(certificate);
        } catch {
            return false;
        }

        return NormalizeSccs(expected).SetEquals(given);
    }

    private static HashSet<string> NormalizeSccs(string value) {
        UtilCollection components = new UtilCollection(value);
        components.assertUnordered();

        var result = new HashSet<string>();
        foreach (UtilCollection component in components) {
            component.assertUnordered();
            var nodes = component.ToList().Select(n => n.ToString()).OrderBy(x => x).ToList();
            result.Add(string.Join(",", nodes));
        }

        return result;
    }
}