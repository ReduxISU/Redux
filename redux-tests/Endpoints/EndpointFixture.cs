using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace redux_tests;
#pragma warning disable CS1591

// Shared WebApplicationFactory — boots the real app in-process once per test class.
// Content root is set to the project source directory so static files and config load correctly.
public class AppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(ProjectSourcePath.Value);
    }
}
