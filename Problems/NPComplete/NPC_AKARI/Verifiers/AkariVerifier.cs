using System.Text.Json.Serialization;
using API.Interfaces;

namespace API.Problems.NPComplete.NPC_AKARI.Verifiers;

class AkariVerifier : IVerifier<AKARI> {

    // --- Fields ---
    public string verifierName { get; } = "Akari (Light Up Puzzle) Solution Verifier";
    public string verifierDefinition { get; } = "Reads the certificate as the instance grid with light bulbs marked '*', checks that it copies every wall and clue of the instance, and accepts it when every white cell is lit, no bulb lights another bulb, and every clue has exactly its number of orthogonally adjacent bulbs.";
    public string source { get; } = "The rules of Akari as published by Nikoli: https://www.nikoli.co.jp/en/puzzles/akari/";
    public string sourceLink { get; } = "https://www.nikoli.co.jp/en/puzzles/akari/";
    public string sourceFile { get; } = SourceFile.Path();
    public string[] contributors { get; } = { "Carter Luker" };
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? complexity { get; set; } = "O(n * m), n = rows, m = columns";
    private string _certificate = "";

    public string certificate {
        get {
            return _certificate;
        }
    }

    /// <summary>The solvers' answer when the instance has no solution. Well-formed, but never a solution.</summary>
    public const string NoSolution = "{}";

    /// <summary>A solution of the default instance, quoted in <see cref="AKARI.certificateFormat"/>.</summary>
    public const string CertificateExample = "((_,*,#,_,_,*,#),(*,4,*,_,1,_,#),(_,*,_,2,*,_,_),(_,#,_,*,_,#,_),(_,_,_,#,_,_,*),(#,_,#,*,_,1,_),(1,*,_,_,1,*,_))";

    // --- Methods Including Constructors ---
    public AkariVerifier() {

    }

    /// <summary>
    /// True when <paramref name="certificate"/> solves <paramref name="problem"/>. Returns false for a readable
    /// certificate that does not: one that breaks a rule, the "{}" no-solution answer, or a grid that does not match
    /// the instance (another size, a changed wall or clue, a bulb on a wall). Throws
    /// <see cref="CertificateParseException"/> for text that is not a grid at all.
    /// </summary>
    public bool verify(AKARI problem, string certificate) {
        if (certificate?.Trim() == NoSolution) {
            return false;
        }

        int[][] certificateGrid = AKARI.ParseGrid(certificate!, allowBulbs: true,
            detail => new CertificateParseException(problem, certificate!, detail));

        bool[]? bulbs = problem.BulbsFrom(certificateGrid);
        return bulbs != null && problem.IsSolution(bulbs);
    }
}
