using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;

// `dotnet API.dll --compute-worker` runs one compute job for the server's worker pool and exits, without a web host (#936).
if (args.Length > 0 && args[0] == API.Interfaces.ComputeWorker.Argument)
    return API.Interfaces.ComputeWorker.Run();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<QuantumSolverSettings>(
    builder.Configuration.GetSection("QuantumSolver"));
builder.Services.AddQuantumHttpClient();
// The cap on compute jobs running at once (#936); see API.Interfaces.ComputeJobLimiter.
builder.Services.AddSingleton<API.Interfaces.ComputeJobLimiter>();
// The warm worker processes that run each compute job on its own (#936, #991), when ComputeLimits:IsolateSolves is on; the pool
// reads the setting when it starts, so it is registered either way and starts no process when the setting is off.
builder.Services.AddSingleton<API.Interfaces.SolveWorkerPool>();
builder.Services.AddHostedService<API.Interfaces.SolveWorkerPoolService>();

// A solver whose outside service fails answers 503 solver_unavailable on every endpoint (#625).
builder.Services.AddControllers(options => options.Filters.Add(new API.Interfaces.SolverUnavailableFilter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new OpenApiInfo {
        Version = "v1",
        Title = "Redux API",
        Description = "An ASP.NET Core Web API for reducing, verifying, and solving NP-Complete problems",
        // TermsOfService = new Uri("https://example.com/terms"),
        // Contact = new OpenApiContact
        // {
        //     Name = "Example Contact",
        //     Url = new Uri("https://example.com/contact")
        // },
        // License = new OpenApiLicense
        // {
        //     Name = "Example License",
        //     Url = new Uri("https://example.com/license")
        // }
    });

    options.CustomSchemaIds(type => type.FullName);

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

    // Real-key example for /Navigation/Reductions (replaces additionalProp1 placeholders).
    options.OperationFilter<ReductionsResponseExampleFilter>();
});
var app = builder.Build();

app.UseStaticFiles();

var optionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<QuantumSolverSettings>>();
QuantumSolverSettingsGlobal.QuantumSolver = optionsMonitor.CurrentValue;
QuantumSolverSettingsGlobal.HttpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();

// Somewhat of a security concern. But since we are not doing POSTS im not concerned about it
app.Use((context, next) => {
    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    return next.Invoke();
});

// app.UseHttpsRedirection();
app.UseCors(x => x
                .AllowAnyMethod()
                .AllowAnyHeader()
                .SetIsOriginAllowed(origin => true) // allow any origin
                .AllowCredentials()); // allow credentials

app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
    c.InjectStylesheet("/assests/css/swagger.css")
);


// Liveness/readiness probe (issue #621). A minimal-API route mapped directly on
// `app` rather than a controller: it must answer without touching problem data,
// reflection scans, or anything else the *NavigationData caches do at startup, so
// it stays cheap even if those caches are slow or mid-initialization. Plain-text
// body (not JSON) since callers only need a fast, trivially-parseable liveness
// signal — rbs.toml already points its container health check at this path.
app.MapGet("/health", () => Results.Text("ok", "text/plain"));

app.MapControllers();


app.Run();
return 0;

/// <summary>Application entry point and composition root for the API.</summary>
public partial class Program { }
