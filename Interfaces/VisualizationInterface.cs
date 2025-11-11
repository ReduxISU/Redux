using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Tools;

namespace API.Interfaces;

interface IVisualization
{
    string visualizationName { get; }
    string visualizationDefinition { get; }
    string visualizationType { get; }
    string source { get; }
    string[] contributors { get; }
    API_JSON visualize(string problem);
    API_JSON SolvedVisualization(string problem, string solution);

    List<API_JSON> StepsVisualization(string problem, List<string> steps);
}

interface IVisualization<U> : IVisualization where U : IProblem
{
    API_JSON IVisualization.visualize(string problem)
    {
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        return visualize((U)Activator.CreateInstance(typeof(U), problem));
    }
    API_JSON visualize(U problem);

    API_JSON IVisualization.SolvedVisualization(string problem, string solution)
    {
        if (solution == "") return visualize(problem);
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        return SolvedVisualization((U)Activator.CreateInstance(typeof(U), problem), solution);
    }
    API_JSON SolvedVisualization(U problem, string solution)
    {
        return new API_empty();
    }

    List<API_JSON> IVisualization.StepsVisualization(string problem, List<string> steps)
    {
        if (steps.Count == 0)
            return new List<API_JSON>();
        return stepsVisualization((U)Activator.CreateInstance(typeof(U), problem), steps);
    }

    List<API_JSON> stepsVisualization(U problem, List<string> steps)
    {
        return new List<API_JSON>();
    }
}
