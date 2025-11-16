using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Tools;

namespace API.Interfaces;

interface ISolver {
    string solverName{get;}
    string solverDefinition{get;}
    string source {get;}
    string[] contributors { get; }

    string solve(string problem);

    List<string> GetSteps(string instance)
    {
        return new List<string>();
    }
}

interface ISolver<T> : ISolver where T : IProblem {
    string ISolver.solve(string problem) {
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        return solve((T)Activator.CreateInstance(typeof(T), problem));
    }
    string solve(T problem);


    List<string> ISolver.GetSteps(string instance)
    {
        return GetSteps((T)Activator.CreateInstance(typeof(T), instance));
    }

    List<string> GetSteps(T problem)
    {
        return new List<string>();
    }
}
