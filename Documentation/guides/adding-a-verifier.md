# Adding a verifier

This guide walks you through adding a new **verifier** to Redux, from an empty file to a green pull request (PR). It uses a real, small verifier already in the repo as the worked example.

New to the repo? Read the [guides index](README.md) first. When you reach the "build and test" step, the details live in [building-and-testing.md](building-and-testing.md). A verifier is usually written together with a solver, so also see [adding-a-solver.md](adding-a-solver.md).

---

## 1. What a verifier is

A **verifier** checks a proposed answer to a problem. It does not find an answer. It only says "yes, this answer works" or "no, it does not".

Everyday analogy: someone hands you a filled-in Sudoku grid. Checking it is quick: look at each row, column, and box and see whether any digit repeats. Solving a blank Sudoku from scratch is much harder. A **solver** does the hard job (finding the answer). A verifier does the easy job (checking one).

Some words you will see:

- **Instance**: one specific question for a problem, written as a string. For Subset Sum, `({1,7,12,15},28)` means "can some of the numbers 1, 7, 12, 15 add up to 28?".
- **Certificate**: a proposed answer, also written as a string. For Subset Sum, `{1,12,15}` means "use 1, 12, and 15". It is called a certificate because it is the proof that the answer is yes.
- **Exception**: a way for code to say "something went wrong here" and stop. In C# you raise one with `throw`.

In Redux:

- A verifier is one C# class. It takes an instance and a certificate and returns `true` or `false`.
- Each problem has one **default verifier** (the problem class points to it). A problem may have extra verifiers too.
- You do not write a controller or register anything. Redux finds every verifier class automatically (see section 3), and the API exposes it as `POST /ProblemProvider/verify`.

## 2. Before you start

1. **The problem must already exist** in `Problems/NPComplete/` (or `NPHard/` or `P/`). If it does not, add it first, see [adding-a-problem.md](adding-a-problem.md).
2. Work on a branch based on `CSharpAPI`. Pull requests go back to `CSharpAPI`.

   ```bash
   git fetch origin
   git switch -c add-verifier-subsetsum origin/CSharpAPI
   ```

   Pick your own branch name, for example `add-verifier-<problem>`.
3. Make sure the project builds before you change anything. See [building-and-testing.md](building-and-testing.md).
4. Decide what a certificate looks like for your problem. Write it down as a short format plus an example (for Subset Sum: format `{K | K is set}`, example `{1,12,15}`). The problem class publishes this text as its `certificateFormat`, and the API quotes it back to callers who send a bad certificate.

## 3. Where the file goes

A verifier file lives inside the folder of the problem it checks, in a `Verifiers` folder:

```text
Problems/NPComplete/NPC_<PROBLEM>/Verifiers/<ClassName>.cs
```

Rules that matter:

| Thing | Rule | Worked example |
| --- | --- | --- |
| Folder | `NPC_<PROBLEM>/Verifiers/` (use `NPH_` under `NPHard/`, `P_` under `P/`) | `NPC_SUBSETSUM/Verifiers/` |
| File name | Same as the class name | `SubsetSumVerifier.cs` |
| Namespace | `API.Problems.NPComplete.NPC_<PROBLEM>.Verifiers` | `API.Problems.NPComplete.NPC_SUBSETSUM.Verifiers` |
| Class name | Unique across the whole project (see below) | `SubsetSumVerifier` |
| Implements | `IVerifier<PROBLEM>` | `IVerifier<SUBSETSUM>` |
| `verifierName` | Exactly `Default <Problem Name> Verifier`, where `<Problem Name>` is the problem's `problemName` text | `"Default Subset Sum Verifier"` |

**How Redux finds your verifier.** At startup, `ProblemProvider` ([AdditionalControllers/ProblemProvider.cs](../../AdditionalControllers/ProblemProvider.cs)) scans every class in the project. Any class that implements `IVerifier` is registered under its **lowercased class name**. Because of that:

