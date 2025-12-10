using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SIMON.Verifiers;

class SimonVerifier : IVerifier<SIMON>
{

    // --- Fields ---
    public string verifierName {get;} = "ProblemVerifier";
    public string verifierDefinition { get; } = "Classifcal Verifier";
    public string source {get;} = " ";
    public string[] contributors { get; } = { "Jason L. Wright", "Eric Hill", "Paul Gilbreath", "Max Gruenwoldt", "Alex Svancara" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public SimonVerifier()
    {

    }

    public bool verify(SIMON problem, string certificate)
    {
        if (problem.funcValues is null)
            throw new NullReferenceException();
        int nbits = SIMON.PowerOfTwo(problem.funcValues.Length);
        if (certificate.Length > nbits)
            return false;

        var vals = new HashSet<int>();

        int s = Convert.ToInt32(certificate, 2);
        for (int i = 0; i < problem.funcValues.Length; i++)
        {
            if (problem.Func(i) != problem.Func(i ^ s))
                return false;
            vals.Add(problem.Func(i));
        }

        if (vals.Count == problem.funcValues.Length && s == 0)
            return true;
        if (vals.Count == problem.funcValues.Length / 2 && s != 0)
            return true;
        return false;
    }
}
