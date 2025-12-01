using API.Interfaces;

namespace API.Problems.NPComplete.NPC_BERNSTEINVAZIRANI.Verifiers;

class BernsteinVaziraniClassicalVerifier : IVerifier<BERNSTEINVAZIRANI> {

    // --- Fields ---
    public string verifierName {get;} = "Bernstein Vazirani Classical Verifier";
    public string verifierDefinition {get;} = "Verify that a proposed solution bit string fulfills the promise of the Bernstein-Vazirani problem: f(x) = s · x for all x.";
    public string source {get;} = " ";
    public string[] contributors {get;} = { "Jason L. Wright" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public BernsteinVaziraniClassicalVerifier() {
        
    }

    /// <summary>
    /// Count the number of bits set to 1 in an integer
    /// </summary>
    /// <param name="value"></param>
    /// <returns>The numver of bits that are 1 in value</returns>
    private static int CountBits(int value) {
        int count = 0;
        while (value != 0) {
            count++;
            value &= value - 1;
        }
        return count;
    }
    public bool verify(BERNSTEINVAZIRANI problem, string certificate){
        int certInt = Convert.ToInt32(certificate, 2);

        for (int x = 0; x < (1 << problem.NBits); x++) {

            // Compute s · x
            int dotProduct = CountBits(certInt & x) % 2;
            bool actual = (dotProduct == 1);

            bool expected = problem.Func(x);

            if (expected != actual)
                return false;
        }
        return true;
    }
}