- Your class name must be unique. Two classes with the same name (ignoring upper/lower case) crash startup.
- You do not edit any list. Creating the class is enough.
- The list of verifiers for a problem, at `GET /Navigation/Problem_VerifiersRefactor?chosenProblem=SUBSETSUM` ([Nav_Verifiers.cs](../../AdditionalControllers/Navigation/Nav_Verifiers.cs)), is built from the type argument of `IVerifier<PROBLEM>`. Pick it carefully.
- Being **listed** is different from being the **default**. Every verifier class for the problem shows up in that list. The one the problem class names in its `defaultVerifier` property is the default. To make your verifier the default, edit the problem class (for Subset Sum: [SUBSETSUM_Class.cs](../../Problems/NPComplete/NPC_SUBSETSUM/SUBSETSUM_Class.cs)). To add an extra verifier, leave `defaultVerifier` alone.

The API endpoint you get for free: `POST /ProblemProvider/verify?verifier=<ClassName>`, with a JSON body like `{"ProblemInstance": "...", "Certificate": "..."}`. Once the API is running (it normally listens on port 27000), try the worked example:

```bash
curl -X POST "http://localhost:27000/ProblemProvider/verify?verifier=SubsetSumVerifier" \
  -H "Content-Type: application/json" \
  -d '{"ProblemInstance": "({1,7,12,15},28)", "Certificate": "{1,12,15}"}'
```

Success looks like `"True"`. A well-formed but wrong certificate such as `{1,7}` gives `"False"`. A malformed one such as `{1,x,3}` gives HTTP 400 (see section 4, step 3).

You can also read any verifier's metadata with `GET /ProblemProvider/info?interface=SubsetSumVerifier`.

> The folder layout above matches the one in [ProblemTemplate/Templates/README.md](../../ProblemTemplate/Templates/README.md), but that file lists the interface fields only loosely. The interface itself, [Interfaces/VerifierInterface.cs](../../Interfaces/VerifierInterface.cs), is the source of truth.

## 4. Step by step

### Step 1. Get the template

You have two options. Both give you the same file.

