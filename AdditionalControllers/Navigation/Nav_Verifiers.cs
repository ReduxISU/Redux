using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

internal static class VerifierNavigationData {
    // Build once from reflected verifier types so navigation doesn't depend on
    // on-disk source layout.
    internal static readonly List<NavigationEntry> Entries =
        InterfaceNavigationData.Build(ProblemProvider.Verifiers, typeof(IVerifier<>));

    internal static List<string> FindWithoutExtension(string? problemName, string? problemTypePrefix) =>
        InterfaceNavigationData.FindWithoutExtension(Entries, problemName, problemTypePrefix);

    internal static bool TryParseProblemKey(string chosenProblem, out string? problemTypePrefix, out string? problemName) {
        problemTypePrefix = null;
        problemName = null;
        if (string.IsNullOrWhiteSpace(chosenProblem)) return false;

        string trimmed = chosenProblem.Trim();
        int idx = trimmed.IndexOf('_');
        if (idx <= 0 || idx >= trimmed.Length - 1) {
            problemName = trimmed;
            return true;
        }

        problemTypePrefix = trimmed[..idx];
        problemName = trimmed[(idx + 1)..];
        return true;
    }
}

// Get all Verifiers for a specific problem\
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Verifiers)")]
#pragma warning disable CS1591

public class Problem_VerifiersRefactorController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns all verifiers available for a given problem </summary>
    ///<param name="chosenProblem" example="SAT3">Problem name</param>
    ///<param name="problemType" example="NPC">Problem type (optional; ignored — problem names are unique across complexity classes)</param>
    ///<response code="200">Returns string array of verifiers</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public String getDefault([FromQuery] string chosenProblem, [FromQuery] string? problemType = null) {
        string NOT_FOUND_ERR_VERIFIER = "entered a verifier that does not exist";
        string jsonString = "";
        var options = new JsonSerializerOptions { WriteIndented = true };

        List<string> subFilesList = VerifierNavigationData.FindWithoutExtension(chosenProblem, problemType);
        jsonString = subFilesList.Count > 0
            ? JsonSerializer.Serialize(subFilesList, options)
            : JsonSerializer.Serialize(NOT_FOUND_ERR_VERIFIER, options);

        return jsonString;
    }
}