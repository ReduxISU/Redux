using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using API.Interfaces;
using API.Interfaces.JSON_Objects.Graphs;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using API.Interfaces.Tools;
using API.Interfaces.JSON_Objects;
using API.Tools;
using API.Problems.NPComplete.NPC_SAT3.ReduceTo.NPC_CLIQUE;
using API.Problems.NPComplete.NPC_CLIQUE.ReduceTo.NPC_VertexCover;
using Antlr4.Runtime;
using API.Tools.ApiParameters;
using System.Dynamic;

[ApiController]
[Route("[controller]")]
[Tags("Problem Provider")]
public class ProblemProvider : ControllerBase
{
    public static readonly Dictionary<string, Type> Problems = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IProblem).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);
    public static readonly Dictionary<string, Type> GraphProblems = Problems.Where(p => typeof(IGraphProblem).IsAssignableFrom(p.Value)).ToDictionary(x => x.Key, x => x.Value);
    public static readonly Dictionary<string, Type> Verifiers = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IVerifier).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);
    public static readonly Dictionary<string, Type> Solvers = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(ISolver).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);
    public static readonly Dictionary<string, Type> Visualizers = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IVisualization).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);
    public static readonly Dictionary<string, Type> Reductions = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => typeof(IReduction).IsAssignableFrom(p) && p.IsClass).ToDictionary(x => x.Name.ToLower(), x => x);
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
    [HttpPost("verify")]
    public string verify(string verifier, [FromBody] Verify verify)
    {
        // TODO: validate arguments
        return JsonSerializer.Serialize(
            Verifier(verifier).verify(verify.ProblemInstance, verify.Certificate).ToString(),
            new JsonSerializerOptions { WriteIndented = true }
        );
    }

    /// <summary>
    /// Takes a problem instance and a solver and returns the soltuion
    /// </summary>
    /// <param name="solver" example = "Sat3BacktrackingSolver">The solver to use</param>
    /// <param name="problemInstance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">The instance of the problem to solve</param>
    /// <returns>solution certificate</returns>
    [HttpPost("solve")]
    public string solve(string solver, [FromBody] string problemInstance)
    {
        // TODO: validate arguments
        return JsonSerializer.Serialize(
            Solver(solver).solve(problemInstance),
            new JsonSerializerOptions { WriteIndented = true }
        );
    }

    /// <summary>
    /// Gets info about a particular object
    /// </summary>
    /// <param name="interface" example = "SAT3">the name of the object to get the info of</param>
    /// <returns>an object instance</returns>
    [ProducesResponseType(typeof(object), 200)]
    [HttpGet("info")]
    public string info(string @interface)
    {
        // TODO: validate arguments
        return Newtonsoft.Json.JsonConvert.SerializeObject(Activator.CreateInstance(Interfaces[@interface.ToLower()]));
    }

    /// <summary>
    /// Makes an instance of a problem from an instance string
    /// </summary>
    /// <param name="problem" example = "3SAT">problem name</param>
    /// <param name="problemInstance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance string</param>
    /// <returns>object instance</returns>
    [ProducesResponseType(typeof(IProblem), 200)]
    [HttpPost("problemInstance")]
    public string problemInstance(string problem, [FromBody] string problemInstance)
    {
        // TODO: validate arguments
        return Newtonsoft.Json.JsonConvert.SerializeObject(ProblemInstance(problem, problemInstance));
    }

    private string getVisualize(IVisualization visualization, List<string> steps, string solution, string instance)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        options.Converters.Add(new API_JSON_Converter<API_JSON>());

        API_JSON visual = visualization.visualize(instance);
        List<API_JSON> apiSteps = visualization.StepsVisualization(instance, steps);
        API_JSON solutionJson = visualization.SolvedVisualization(instance, solution);

        List<API_JSON> list = new List<API_JSON> { visual };
        list.AddRange(apiSteps);
        if (solution.GetType() != typeof(API_empty)) list.Add(solutionJson);

        return JsonSerializer.Serialize(list, options);
    }

    /// <summary>
    /// Gets the visualization of an object
    /// </summary>
    /// <param name="visualization" example = "Sat3DefaultVisualization">The visualization to use</param>
    /// <param name="solver" example = "Sat3BacktrackingSolver">The solver to use for steps and solution</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance of the problem</param>
    /// <returns>a list containing the basic visualization, any steps from the solver, and the solved visualization</returns>
    [HttpPost("visualize")]
    public string visualize(string visualization, string solver, [FromBody] string instance)
    {
        return getVisualize(Visualization(visualization), Solver(solver).GetSteps(instance), Solver(solver).solve(instance), instance);
    }

    /// <summary>
    /// Reduces a problem and returns the visualization of the reduction
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">List of reductions to use, seperated by a dash. Can use a single reduction</param>
    /// <param name="solver" example = "Sat3BacktrackingSolver">The solver to use for steps and solution</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance string of the problem</param>
    /// <returns>a list containing the basic visualization, any steps from the solver, and the solved visualization</returns>
    [HttpPost("visualizeReduction")]
    public string visualizeReduction(string reduction, string solver, [FromBody] string instance)
    {
        List<string> reds = reduction.Split("-").ToList();

        ISolver sol = Solver(solver);
        List<string> steps = sol.GetSteps(instance);
        string solution = sol.solve(instance);

        IReduction? red = null;
        foreach (string reductionname in reds)
        {
            red = Reduction(reductionname, instance);
            steps = steps.Select(step => red.mapSolutions(step)).ToList();
            solution = red.mapSolutions(solution);
            instance = red.reductionTo.instance;
        }

        return getVisualize(red.visualization, steps, solution, instance);
    }

    /// <summary>
    /// returns the reduction of a problem
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">the reduction to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; !x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of a problem</param>
    /// <returns>reduction</returns>
    [HttpPost("reduce")]
    public string reduce(string reduction, [FromBody] string instance)
    {
        return JsonSerializer.Serialize(Reduction(reduction, instance), new JsonSerializerOptions() { WriteIndented = true });
    }

    /// <summary>
    /// Maps the solution from one problem to another
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">reduction to use</param>
    /// <param name="solution" example = "(x1:True)">solution certificate</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">instance of the problem</param>
    /// <returns>solution certificate of the reduction</returns>
    [HttpPost("mapSolution")]
    public string mapSolution(string reduction, string solution, [FromBody] string instance)
    {
        IReduction red = Reduction(reduction, instance);
        string mappedSolution = red.mapSolutions(solution);
        return JsonSerializer.Serialize(mappedSolution, new JsonSerializerOptions() { WriteIndented = true });
    }


    /// <summary>
    /// Gets the gadget map of a reduction
    /// </summary>
    /// <param name="reduction" example = "SipserReduceToCliqueStandard">reduction to use</param>
    /// <param name="instance" example = "(x1 | !x2 | x3) &amp; (!x1 | x3 | x1) &amp; (x2 | !x3 | x1)">the instance of the problem</param>
    /// <returns>gadget map</returns>
    [HttpPost("gadgets")]
    public string gadgets(string reduction, [FromBody] string instance)
    {
        IReduction red = Reduction(reduction, instance);
        return JsonSerializer.Serialize(red.gadgets, new JsonSerializerOptions() { WriteIndented = true });
    }
}

public class Verify
{
    public string Certificate { get; set; } = "";
    public string ProblemInstance { get; set; } = "";
}
