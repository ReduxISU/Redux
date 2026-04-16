using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
[ApiController]
[Route("Navigation/[controller]")]
[Tags("Navigation - Batch")]
#pragma warning disable CS1591
public class BatchController : ControllerBase
{
#pragma warning restore CS1591
    private readonly IMemoryCache _cache;
    public BatchController(IMemoryCache cache)
    {
        _cache = cache;
    }

    
    /// <summary>
    /// Returns ALL problems for a given complexity class in a single call.
    /// No request body is required. Defaults to NPC.
    /// </summary>
    [ProducesResponseType(typeof(List<string>), 200)]
    [HttpPost("allProblems")]
    public IActionResult GetAllProblems([FromQuery] string problemType = "NPC")
    {
        string cacheKey = $"batch_problems_{problemType}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            Response.Headers["X-Cache"] = "HIT";
            return Content(cached!, "application/json");
        }
        string problemTypeDirectory = GetDirectory(problemType);
        string projectSourcePath = ProjectSourcePath.Value;
        string problemsPath = Path.Combine(projectSourcePath, "Problems", problemTypeDirectory);
        if (!Directory.Exists(problemsPath))
            return NotFound($"Directory not found: {problemsPath}");
        var problems = Directory.GetDirectories(problemsPath)
            .Select(dir =>
            {
                string name = Path.GetFileName(dir);
                string[] parts = name.Split('_');
                return parts.Length >= 2 ? string.Join("_", parts.Skip(1)) : null;
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(problems, Newtonsoft.Json.Formatting.Indented);
        _cache.Set(cacheKey, json, TimeSpan.FromHours(1));
        Response.Headers["X-Cache"] = "MISS";
        return Content(json, "application/json");
    }


    /// <summary>
    /// Returns ALL solvers for ALL problems in one call.
    /// No request body is required. Defaults to NPC.
    /// </summary>
    [ProducesResponseType(typeof(Dictionary<string, List<string>>), 200)]
    [HttpPost("allSolvers")]
    public IActionResult GetAllSolvers([FromQuery] string problemType = "NPC")
    {
        string cacheKey = $"batch_solvers_{problemType}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            Response.Headers["X-Cache"] = "HIT";
            return Content(cached!, "application/json");
        }
        var result = GetAllSubfolderFiles(problemType, "Solvers");
        if (result == null)
            return NotFound($"No problem directory found for type {problemType}");
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
        _cache.Set(cacheKey, json, TimeSpan.FromHours(1));
        Response.Headers["X-Cache"] = "MISS";
        return Content(json, "application/json");
    }


    /// <summary>
    /// Returns ALL verifiers for ALL problems in one call.
    /// No request body is required. Defaults to NPC.
    /// </summary>
    [ProducesResponseType(typeof(Dictionary<string, List<string>>), 200)]
    [HttpPost("allVerifiers")]
    public IActionResult GetAllVerifiers([FromQuery] string problemType = "NPC")
    {
        string cacheKey = $"batch_verifiers_{problemType}";

        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            Response.Headers["X-Cache"] = "HIT";
            return Content(cached!, "application/json");
        }
        var result = GetAllSubfolderFiles(problemType, "Verifiers");
        if (result == null)
            return NotFound($"No problem directory found for type {problemType}");
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
        _cache.Set(cacheKey, json, TimeSpan.FromHours(1));
        Response.Headers["X-Cache"] = "MISS";
        return Content(json, "application/json");
    }


    /// <summary>
    /// Returns ALL visualizations for ALL problems in one call.
    /// No request body is required. Defaults to NPC.
    /// </summary>
    [ProducesResponseType(typeof(Dictionary<string, List<string>>), 200)]
    [HttpPost("allVisualizations")]
    public IActionResult GetAllVisualizations([FromQuery] string problemType = "NPC")
    {
        string cacheKey = $"batch_visualizations_{problemType}";

        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            Response.Headers["X-Cache"] = "HIT";
            return Content(cached!, "application/json");
        }

        var result = GetAllSubfolderFiles(problemType, "Visualizations");
        if (result == null)
            return NotFound($"No problem directory found for type {problemType}");
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
        _cache.Set(cacheKey, json, TimeSpan.FromHours(1));
        Response.Headers["X-Cache"] = "MISS";
        return Content(json, "application/json");
    }


