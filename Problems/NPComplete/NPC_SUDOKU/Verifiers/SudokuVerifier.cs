using API.Interfaces;

namespace API.Problems.NPComplete.NPC_SUDOKU.Verifiers;

class SudokuVerifier : IVerifier<SUDOKU> {

    // --- Fields ---
    public string verifierName { get; } = "TODO";
    public string verifierDefinition {get;} = "TODO";
    public string source {get;} = " ";
    public string[] contributors { get; } = { "Eric Hill" };
    private string _certificate =  "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    // --- Methods Including Constructors ---
    public SudokuVerifier()
    {
    }

    public bool verify(SUDOKU problem, string certificate){
        // int i = Convert.ToInt32(certificate);
        // // All we need to do is see if funcValues[certificate] is non-zero
        // return problem.funcValues[i] != 0;

        return true;
    }
}
