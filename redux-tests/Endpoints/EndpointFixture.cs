using API.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace redux_tests;
#pragma warning disable CS1591

// Shared WebApplicationFactory — boots the real app in-process once per test class.
//
// WebApplicationFactory.SetContentRoot normally walks up looking for a .sln file.
// This repo has no .sln, so that throws before ConfigureWebHost runs. Setting the
// ASPNETCORE_TEST_CONTENTROOT_API env var makes the factory use it directly instead.
// ProjectSourcePath.Value is resolved at compile time via [CallerFilePath], so it
// is correct on both local machines and CI.
//
// Solves run in the server here, not in worker processes (ComputeLimits:IsolateSolves, on in appsettings.json): about a
// hundred test classes each start a host, and a warm pool of workers for each would use gigabytes of memory and slow the
// suite down. SolveWorkers_Endpoint_Tests turns it back on for its own host.
public class AppFactory : WebApplicationFactory<Program> {
    public AppFactory() {
        Environment.SetEnvironmentVariable(
            "ASPNETCORE_TEST_CONTENTROOT_API",
            ProjectSourcePath.Value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [ComputeLimits.IsolateSolvesKey] = "false" }));
    }
}
