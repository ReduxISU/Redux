using API.Interfaces.JSON_Objects;
using API.Interfaces.JSON_Objects.Graphs;
using API.Tools;

namespace API.Interfaces;

interface IVisualization {
    string visualizationName { get; }
    string visualizationDefinition { get; }
    // A tag set (see Tags): every renderer that can draw this visualization's payload. The JSON lists them as
    // visualizationTypes; the single visualizationType field is deprecated and holds the first.
    VisualizationType visualizationType { get; }
    string source { get; }
    // Repo-relative path of the file declaring this class; see SourceFile.
    string sourceFile { get; }
    // Link to the formal citation in `source`. Optional, so it defaults to "". [JsonIgnore] keeps it out
    // of JSON written through an interface-typed member (e.g. /ProblemProvider/reduce), which never had
    // it; a class that declares its own sourceLink still shows it in /info.
    [System.Text.Json.Serialization.JsonIgnore]
    string sourceLink { get => ""; }
    string[] contributors { get; }
    ISolver solver { get; }
    API_JSON visualize(string problem);
    API_JSON SolvedVisualization(string problem, string solution);
    List<API_JSON> StepsVisualization(string problem, List<Object> steps);

    /// <summary>
    /// The answer shape (<c>TPartial</c> of <see cref="API.Interfaces.Steps.SolverStep{TPartial}"/>) this
    /// visualization draws steps for; null means it draws legacy untyped steps. Implement explicitly
    /// (<c>Type? IVisualization.StepShape =&gt; ...</c>) so it does not appear as a public member in /info.
    /// Ignored by JSON: an object written through an <see cref="IVisualization"/>-typed property (the reduce
    /// endpoint does this) would otherwise serialize this <see cref="Type"/>, which System.Text.Json rejects.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    Type? StepShape => null;
}

interface IVisualization<U> : IVisualization where U : IProblem {
    API_JSON IVisualization.visualize(string problem) {
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        return visualize(ParseGuard.CreateProblem<U>(problem));
    }
    API_JSON visualize(U problem);

    API_JSON IVisualization.SolvedVisualization(string problem, string solution) {
        if (solution == "") return visualize(problem);
        // Should there be some sort of contraint that assures there is a constructor
        // that matches the signature of a single `string` argument?
        // Perhaps a static `FromInstance(string instance)` method for `IProblem` will work.
        return SolvedVisualization(ParseGuard.CreateProblem<U>(problem), solution);
    }
    API_JSON SolvedVisualization(U problem, string solution) {
        return new API_empty();
    }

    List<API_JSON> IVisualization.StepsVisualization(string problem, List<Object> steps) {
        if (steps.Count == 0)
            return new List<API_JSON>();
        return StepsVisualization(ParseGuard.CreateProblem<U>(problem), steps);
    }

    List<API_JSON> StepsVisualization(U problem, List<Object> steps) {
        return new List<API_JSON>();
    }
}

// Narrows visualize(U)'s return type from the bare API_JSON marker to the concrete payload
// shape a visualization actually produces, so callers can work with the real type instead of
// duck-typing on `kind` (see #524). Scoped to `visualize` only: SolvedVisualization and
// StepsVisualization keep returning API_JSON/List<API_JSON> at the IVisualization<U> level
// because their default bodies hand back API_empty()/an empty list regardless of TPayload,
// and IsEmptyVisualization() (AdditionalControllers/ProblemProvider.cs) depends on that
// API_empty sentinel — widening those to TPayload would break it for every class that leaves
// the defaults in place.
interface IVisualization<U, TPayload> : IVisualization<U> where U : IProblem where TPayload : API_JSON {
    new TPayload visualize(U problem);
    API_JSON IVisualization<U>.visualize(U problem) => visualize(problem);
}
