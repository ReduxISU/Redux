using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.RegularExpressions;

[ApiController]
[Route("[controller]")]
[Tags("Problem Template")]
#pragma warning disable CS1591
public class ProblemTemplate : ControllerBase
{
#pragma warning restore CS1591

    ///<summary>Returns generated files zipped together for the user to implement.</summary>
    ///<param name="problemName" example="Traveling Sales Person">Problem name</param>
    ///<response code="200">Returns the problem template with the given name.</response>
    [ProducesResponseType(typeof(ActionResult), 200)]
    [HttpGet]
    public ActionResult DownloadProblemTemplate([FromQuery] string problemName)
    {
        string problemNameUpper = ToUpperCase(problemName);
        string problemNamePascel = ToPascelCase(problemName);
        string templatePath = $"{ProjectSourcePath.Value}/ProblemTemplate/Templates";

        byte[] zip = ZipFiles(
            new Dictionary<string, string>{
                {"README.md", System.IO.File.ReadAllText($"{ProjectSourcePath.Value}/ProblemTemplate/Templates/README.md")},
                {$"NPC_{problemNameUpper}/{problemNamePascel}Graph.cs", GenerateProblemTemplate(problemName, System.IO.File.ReadAllText($"{templatePath}/ProblemGraph.txt"))},
                {$"NPC_{problemNameUpper}/{problemNameUpper}_Class.cs", GenerateProblemTemplate(problemName, System.IO.File.ReadAllText($"{templatePath}/PROBLEM_Class.txt"))},
                {$"NPC_{problemNameUpper}/{problemNameUpper}_Controller.cs", GenerateProblemTemplate(problemName, System.IO.File.ReadAllText($"{templatePath}/PROBLEM_Controller.txt"))},
                {$"NPC_{problemNameUpper}/Solvers/{problemNamePascel}Solver.cs", GenerateSolverTemplate(problemName, $"{problemName} Solver", System.IO.File.ReadAllText($"{templatePath}/Solvers/ProblemSolver.txt"))},
                {$"NPC_{problemNameUpper}/Verifiers/{problemNamePascel}Verifier.cs", GenerateVerifierTemplate(problemName, $"{problemName} Verifier", System.IO.File.ReadAllText($"{templatePath}/Verifiers/ProblemVerifier.txt"))},
            }
        );

        return File(zip, "application/force-download", "ProblemTemplate.zip");
    }

    ///<summary>Returns generated files zipped together for the user to implement.</summary>
    ///<param name="problemFrom" example="SAT3">Reduce from problem</param>
    ///<param name="problemTo" example="CLIQUE">Reduce to problem</param>
    ///<param name="reductionName" example="Clique to 3 SAT reduction">Reduction name</param>
    ///<response code="200">Returns the reduction template with the given name.</response>
    [ProducesResponseType(typeof(ActionResult), 200)]
    [HttpGet("reduction")]
    public ActionResult DownloadReductionTemplate([FromQuery] string problemFrom, [FromQuery] string problemTo, [FromQuery] string reductionName)
    {
        string reductionNamePascel = ToPascelCase(reductionName);
        string templatePath = $"{ProjectSourcePath.Value}/ProblemTemplate/Templates";

        byte[] zip = ZipFiles(
            new Dictionary<string, string>{
                {$"NPC_{problemFrom}/ReduceTo/NPC_{problemTo}/{reductionNamePascel}.cs", GenerateReductionTemplate(problemFrom, problemTo, reductionName, System.IO.File.ReadAllText($"{templatePath}/ReduceTo/NPC_PROBLEM/Reduction.txt"))},
                {$"NPC_{problemFrom}/{reductionNamePascel}_Controller.cs", GenerateReductionTemplate(problemFrom, problemTo, reductionName, System.IO.File.ReadAllText($"{templatePath}/Reduction_Controller.txt"))},
            }
        );

        return File(zip, "application/force-download", "ReductionTemplate.zip");
    }

    ///<summary>Returns generated files zipped together for the user to implement.</summary>
    ///<param name="problemName" example="CLIQUE">Problem name</param>
    ///<param name="solverName" example="My Clique Solver">Solver name</param>
    ///<response code="200">Returns the solver template with the given name.</response>
    [ProducesResponseType(typeof(ActionResult), 200)]
    [HttpGet("solver")]
    public ActionResult DownloadSolverTemplate([FromQuery] string problemName, [FromQuery] string solverName)
    {
        string solverNamePascel = ToPascelCase(solverName);
        string templatePath = $"{ProjectSourcePath.Value}/ProblemTemplate/Templates";

        byte[] zip = ZipFiles(
            new Dictionary<string, string>{
                {$"NPC_{problemName}/Solvers/{solverNamePascel}.cs", GenerateSolverTemplate(problemName, solverName, System.IO.File.ReadAllText($"{templatePath}/Solvers/ProblemSolver.txt"))},
                {$"NPC_{problemName}/{solverNamePascel}_Controller.cs", GenerateSolverTemplate(problemName, solverName, System.IO.File.ReadAllText($"{templatePath}/Solver_Controller.txt"))},
            }
        );

        return File(zip, "application/force-download", "SolverTemplate.zip");
    }

