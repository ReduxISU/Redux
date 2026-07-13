// This is a support file to get the root file directory to resolve a relative path issues with Window vs Linux
// Basically, Windows uses the root file directory so, Redux/Problems, but Linux uses /bin/Debug/net6.0/Problems.
// This makes it so you can always just use the root(Widows on any system)
// E.G: string projectSourcePath = ProjectSourcePath.Value; will return the root path, then you can concatinate where ever you're going after like Problems/...

// Solution is based off StackOverflow (https://stackoverflow.com/a/66285728)

using System.Runtime.CompilerServices;

// Lets the test project (assembly name "redux-tests") see internal members:
// ProjectSourcePath below, and the top-level internal Program class used by
// WebApplicationFactory<Program> in redux-tests/Endpoints/EndpointFixture.cs.
[assembly: InternalsVisibleTo("redux-tests")]

internal static class ProjectSourcePath
{
    private const  string  myRelativePath = nameof(ProjectSourcePath) + ".cs";
    private static string? lazyValue;
    public  static string  Value => lazyValue ??= calculatePath();
    private static string calculatePath()
    {
        string pathName = GetSourceFilePathName();
        //Assert( pathName.EndsWith( myRelativePath, StringComparison.Ordinal ) );
        return pathName.Substring( 0, pathName.Length - myRelativePath.Length );
    }

    public static string GetSourceFilePathName( [CallerFilePath] string? callerFilePath = null)
        => callerFilePath ?? "";
}