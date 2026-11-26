using API.Interfaces;
using System;
using SPADE;


namespace API.Problems.NPComplete.NPC_KNAPSACK.Verifiers;

class KnapsackVerifier : IVerifier<KNAPSACK> {
    public const string CertificateGrammar = "{(weight,value),...} subset i | total weight <= W, total value >= V";
    public const string CertificateExample = "{(20,100),(30,120)}";

    // --- Fields ---
    public string verifierName { get; } = "Knapsack (Binary) Solution Verifier";
    public string verifierDefinition { get; } = "This is a verifier for Knapsack. It checks that that the weight of the chosen items do not exceed the allowed weight and that the value of the items exceed the required value";
    public string source { get; } = "";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Garret Stouffer", "Daniel Igbokwe", "Russell Phillips" };

    private string _complexity = "O(n^2)";

    private string _certificate = "{(30:120,20:100):220}";

    public string complexity {
        get {
            return _complexity;
        }
    }

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public KnapsackVerifier() {

    }

    public bool verify(KNAPSACK problem, string certificate) {
        List<UtilCollection> solution;
        try {
            solution = parseCertificate(problem, certificate);
        } catch {
            return false;
        }
        int totalW = 0;
        int totalV = 0;

        foreach (UtilCollection solItem in solution) {
            totalW += solItem[0].parseInt();
            totalV += solItem[1].parseInt();
        }

        if (totalW <= problem.W && totalV >= problem.V) return true;
        else return false;

    }

    // The chosen items, with repeats kept (#628): two identical items in the certificate are two items, and each must be a
    // separate item of the instance, so (1,2) can be chosen twice only when the instance has it twice.
    // TODO(#628): once SPADE releases "is multiset", the certificate can be read as a SPADE multiset.
    private List<UtilCollection> parseCertificate(KNAPSACK problem, string certificate) {
        List<UtilCollection> solution = RepeatedElements.KeepRepeats(certificate, new UtilCollection(certificate));
        // A copy: UtilCollection.ToList() hands back the collection's own list, which must not lose items here.
        List<UtilCollection> unchosen = new List<UtilCollection>(problem.items.ToList());
        foreach (UtilCollection solItem in solution) {
            solItem.assertPair();
            int at = unchosen.FindIndex(item => item.Equals(solItem));
            if (at < 0) throw new Exception();
            unchosen.RemoveAt(at);
        }
        return solution;
    }


}
