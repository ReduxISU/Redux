using System.Runtime.CompilerServices;

namespace API.Interfaces;

// Backs the `sourceFile` property on problems, verifiers, visualizations, solvers,
// and reductions.
// [CallerFilePath] is filled in by the compiler at the call site, so each class must call
// this from its own source file (typically `public string sourceFile { get; } = SourceFile.Path();`).
// That is why `sourceFile` is a required interface member rather than a default
// implementation: a default body would report the interface's file, not the class's.
static class SourceFile {
    // Repo-relative, '/'-separated (e.g. "Problems/NPComplete/NPC_CLIQUE/CLIQUE_Class.cs"),
    // so the value is the same on every build machine and doesn't leak absolute paths.
    public static string Path([CallerFilePath] string callerFilePath = "") {
        string root = ProjectSourcePath.Value;
        string relative = callerFilePath.StartsWith(root, StringComparison.Ordinal)
            ? callerFilePath.Substring(root.Length)
            : callerFilePath;
        return relative.Replace('\\', '/');
    }
}