- **Copy it by hand.** The template is [ProblemTemplate/Templates/Verifiers/ProblemVerifier.txt](../../ProblemTemplate/Templates/Verifiers/ProblemVerifier.txt). Copy it to your new folder, rename it `<ClassName>.cs`, and replace the placeholders `{PROBLEM}`, `{VERIFIER}`, and `{VERIFIER_PASCAL_CASE}`.
- **Download it from a running API.** Start the API, then call `GET /ProblemTemplate/verifier?problemName=SUBSETSUM&verifierName=My Subset Sum Verifier`. The two query parameters are `problemName` (the problem's class name, for example `SUBSETSUM`) and `verifierName` (the display name; the class name is made from it by removing spaces and odd characters). It returns a zip containing `NPC_SUBSETSUM/Verifiers/MySubsetSumVerifier.cs` and a README. The code for this is in [ProblemTemplate/ProblemTemplate.cs](../../ProblemTemplate/ProblemTemplate.cs).

A test requires the `verifierName` to be exactly `Default <Problem Name> Verifier` for **every** verifier, even a second one for the same problem (see section 5). The full-problem download (`GET /ProblemTemplate`) already generates that name. For the standalone verifier download, pass it as `verifierName` yourself (for example `Default Subset Sum Verifier`), or edit it afterwards.

**The template's `verify` ends with a `NotImplementedException`.** That is on purpose: a verifier you forgot to finish fails loudly instead of saying "yes" to everything. Replace it with your real check.

### Step 2. Fill in the members

Every verifier implements the members in [Interfaces/VerifierInterface.cs](../../Interfaces/VerifierInterface.cs). Here is each one in plain words, with the value used by the worked example, [`SubsetSumVerifier.cs`](../../Problems/NPComplete/NPC_SUBSETSUM/Verifiers/SubsetSumVerifier.cs).

| Member | What it is for | Worked example value |
| --- | --- | --- |
| `verifierName` | The name people see in the GUI. A test checks it is exactly `Default <Problem Name> Verifier` (see section 5). | `"Default Subset Sum Verifier"` |
| `verifierDefinition` | One sentence on what the verifier checks. | `"This is a verifier for Subset Sum"` |
| `source` | A citation for where the checking rule comes from. May be empty when it is just the problem's own definition. | `""` |
| `contributors` | Names of the people who wrote it. | `{ "Garret Stouffer" }` |
| `certificate` | A string property the interface requires. Existing verifiers keep it as an empty string and never use it. | `""` |
| `verify(PROBLEM, string)` | **The actual check.** Returns `true` or `false`, or throws for malformed input. | see Step 3 |

The template also adds `sourceLink` (set to `"TODO"`) and an optional `complexity` note. They are not part of the interface, but the template includes them, so fill them in or delete them.

Two more things you need:

- **A public constructor with no arguments** (`public SubsetSumVerifier() { }`). Redux builds your class this way (`Activator.CreateInstance`), and so do the tests.
- **Certificate format text** on the problem class. The problem's `certificateFormat` is what gets quoted back to a caller when their certificate is bad. The worked example keeps the grammar and an example as two constants on the verifier and builds the problem's text from them:

  ```csharp
  public const string CertificateGrammar = "{K | K is set}";
  public const string CertificateExample = "{1,12,15}";
  ```

  The problem class then says `$"Format: {SubsetSumVerifier.CertificateGrammar} Example: {SubsetSumVerifier.CertificateExample}"`. If you add a new default verifier, update the problem's `certificateFormat` so it describes what your verifier accepts.

Notice the method you write takes the **problem object** (`SUBSETSUM`), not a string. The interface turns the API's instance string into a problem object for you, and if the instance string is bad, it answers the caller with an error before your code runs.

### Step 3. Write `verify()`: throw versus return false

This is the part that matters most. A verifier has **three** possible outcomes, not two:

| Situation | What `verify` does | What the caller sees |
| --- | --- | --- |
| The certificate is well-formed and correct | returns `true` | HTTP 200, `"True"` |
| The certificate is well-formed but wrong | returns `false` | HTTP 200, `"False"` |
| The certificate is not in the right format at all | throws `CertificateParseException` | HTTP 400 with the problem's `certificateFormat` quoted back |

"Well-formed" means "I can read it". "Correct" means "it really answers the question". Examples for Subset Sum, instance `({1,7,12,15},28)`:

| Certificate | Readable? | Correct? | Result |
| --- | --- | --- | --- |
| `{1,12,15}` | yes | yes (1 + 12 + 15 = 28) | `true` |
| `{1,7}` | yes | no (the sum is 8) | `false` |
| `{1,99}` | yes | no (99 is not in the set) | `false` |
| `{7,7}` (when the set has only one 7) | yes | no (a number may not be used twice) | `false` |
| `{1,x,3}` | no (`x` is not a number) | not applicable | throws |
| `` (empty) | no | not applicable | throws |

Why bother with the difference? A caller (the GUI, a script, an AI agent) can learn from a 400 error that it sent the wrong shape and read the format hint. If the verifier returned `false` for garbage, the caller would think "that is a wrong answer" and never learn it is using the wrong format.

Here is the parsing half of the worked example. It uses SPADE (Redux's small text-reading library) to read `{1,12,15}` into a list, and turns any trouble into a `CertificateParseException`:

```csharp
StringParser parser = new(CertificateGrammar);
List<UtilCollection> elements;
try {
    parser.parse(certificate);
    // Materialize inside the try: SPADE may parse a scalar (e.g. "101")
    // without error and only fail when the result is enumerated.
    elements = parser["K"].ToList();
} catch (Exception ex) {
    throw new CertificateParseException(problem, certificate, ex.Message);
}
```

The three arguments to `CertificateParseException` are the problem object, the text you received, and an optional human-readable reason. The class is defined in [Interfaces/ParseExceptions.cs](../../Interfaces/ParseExceptions.cs).

The checking half then only returns `true` or `false`. It counts how many times each number appears in the instance, uses each number from the certificate at most that many times, adds them up, and compares to the target `T`. Anything that is not a member of the set, or is used too often, returns `false`. The last line is `return sum == problem.T;`.

Tips:

- **Parse first, judge second.** Turn the certificate text into data (and throw if you cannot) before you start deciding `true` or `false`. The worked example does it in this order for the format. One of its checks (`not an integer`) happens while it loops, so a bad later element is only noticed if an earlier element has not already caused a `false`. Parsing everything up front avoids this kind of order surprise.
- **Check the instance, not just the certificate.** A correct verifier must use the instance (`problem.S` and `problem.T` here). A verifier that only looks at the certificate cannot be right.
- **Safety net.** If your parsing code lets an ordinary parsing failure escape (`FormatException`, `IndexOutOfRangeException`, `KeyNotFoundException`, and similar), the interface converts it to a `CertificateParseException` for you (`ParseGuard.VerifyCertificate` in [ParseExceptions.cs](../../Interfaces/ParseExceptions.cs)). Do not rely on that. Throw it yourself with a clear message so the caller gets a useful hint. Other kinds of bug still surface as a 500 error, on purpose.
- **Keep it fast.** The solver for your problem may call your verifier once per candidate answer (the Subset Sum solver does this thousands of times), so avoid slow work inside `verify`.

## 5. Write tests

### Tests that already check your verifier for free

You do not have to register your verifier with these tests. They find it automatically by scanning every verifier class, and each one runs once per verifier.

**These tests will check your verifier automatically:**

| Test file | What it checks about your verifier |
| --- | --- |
| [NamingConvention_Tests.cs](../../redux-tests/Metadata/NamingConvention_Tests.cs) (`VerifierName_Equals_ProblemName_Plus_Verifier`) | Your class implements `IVerifier<T>`, the problem `T` builds with its default instance, your class builds with no arguments, and `verifierName` is exactly `Default <Problem Name> Verifier`. This applies to **every** verifier, including extra ones. |
| [ParseError_Endpoint_Tests.cs](../../redux-tests/Endpoints/ParseError_Endpoint_Tests.cs) (`Verify_GarbageCertificate_IsNever500`, `Verify_GarbageInstance_IsNever500`) | Sending garbage (`""`, `{{{`, `not an instance`, `{a,b}:::`) as the certificate, or as the instance, to `POST /ProblemProvider/verify` never produces an HTTP 500. A clean `true`, `false`, or 400 is fine. A raw crash is not. |
| [ProblemInstantiation_Tests.cs](../../redux-tests/Metadata/ProblemInstantiation_Tests.cs) | Your problem class builds with its default instance (your verifier hangs off it). |

Notice what is **not** checked for free: nothing tests that your verifier gives the right answer. The generic tests only prove the basics. Correctness is your job.

> If a metadata test names your class and fails, fix the class. Do not add it to an allowlist to make the test pass.

### Tests you write yourself

Verifier tests live in the test project, one folder per problem:

```text
redux-tests/Problems/NPC_<PROBLEM>/<PROBLEM>_Tests.cs
```

The worked example's tests are in [redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs](../../redux-tests/Problems/NPC_SUBSETSUM/SUBSETSUM_Tests.cs). If a test file for your problem exists, add to it. If not, create one. The pattern is three groups of tests, one per outcome from section 4, step 3. Here they are from the worked example:

```csharp
[Theory]
[InlineData("({1,7,12,15},28)", "{1,12,15}")] // 1 + 12 + 15 = 28
[InlineData("({5,1,2},5)", "{5}")]             // singleton {S[0]} is a valid witness
[InlineData("({3,5,9},8)", "{3,5}")]           // 3 + 5 = 8
public void SUBSETSUM_Verifier_Accepts_Valid_Certificate(string instance, string certificate) {
    SUBSETSUM problem = new SUBSETSUM(instance);
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    Assert.True(verifier.verify(problem, certificate));
}

[Theory]
[InlineData("({1,7,12,15},28)", "{1,7}")]   // sum 8 != 28
[InlineData("({1,7,12,15},28)", "{1,99}")]  // 99 is not a member of S
public void SUBSETSUM_Verifier_Rejects_Invalid_Certificate(string instance, string certificate) {
    SUBSETSUM problem = new SUBSETSUM(instance);
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    Assert.False(verifier.verify(problem, certificate));
}

[Theory]
[InlineData("")]          // empty — SPADE parse failure
[InlineData("{1,x,3}")]   // non-integer token
public void SUBSETSUM_Verifier_Throws_On_Malformed_Certificate(string certificate) {
    SUBSETSUM problem = new SUBSETSUM("({1,7,12,15},28)");
    SubsetSumVerifier verifier = new SubsetSumVerifier();
    Assert.Throws<CertificateParseException>(() => verifier.verify(problem, certificate));
}
```

Things to copy from the existing tests:

- `using Xunit;`, `using API.Interfaces;` (that is where `CertificateParseException` lives), and `using` lines for your problem's namespace and its `Verifiers` namespace.
- `namespace redux_tests;` and a `public class <PROBLEM>_Tests`.
- `[Theory]` with `[InlineData(...)]` to run one test body on many inputs, and `[Fact]` for a single check. Test names read like sentences: `<PROBLEM>_Verifier_<What>`.
- Use **small hand-picked instances** whose answers you worked out on paper.

**Edge-case checklist.** Aim to cover each of these:

- [ ] A valid certificate (accepts).
- [ ] The smallest valid certificate, for example one element, or the empty answer if the problem allows it.
- [ ] A well-formed certificate that is wrong (rejects, returns `false`). Try several kinds of wrong: wrong total, a value that is not in the instance, a value used too many times, a missing piece.
- [ ] A malformed certificate (throws `CertificateParseException`): a non-number where a number belongs, unbalanced braces, wrong shape.
- [ ] An empty string `""` (usually throws, since it is not in the format; say in a comment whichever your problem does).
- [ ] The default instance with the problem's own solver output (see the next test).

**Connect it to the solver.** If your problem has a solver, add a test that the solver's output passes your verifier. The worked example does:

```csharp
string solution = solver.solve(problem);
Assert.True(verifier.verify(problem, solution));
```

See [adding-a-solver.md](adding-a-solver.md) for the solver side.

## 6. Build, test, and get CI green

Run these from the repo root (the folder that contains `Redux.slnx`). The full explanation, with what each result looks like and what to do when CI is red, is in [building-and-testing.md](building-and-testing.md).

```bash
dotnet format Redux.slnx
dotnet build Redux.slnx -c Release
dotnet test Redux.slnx -c Release --filter "Category!=Performance"
dotnet test Redux.slnx -c Release --filter "Category=Performance"
```

To run only your own tests while you work:

```bash
dotnet test Redux.slnx -c Release --filter "FullyQualifiedName~SUBSETSUM_Verifier"
```

(Replace the text after `~` with part of your test name.) Success looks like `Passed!` with `Failed: 0`.

## 7. Checklist before opening the PR

- [ ] The problem already exists, and its `certificateFormat` describes what your verifier accepts.
- [ ] The file is at `Problems/NPComplete/NPC_<PROBLEM>/Verifiers/<ClassName>.cs`, and the namespace matches the folder.
- [ ] The class name is unique in the whole project.
- [ ] The class implements `IVerifier<PROBLEM>` and has a public constructor with no arguments.
- [ ] `verifierName` is `Default <Problem Name> Verifier`; `verifierDefinition`, `source`, and `contributors` are filled in (no `"TODO"` left).
- [ ] The template's `NotImplementedException` is gone. `verify` really checks the certificate against the instance.
- [ ] A malformed certificate throws `CertificateParseException`. A well-formed wrong one returns `false`.
- [ ] If this is the problem's default verifier, the problem class's `defaultVerifier` points to it.
- [ ] You added tests for valid, invalid, malformed, and empty certificates.
- [ ] `dotnet format Redux.slnx` was run, and build and tests pass.
- [ ] You read the rbs report on your PR (see [building-and-testing.md](building-and-testing.md)).
- [ ] The PR targets `CSharpAPI`.

## 8. Common mistakes

- **Leaving the template's `NotImplementedException`, or replacing it with `return true;`.** The first makes every call fail with a 500, and the second accepts everything. Always write real checks and a test that rejects a wrong answer.
- **Returning `false` for garbage.** Malformed input should throw `CertificateParseException` so the caller gets a 400 and the format hint. A wrong-but-readable answer is the only case for `false`.
- **Throwing for a wrong answer.** The opposite mistake: a readable certificate that does not solve the problem is `false`, not an exception.
- **Ignoring the instance.** `verify` must compare the certificate to the problem's data. Check that your test fails when you change the instance.
- **A `verifierName` that is not `Default <Problem Name> Verifier`.** A naming test fails with a message naming your class and the exact text it expected. This applies to extra verifiers too, so give an extra verifier a different class name but expect the same `verifierName` shape.
- **Reusing a class name.** Class names are keys, ignoring case. A duplicate crashes the registry for everyone.
- **Folder and namespace do not match**, or the file sits under the wrong problem.
- **A constructor that needs arguments.** Redux and the tests build verifiers with no arguments.
- **Forgetting to update `certificateFormat`** on the problem when you change the default verifier. Callers then get a wrong hint in error messages.
- **Letting an unexpected crash escape.** A bug in your checking code (not in parsing) becomes an HTTP 500. The garbage-input tests will catch many of these.
- **Writing a controller.** Not needed. Reflection exposes your class.
- **Editing test allowlists** to turn a failure green. Fix the cause.