/// <summary>
/// Returns ProblemProvider/info data for ALL interfaces in one call.
/// No request body is required. Defaults to NPC.
/// </summary>
[ProducesResponseType(typeof(Dictionary<string, object>), 200)]
[HttpPost("allInfo")]
public IActionResult GetAllInfo([FromQuery] string problemType = "NPC")
{
    string cacheKey = $"batch_info_{problemType}";
    if (_cache.TryGetValue(cacheKey, out string? cached))
    {
        Response.Headers["X-Cache"] = "HIT";
        return Content(cached!, "application/json");
    }
    string problemTypeDirectory = GetDirectory(problemType);
    string projectSourcePath = ProjectSourcePath.Value;
    string problemsPath = Path.Combine(projectSourcePath, "Problems", problemTypeDirectory);
    if (!Directory.Exists(problemsPath))
        return NotFound($"Directory not found: {problemsPath}");
    var interfaces = ProblemProvider.Interfaces;
    var interfaceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var problemDir in Directory.GetDirectories(problemsPath))
    {
        string fullDirName = Path.GetFileName(problemDir);
        string[] parts = fullDirName.Split('_');
        if (parts.Length < 2) continue;
        string problemName = string.Join("_", parts.Skip(1));
        interfaceNames.Add(problemName);
        
        foreach (var solver in GetFilesNoExt(Path.Combine(problemDir, "Solvers")))
            interfaceNames.Add(solver);
        foreach (var verifier in GetFilesNoExt(Path.Combine(problemDir, "Verifiers")))
            interfaceNames.Add(verifier);
        foreach (var visualization in GetFilesNoExt(Path.Combine(problemDir, "Visualizations")))
            interfaceNames.Add(visualization);
    }
    var result = new Dictionary<string, object>();

    foreach (var name in interfaceNames.OrderBy(x => x))
    {
        try
        {
            if (interfaces.TryGetValue(name.ToLower(), out var type))
            {
                var instance = Activator.CreateInstance(type);
                if (instance != null)
                {
                    result[name] = instance;
                }
            }
        }
        catch
        {
            // Skip bad interface instead of failing the whole response here
        }
    }
    string finalJson = Newtonsoft.Json.JsonConvert.SerializeObject(
        result,
        Newtonsoft.Json.Formatting.Indented,
        new Newtonsoft.Json.JsonSerializerSettings
        {
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
        });

    _cache.Set(cacheKey, finalJson, TimeSpan.FromHours(1));
    Response.Headers["X-Cache"] = "MISS";
    return Content(finalJson, "application/json");
}


    /// <summary>
    /// Clears the batch cache.
    /// </summary>
    [HttpDelete("clearCache")]
    public IActionResult ClearCache()
    {
        foreach (var pt in new[] { "NPC", "P", "NPHard" })
        {
            _cache.Remove($"batch_problems_{pt}");
            _cache.Remove($"batch_solvers_{pt}");
            _cache.Remove($"batch_verifiers_{pt}");
            _cache.Remove($"batch_visualizations_{pt}");
            _cache.Remove($"batch_info_{pt}");
        }

        return Ok("Cache cleared.");
    }
    private Dictionary<string, List<string>>? GetAllSubfolderFiles(string problemType, string subfolder)
    {
        string problemTypeDirectory = GetDirectory(problemType);
        string projectSourcePath = ProjectSourcePath.Value;
        string problemsPath = Path.Combine(projectSourcePath, "Problems", problemTypeDirectory);

        if (!Directory.Exists(problemsPath))
            return null;

        var result = new Dictionary<string, List<string>>();

        foreach (var problemDir in Directory.GetDirectories(problemsPath))
        {
            string fullDirName = Path.GetFileName(problemDir);
            string[] parts = fullDirName.Split('_');
            if (parts.Length < 2) continue;

            string problemName = string.Join("_", parts.Skip(1));
            var files = GetFilesNoExt(Path.Combine(problemDir, subfolder));

            result[problemName] = files.OrderBy(x => x).ToList();
        }

        return result;
    }
    private static string GetDirectory(string problemType) => problemType switch
    {
        "NPC" => "NPComplete",
        "P" => "P",
        "NPHard" => "NPHard",
        _ => "NPComplete"
    };
    private static List<string> GetFilesNoExt(string dirPath)
    {
        if (!Directory.Exists(dirPath))
            return new List<string>();

        return Directory.GetFiles(dirPath)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();
    }
}
