using System.Text.RegularExpressions;
using API.Interfaces;
using Xunit;

namespace redux_tests;
#pragma warning disable CS1591

// A problem's Solvers folder holds only solvers, so a contributor who opens it to read or copy a solver finds nothing else
// there. Shared code sits next to it: a data structure or an instance fact at the problem's root (NPC_SAT3/SatFormula.cs,
// TSP.Weight). A solver writes the steps it records in its own file, so a problem has no Steps folder: the words of a step
// belong to the solver that takes it, and the recorders shared by many solvers are in Interfaces/Steps. A solver may still
// split its own class over several files of the folder; every part is the solver.
//
// The files are read as text: every top-level type is declared at the start of a line, under a file-scoped namespace.
[Trait("Category", "Metadata")]
public class SolverFolder_Tests {
    private static readonly Regex TopLevelType = new(
        @"^(?:(?:public|internal|sealed|static|abstract|partial|file|readonly|unsafe)\s+)*(?:class|record(?:\s+(?:class|struct))?|struct|interface|enum|delegate\s+\S+)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<generic><[^>]*>)?",
        RegexOptions.Multiline);
    private static readonly Regex FileNamespace = new(@"^namespace\s+(?<name>[A-Za-z0-9_.]+)\s*;", RegexOptions.Multiline);

    public static TheoryData<string> SolverFiles() {
        var data = new TheoryData<string>();
        string problems = Path.Combine(ProjectSourcePath.Value, "Problems");
        foreach (string file in Directory.EnumerateFiles(problems, "*.cs", SearchOption.AllDirectories)
                     .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "Solvers")
                     .Order(StringComparer.Ordinal))
            data.Add(Path.GetRelativePath(ProjectSourcePath.Value, file).Replace('\\', '/'));
        return data;
    }

    [Fact]
    public void SolverFolders_AreFound() => Assert.True(SolverFiles().Count() > 50);

    [Theory]
    [MemberData(nameof(SolverFiles))]
    public void EveryTypeInASolversFolder_IsASolver(string file) {
        string text = File.ReadAllText(Path.Combine(ProjectSourcePath.Value, file));
        var ns = FileNamespace.Match(text);
        Assert.True(ns.Success, $"{file} has no file-scoped namespace.");
        var types = TopLevelType.Matches(text);
        Assert.True(types.Count > 0, $"{file} declares no top-level type.");

        var assembly = typeof(ISolver).Assembly;
        foreach (Match m in types) {
            string name = m.Groups["name"].Value;
            int arity = m.Groups["generic"].Success ? m.Groups["generic"].Value.Count(c => c == ',') + 1 : 0;
            string fullName = $"{ns.Groups["name"].Value}.{name}" + (arity > 0 ? $"`{arity}" : "");
            Type? type = assembly.GetType(fullName);
            Assert.True(type != null, $"{file}: no type {fullName} in the API assembly.");
            Assert.True(typeof(ISolver).IsAssignableFrom(type),
                $"{file} declares {name}, which is not a solver (ISolver). Move it out of the Solvers folder: shared code goes next "
                + "to the problem class, and a solver's steps in the solver itself.");
        }
    }

    [Fact]
    public void NoProblemHasAStepsFolder() {
        string problems = Path.Combine(ProjectSourcePath.Value, "Problems");
        var folders = Directory.EnumerateDirectories(problems, "Steps", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(ProjectSourcePath.Value, d).Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToList();
        Assert.True(folders.Count == 0,
            $"{string.Join(", ", folders)}: a problem has no Steps folder. Write a solver's steps in the solver; put an instance fact "
            + "on the problem class, a picture's ids and names with its frames in Interfaces, and a recorder many solvers share in "
            + "Interfaces/Steps.");
    }
}
