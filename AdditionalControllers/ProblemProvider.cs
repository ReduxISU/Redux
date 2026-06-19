using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects.Graphs;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using API.Interfaces.Tools;
using API.Interfaces.JSON_Objects;
using API.Tools;
using Antlr4.Runtime;
using API.Tools.ApiParameters;
using System.Dynamic;

/// <summary>
/// Core controller for Problems.
/// </summary>
[ApiController]
[Route("[controller]")]
[Tags("Problem Provider")]
public class ProblemProvider : ControllerBase
{
    /// <summary>A dictionary of all of the problems mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Problems = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IProblem).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);

    /// <summary>A dictionary of all of the graph problems in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> GraphProblems = Problems.Where(p => typeof(IGraphProblem).IsAssignableFrom(p.Value)).ToDictionary(x => x.Key, x => x.Value);

    /// <summary>A dictionary of all verifiers in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Verifiers = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IVerifier).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);

    /// <summary>A dictionary of all solvers in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Solvers = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(ISolver).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);

    /// <summary>A dictionary of all visualizers in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Visualizers = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IVisualization).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);

    /// <summary>A dictionary of all reductions in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Reductions = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IReduction).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);

    /// <summary>A dictionary of all problems, verifiers, solvers, visualizers and reductions in Redux mapped to their C# type.</summary>
    public static readonly Dictionary<string, Type> Interfaces = (new[] { Problems, Verifiers, Solvers, Visualizers,Reductions }).SelectMany(d => d).ToDictionary(x => x.Key, x => x.Value);

#pragma warning disable CS8603 // Possible null reference return.
    static IProblem Problem(string name)
    {
        return Activator.CreateInstance(Problems[name.ToLower()]) as IProblem; // guaranteed success by `IsAssignableFrom`
    }

    static IProblem ProblemInstance(string name, string instance)
    {
        return Activator.CreateInstance(Problems[name.ToLower()], instance) as IProblem; // guaranteed success by `IsAssignableFrom`
    }

    static IGraphProblem GraphProblem(string name, string instance)
    {
        return Activator.CreateInstance(GraphProblems[name.ToLower()], instance) as IGraphProblem; // guaranteed success by `IsAssignableFrom`
    }

    static IVerifier Verifier(string name)
    {
        return Activator.CreateInstance(Verifiers[name.ToLower()]) as IVerifier; // guaranteed success by `IsAssignableFrom`
    }

    static ISolver Solver(string name)
    {
        return Activator.CreateInstance(Solvers[name.ToLower()]) as ISolver; // guaranteed success by `IsAssignableFrom`
    }

    static IVisualization Visualization(string name)
    {
        return Activator.CreateInstance(Visualizers[name.ToLower()]) as IVisualization;
    }

    static IReduction Reduction(string name, string instance)
    {
        return Activator.CreateInstance(Reductions[name.ToLower()], instance) as IReduction;
    }

    static IReduction Reduction(string name)
    {
        return Activator.CreateInstance(Reductions[name.ToLower()]) as IReduction;
    }

#pragma warning restore CS8603 // Possible null reference return.