    ///<summary>Returns generated files zipped together for the user to implement.</summary>
    ///<param name="problemName" example="CLIQUE">Problem name</param>
    ///<param name="verifierName" example="My Clique Verifier">Verifier name</param>
    ///<response code="200">Returns the verifier template with the given name.</response>
    [ProducesResponseType(typeof(ActionResult), 200)]
    [HttpGet("verifier")]
    public ActionResult DownloadVerifierTemplate([FromQuery] string problemName, [FromQuery] string verifierName)
    {
        string verifierNamePascel = ToPascelCase(verifierName);
        string templatePath = $"{ProjectSourcePath.Value}/ProblemTemplate/Templates";

        byte[] zip = ZipFiles(
            new Dictionary<string, string>{
                {$"NPC_{problemName}/Verifiers/{verifierNamePascel}.cs", GenerateVerifierTemplate(problemName, verifierName, System.IO.File.ReadAllText($"{templatePath}/Verifiers/ProblemVerifier.txt"))},
                {$"NPC_{problemName}/{verifierNamePascel}_Controller.cs", GenerateVerifierTemplate(problemName, verifierName, System.IO.File.ReadAllText($"{templatePath}/Verifier_Controller.txt"))},
            }
        );

        return File(zip, "application/force-download", "VerifierTemplate.zip");
    }

    static string GenerateProblemTemplate(string problemName, string template)
    {
        if (string.IsNullOrEmpty(problemName))
        {
            problemName = "Problem";
        }

        return template
            .Replace("{NAME_UPPERCASE}", ToUpperCase(problemName))
            .Replace("{NAME_PASCEL_CASE}", ToPascelCase(problemName))
            .Replace("{NAME_CAMEL_CASE}", ToCamelCase(problemName))
            .Replace("{NAME}", problemName);
    }

    static string GenerateSolverTemplate(string problemName, string solverName, string template)
    {
        if (string.IsNullOrEmpty(problemName))
        {
            problemName = "Problem";
        }
        if (string.IsNullOrEmpty(solverName))
        {
            solverName = "Problem";
        }

        return template
            .Replace("{PROBLEM}", problemName)
            .Replace("{SOLVER}", solverName)
            .Replace("{SOLVER_PASCEL_CASE}", ToPascelCase(solverName));
    }

    static string GenerateVerifierTemplate(string problemName, string verifierName, string template)
    {
        if (string.IsNullOrEmpty(problemName))
        {
            problemName = "Problem";
        }
        if (string.IsNullOrEmpty(verifierName))
        {
            verifierName = "Problem";
        }

        return template
            .Replace("{PROBLEM}", problemName)
            .Replace("{VERIFIER}", verifierName)
            .Replace("{VERIFIER_PASCEL_CASE}", ToPascelCase(verifierName));
    }

    static string GenerateReductionTemplate(string problemFrom, string problemTo, string reductionName, string template)
    {
        if (string.IsNullOrEmpty(reductionName))
        {
            reductionName = "Reduction";
        }
        if (string.IsNullOrEmpty(problemFrom))
        {
            problemFrom = "ReduceFrom";
        }
        if (string.IsNullOrEmpty(problemTo))
        {
            problemTo = "ReduceTo";
        }

        return template
            .Replace("{REDUCE_FROM}", problemFrom)
            .Replace("{REDUCE_TO}", problemTo)
            .Replace("{REDUCTION_PASCEL_CASE}", ToPascelCase(reductionName))
            .Replace("{REDUCTION}", reductionName);
    }

    static byte[] ZipFiles(Dictionary<string, string> files)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Key);
                using var entryStream = entry.Open();
                using var streamWriter = new StreamWriter(entryStream);
                streamWriter.Write(file.Value);
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream.ToArray();
    }

    // String manipulation

    static string RemoveInvalidIdentifierChars(string name)
    {
        return new string((
            from c in name
            where char.IsLetterOrDigit(c) || c == '_'
            select c
       ).ToArray());
    }

    static string ToPascelCase(string name)
    {
        return FirstToUpper(RemoveInvalidIdentifierChars(Regex.Replace(name, " [a-z]", m => m.Value.ToUpper()[1..])));
    }

    static string ToCamelCase(string name)
    {
        return FirstToLower(ToPascelCase(name));
    }

    static string ToUpperCase(string s)
    {
        return RemoveInvalidIdentifierChars(s).ToUpper();
    }

    static string FirstToUpper(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }
        return s.First().ToString().ToUpper() + s[1..];
    }

    static string FirstToLower(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }
        return s.First().ToString().ToLower() + s[1..];
    }
}
