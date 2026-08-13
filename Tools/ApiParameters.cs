
namespace API.Tools.ApiParameters;

/// <summary>API parameters for the verify routes.</summary>
public class Verify {
    /// <summary>The certificate solution to the problem.</summary>
    /// <example>{1,2,3}</example>
    public string Certificate { get; set; } = "";
    /// <summary>The problem instance.</summary>
    /// <example>(({1,2,3},{{1,2},{2,3},{1,3}}),3)</example>
    public string ProblemInstance { get; set; } = "";
}

/// <summary>API parameters for the map solution routes.</summary>
public class MapSolution {
    /// <summary>The problem to reduce from.</summary>
    /// <example>INDEPENDENTSET</example>
    public string ProblemFrom { get; set; } = "";
    /// <summary>The problem to reduce to.</summary>
    /// <example>CLIQUE</example>
    public string ProblemTo { get; set; } = "";
    /// <summary>The solution to the problem.</summary>
    /// <example>{1,2,3}</example>
    public string ProblemFromSolution { get; set; } = "";
}