    /// <summary>
    /// Takes a problem instance, a solution certificate, and returns if that is a correct solution
    /// </summary>
    /// <param name="verifier" example = "cliqueverifier">the verifier to use</param>
    /// <param name="verify">The certificate and problem instance</param>
    /// <returns>true or false</returns>
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(400)]
    [HttpPost("verify")]
    public IActionResult verify(string verifier, [FromBody] Verify verify)
    {
        try {
            string result = JsonSerializer.Serialize(
                Verifier(verifier).verify(verify.ProblemInstance, verify.Certificate).ToString(),
                new JsonSerializerOptions { WriteIndented = true }
            );
            return Content(result, "application/json");
        } catch (ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        } catch (CertificateParseException ex) {
            return BadRequest(ParseErrorBody("certificate_parse_error", ex.Problem.problemName,
                ex.Problem.certificateFormat, ex.Received, ex.Message));
        }
    }

    private static object ParseErrorBody(string error, string problemName, string expected, string received, string detail) {
        return new {
            error,
            problem = problemName,
            expected,
            received,
            detail
        };
    }

    private static string LookupInstanceFormat(string problemName) {
        // problemName may be either the class name (Problems key) or the
        // friendly problemName property — scan both. Error path: O(N) is fine.
        if (Problems.TryGetValue(problemName.ToLower(), out var type)) {
            try { return (Activator.CreateInstance(type) as IProblem)?.instanceFormat ?? ""; }
            catch { /* fall through */ }
        }
        foreach (var kv in Problems) {
            try {
                if (Activator.CreateInstance(kv.Value) is IProblem p
                    && string.Equals(p.problemName, problemName, StringComparison.OrdinalIgnoreCase)) {
                    return p.instanceFormat;
                }
            } catch { /* skip */ }
        }
        return "";
    }

    /// <summary>
    /// Takes a problem instance and a solver and returns the solution
    /// </summary>
    /// <param name="solver" example = "Sat3BacktrackingSolver">The solver to use</param>
    /// <param name="problemInstance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">The instance of the problem to solve</param>
    /// <returns>solution certificate</returns>
    [HttpPost("solve")]
    [ProducesResponseType(400)]
    public IActionResult solve(string solver, [FromBody] string problemInstance)
    {
        if (!Solvers.TryGetValue(solver.ToLower(), out _))
            return BadRequest(new { error = "unknown_solver", received = solver });
        try {
            return Content(
                JsonSerializer.Serialize(
                    Solver(solver).solve(problemInstance),
                    new JsonSerializerOptions { WriteIndented = true }),
                "application/json");
        } catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        } catch (ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        }
    }

    /// <summary>
    /// Gets info about a particular object
    /// </summary>
    /// <param name="interface" example = "SAT3">the name of the object to get the info of</param>
    /// <returns>an object instance</returns>
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [HttpGet("info")]
    public IActionResult info(string @interface)
    {
        if (string.IsNullOrEmpty(@interface) || !Interfaces.TryGetValue(@interface.ToLower(), out var type))
            return BadRequest(new { error = "unknown_interface", received = @interface });
        return Content(Newtonsoft.Json.JsonConvert.SerializeObject(Activator.CreateInstance(type)), "application/json");
    }

    /// <summary>
    /// Makes an instance of a problem from an instance string
    /// </summary>
    /// <param name="problem" example = "3SAT">problem name</param>
    /// <param name="problemInstance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance string</param>
    /// <returns>object instance</returns>
    [ProducesResponseType(typeof(IProblem), 200)]
    [ProducesResponseType(400)]
    [HttpPost("problemInstance")]
    public IActionResult problemInstance(string problem, [FromBody] string problemInstance)
    {
        try {
            return Content(
                Newtonsoft.Json.JsonConvert.SerializeObject(ProblemInstance(problem, problemInstance)),
                "application/json");
        } catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        } catch (ProblemParseException ex) {
            return BadRequest(ParseErrorBody("instance_parse_error", ex.ProblemName,
                LookupInstanceFormat(ex.ProblemName), ex.Received, ex.Message));
        }
    }

    private static bool IsEmptyVisualization(API_JSON item)
    {
        if (item is API_empty) return true;

        if (item is API_QUANTUMCIRCUIT circuit)
        {
            bool hasSolution = !string.IsNullOrWhiteSpace(circuit.solution);
            bool hasQasm = !string.IsNullOrWhiteSpace(circuit.qasm);
            bool hasD3 = circuit.d3.HasValue && circuit.d3.Value.ValueKind != JsonValueKind.Undefined && circuit.d3.Value.ValueKind != JsonValueKind.Null;

            return !(hasSolution || hasQasm || hasD3);
        }

        return false;
    }

    private string getVisualize(IVisualization visualization, List<Object> steps, string solution, string instance)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        options.Converters.Add(new API_JSON_Converter());

        API_JSON visual = visualization.visualize(instance);
        List<API_JSON> apiSteps = visualization.StepsVisualization(instance, steps);
        API_JSON solutionJson = visualization.SolvedVisualization(instance, solution);

        List<API_JSON> list = new List<API_JSON>();

        if (!IsEmptyVisualization(visual))
            list.Add(visual);

        list.AddRange(apiSteps.Where(step => !IsEmptyVisualization(step)));

        if (!IsEmptyVisualization(solutionJson))
            list.Add(solutionJson);

        return JsonSerializer.Serialize(list, options);
    }

    /// <summary>
    /// Gets the visualization of an object
    /// </summary>
    /// <param name="visualization" example = "Sat3DefaultVisualization">The visualization to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance of the problem</param>
    /// <returns>a list containing the basic visualization, any steps from the solver, and the solved visualization</returns>
    [HttpPost("visualize")]
    [ProducesResponseType(400)]
    public IActionResult visualize(string visualization, [FromBody] string instance)
    {
        if (!Visualizers.TryGetValue(visualization.ToLower(), out _))
            return BadRequest(new { error = "unknown_visualization", received = visualization });
        var vis = Visualization(visualization);
        return Content(getVisualize(vis, vis.solver.GetSteps(instance), vis.solver.solve(instance), instance), "application/json");
    }

    /// <summary>
    /// Reduces a problem and returns the visualization of the reduction
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">List of reductions to use, separated by a dash. Can use a single reduction</param>
    /// <param name="solution" example = "(x1:True,x2:True)">The solution to visualize</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance string of the problem</param>
    /// <returns>a list containing the basic visualization, any steps from the solver, and the solved visualization</returns>
    [HttpPost("visualizeReduction")]
    [ProducesResponseType(400)]
    public IActionResult visualizeReduction(string reduction, string solution, [FromBody] string instance)
    {
        List<string> reds = reduction.Split("-").ToList();
        foreach (string r in reds)
            if (!Reductions.TryGetValue(r.ToLower(), out _))
                return BadRequest(new { error = "unknown_reduction", received = r });

        IReduction? red = null;
        foreach (string reductionname in reds)
        {
            red = Reduction(reductionname, instance);
            solution = red.mapSolutions(solution);
            instance = red.reductionTo.instance;
        }

        if (red is null)
            return BadRequest(new { error = "no_reductions_provided" });

        return Content(getVisualize(red.visualization, new List<Object>(), solution, instance), "application/json");
    }

    /// <summary>
    /// returns the reduction of a problem
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">the reduction to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of a problem</param>
    /// <returns>reduction</returns>
    [HttpPost("reduce")]
    [ProducesResponseType(400)]
    public IActionResult reduce(string reduction, [FromBody] string instance)
    {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        return Content(JsonSerializer.Serialize(Reduction(reduction, instance), new JsonSerializerOptions() { WriteIndented = true }), "application/json");
    }

    /// <summary>
    /// Maps the solution from one problem to another
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">reduction to use</param>
    /// <param name="solution" example = "(x1:True)">solution certificate</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of the problem</param>
    /// <returns>solution certificate of the reduction</returns>
    [HttpPost("mapSolution")]
    [ProducesResponseType(400)]
    public IActionResult mapSolution(string reduction, string solution, [FromBody] string instance)
    {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        IReduction red = Reduction(reduction, instance);
        string mappedSolution = red.mapSolutions(solution);
        return Content(JsonSerializer.Serialize(mappedSolution, new JsonSerializerOptions() { WriteIndented = true }), "application/json");
    }


    /// <summary>
    /// Gets the gadget map of a reduction
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">reduction to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance of the problem</param>
    /// <returns>gadget map</returns>
    [HttpPost("gadgets")]
    [ProducesResponseType(400)]
    public IActionResult gadgets(string reduction, [FromBody] string instance)
    {
        if (!Reductions.TryGetValue(reduction.ToLower(), out _))
            return BadRequest(new { error = "unknown_reduction", received = reduction });
        IReduction red = Reduction(reduction, instance);
        return Content(JsonSerializer.Serialize(red.gadgets, new JsonSerializerOptions() { WriteIndented = true }), "application/json");
    }
}

/// <summary>Request body for verify endpoint</summary>
public class Verify
{
    /// <summary>
    /// Proposed solution for this problem
    /// </summary>
    public string Certificate { get; set; } = "";

    /// <summary>Instance of the problem</summary>
    public string ProblemInstance { get; set; } = "";
}
