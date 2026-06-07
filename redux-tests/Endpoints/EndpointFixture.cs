using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace redux_tests;
#pragma warning disable CS1591

// Shared WebApplicationFactory — boots the real app in-process once per test class.
//
// WebApplicationFactory.SetContentRoot normally walks up looking for a .sln file.
// This repo has no .sln, so that throws before ConfigureWebHost runs. Setting the
// ASPNETCORE_TEST_CONTENTROOT_API env var makes the factory use it directly instead.
// ProjectSourcePath.Value is resolved at compile time via [CallerFilePath], so it
// is correct on both local machines and CI.
public class AppFactory : WebApplicationFactory<Program>
{
    public AppFactory()
    {
        Environment.SetEnvironmentVariable(
            "ASPNETCORE_TEST_CONTENTROOT_API",
            ProjectSourcePath.Value);
    }
}
