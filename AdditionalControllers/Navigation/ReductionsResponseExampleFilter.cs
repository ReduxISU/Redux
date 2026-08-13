using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

/// <summary>
/// Supplies a concrete example for the Reductions endpoint's 200 response so Swagger
/// renders real problem-name keys (e.g. "VERTEXCOVER" → "SETCOVER") instead of the
/// generic "additionalProp1" placeholders it auto-generates for Dictionary&lt;string, …&gt;.
/// </summary>
internal sealed class ReductionsResponseExampleFilter : IOperationFilter {
    private const string ExampleJson = """
    {
      "VERTEXCOVER": {
        "SETCOVER": [
          {
            "className": "KarpVertexCoverToSetCover",
            "endpoint": "POST /ProblemProvider/reduce?reduction=KarpVertexCoverToSetCover",
            "inputType": "VERTEXCOVER",
            "outputType": "SETCOVER"
          }
        ],
        "NODESET": [
          {
            "className": "KarpVertexCoverToNodeSet",
            "endpoint": "POST /ProblemProvider/reduce?reduction=KarpVertexCoverToNodeSet",
            "inputType": "VERTEXCOVER",
            "outputType": "NODESET"
          }
        ]
      }
    }
    """;

    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context) {
        if (context.MethodInfo?.DeclaringType != typeof(ReductionsController))
            return;
        if (operation.Responses is null || !operation.Responses.TryGetValue("200", out var response))
            return;
        if (response is not OpenApiResponse concrete || concrete.Content is null)
            return;

        // Set on every media type Swashbuckle emits (the action returns string, so the
        // response can carry application/json, text/json and text/plain).
        foreach (var media in concrete.Content.Values)
            media.Example = JsonNode.Parse(ExampleJson);
    }
}
