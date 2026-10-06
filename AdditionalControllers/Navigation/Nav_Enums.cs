using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;

// Closed-vocabulary catalog endpoints for the taxonomy enums the GUI's facet filters are
// built from: ComplexityClass, ProblemType, ReductionType, ReductionCost, SolverType,
// SolverComplexityBucket. Same shape as VisualizationTypeCatalog/VisualizationTypesController
// (Nav_Visualizations.cs) -- Enum.GetNames, sorted, returned as a bare string array -- so the
// frontend's facet option lists can be built from the real enum instead of a hand-typed copy
// of it that can silently drift out of sync when a member is added, renamed, or removed.
//
// Each of these enums already has an existing per-class-name lookup catalog elsewhere
// (ComplexityClassCatalog in Nav_Problems.cs, ReductionCostCatalog/ReductionTypeCatalog in
// Nav_Reductions.cs, SolverTypeCatalog in Nav_Solvers.cs) that answers "what value did this
// specific class declare?" -- a different question from "what are all the possible values?",
// which is what these endpoints answer. Deliberately not folded into those existing internal
// static classes (name collisions aside), and deliberately not given their own internal
// static class here either: unlike VisualizationTypeCatalog, there is no per-class-name
// lookup to share the class with, so a bare `Enum.GetNames` call inline in each controller
// action is the whole implementation.

// Get the closed vocabulary of ComplexityClass wire values.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Enums)")]
#pragma warning disable CS1591

public class ComplexityClassesController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns every ComplexityClass wire value, sorted. The closed vocabulary the GUI's Complexity Class facet filter must cover.</summary>
    ///<response code="200">Returns a sorted string array of ComplexityClass member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var values = Enum.GetNames(typeof(ComplexityClass)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        return Content(JsonSerializer.Serialize(values, options), "application/json");
    }
}

// Get the closed vocabulary of ProblemType wire values.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Enums)")]
#pragma warning disable CS1591

public class ProblemTypesController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns every ProblemType wire value, sorted. The closed vocabulary the GUI's Problem Type facet filter must cover.</summary>
    ///<response code="200">Returns a sorted string array of ProblemType member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var values = Enum.GetNames(typeof(ProblemType)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        return Content(JsonSerializer.Serialize(values, options), "application/json");
    }
}

// Get the closed vocabulary of ReductionType wire values.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Enums)")]
#pragma warning disable CS1591

public class ReductionTypesController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns every ReductionType wire value, sorted. The closed vocabulary the GUI's Reduction Type facet filter must cover.</summary>
    ///<response code="200">Returns a sorted string array of ReductionType member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var values = Enum.GetNames(typeof(ReductionType)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        return Content(JsonSerializer.Serialize(values, options), "application/json");
    }
}

// Get the closed vocabulary of ReductionCost wire values.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Enums)")]
#pragma warning disable CS1591

public class ReductionCostsController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns every ReductionCost wire value, sorted. The closed vocabulary the GUI's Reduction Cost facet filter must cover.</summary>
    ///<response code="200">Returns a sorted string array of ReductionCost member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var values = Enum.GetNames(typeof(ReductionCost)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        return Content(JsonSerializer.Serialize(values, options), "application/json");
    }
}

// Get the closed vocabulary of SolverType wire values.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Enums)")]
#pragma warning disable CS1591

public class SolverTypesController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns every SolverType wire value, sorted. The closed vocabulary the GUI's Solver Type facet filter must cover.</summary>
    ///<response code="200">Returns a sorted string array of SolverType member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var values = Enum.GetNames(typeof(SolverType)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        return Content(JsonSerializer.Serialize(values, options), "application/json");
    }
}

// Get the closed vocabulary of SolverComplexityBucket wire values.
[ApiController]
[Route("Navigation/[controller]")]
[Tags("- Navigation (Enums)")]
#pragma warning disable CS1591

public class SolverComplexityBucketsController : ControllerBase {
#pragma warning restore CS1591

    ///<summary>Returns every SolverComplexityBucket wire value, sorted. The closed vocabulary the GUI's Solver Complexity facet filter must cover.</summary>
    ///<response code="200">Returns a sorted string array of SolverComplexityBucket member names</response>

    [ProducesResponseType(typeof(string[]), 200)]
    [HttpGet]
    public IActionResult Get() {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var values = Enum.GetNames(typeof(SolverComplexityBucket)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        return Content(JsonSerializer.Serialize(values, options), "application/json");
    }
}
