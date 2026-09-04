using API.Interfaces;
using SPADE;

namespace API.Problems.NPComplete.NPC_PARTITION.Verifiers;

class PartitionVerifier : IVerifier<PARTITION> {
    public const string CertificateGrammar = "(S1),(S2) | S1,S2 partition S exactly (each element used once), sum(S1) = sum(S2)";
    public const string CertificateExample = "(33,21,15),(1,7,12,11,5,6,9,18)";

    // --- Fields ---
    public string verifierName { get; } = "Partition Verifier";
    public string verifierDefinition { get; } = "This is a verifier for the Partition problem";
    public string source { get; } = "";
    public string[] contributors { get; } = { "Andrija Sevaljevic" };

    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }


    // --- Methods Including Constructors ---
    public PartitionVerifier() {

    }

    // Certificates come in two shapes: the documented bare pair
    // ("(33,21,15),(1,7,12,11,5,6,9,18)", no enclosing collection) and the form
    // PartitionBruteForce actually emits, which wraps that pair in an outer
    // "{...}". Try parsing as-is first (covers the solver's output); if that
    // isn't a single valid top-level collection, wrap it in parens ourselves.
    private List<List<string>> ParseCertificate(string certificate) {
        UtilCollection pair;
        try {
            pair = new UtilCollection(certificate);
            pair.assertCount(2);
        } catch {
            pair = new UtilCollection("(" + certificate + ")");
            pair.assertCount(2);
        }

        return pair.ToList().Select(group => group.ToList().Select(x => x.ToString()).ToList()).ToList();
    }

    public bool verify(PARTITION problem, string certificate) {
        List<List<string>> groups;
        try {
            groups = ParseCertificate(certificate);
        } catch {
            return false;
        }
        List<string> c = groups[0];
        List<string> c2 = groups[1];

        foreach (var a in problem.S) {
            if (problem.S.Count(n => n == a) != (c.Count(n => n == a) + c2.Count(n => n == a))) {
                return false;
            }
        }

        int sum = 0;
        int sum2 = 0;

        foreach (string a in c) {
            if (problem.S.Contains(a)) {
                sum += int.Parse(a);
            } else {
                return false;
            }
        }
        foreach (string a in c2) {
            if (problem.S.Contains(a)) {
                sum2 += int.Parse(a);
            } else {
                return false;
            }
        }

        if (sum == sum2 && (c.Count() + c2.Count()) == problem.S.Count()) {
            return true;
        }

        return false;
    }


}