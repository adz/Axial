namespace Axial.Guardrails.Tests

open System
open System.IO
open FSharp.Analyzers.SDK
open Axial.Guardrails
open Xunit

[<CollectionDefinition("ExternalSource", DisableParallelization = true)>]
type ExternalSourceCollection() = class end

[<Collection("ExternalSource")>]
module ExternalSourceTests =
    type Fixture =
        { Repo: string
          Project: string
          Internal: string
          External: string
          Config: string
          BaselineOutput: string }

    let private write (path: string) (text: string) =
        Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
        File.WriteAllText(path, text)

    let private fixture () =
        let root = Path.Combine(Path.GetTempPath(), "Axial.Guardrails.External", Guid.NewGuid().ToString("N"))
        let repo = Path.Combine(root, "repo")
        let project = Path.Combine(repo, "sample.fsproj")
        let internalFile = Path.Combine(repo, "Internal.fs")
        let externalFile = Path.Combine(root, ".nuget", "packages", "xunit.v3.extensibility.core", "4.0.1", "_content", "DefaultRunnerReporters.fs")
        let policy = Path.Combine(repo, "Axial.Guardrails.toml")
        let config = Path.Combine(repo, "guardrails.config")
        let baseline = Path.Combine(repo, "Axial.Guardrails.baseline")
        let baselineOutput = Path.Combine(root, "generated.baseline")
        write project """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Compile Include="Internal.fs" /><Compile Include="../.nuget/packages/xunit.v3.extensibility.core/4.0.1/_content/DefaultRunnerReporters.fs" /></ItemGroup></Project>"""
        write internalFile "module Internal\nlet value = 1\n"
        write externalFile "module DefaultRunnerReporters\nlet value = 1\n"
        write baseline "# baseline\n"
        write policy """version = 2
default.role = "domain"

[projects]
test = ["**/*.fsproj"]
"""
        write config (String.concat "\n" [
            "guardrail\tAxial"
            // Exercise every analyzer's source classification even though this fixture has a test project role.
            "setting\tinTests\tall"
            $"setting\tpolicyRoot\t{repo}"
            $"setting\tpolicyFile\t{policy}"
            $"setting\tprojectPath\t{project}"
            $"setting\tbaselineRoot\t{repo}"
            $"setting\tbaselineOutput\t{baselineOutput}"
        ])
        Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, config)
        { Repo = repo; Project = project; Internal = internalFile; External = externalFile; Config = config; BaselineOutput = baselineOutput }

    let private cleanup (f: Fixture) =
        Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, null)
        try Directory.Delete(Path.GetDirectoryName f.Repo, true) with _ -> ()

    let private analyze (f: Fixture) analyzer source =
        task {
            let! ctx = AnalyzerTestContext.createAt f.External source
            return! analyzer ctx |> Async.StartAsTask
        }

    [<Fact>]
    let ``desktop startLayer blocking bridge requires its exact declared boundary`` () = task {
        let f = fixture ()
        let policy = Path.Combine(f.Repo, "Axial.Guardrails.toml")
        write (Path.Combine(f.Repo, "Other.fs")) "module Other\n"
        let source marked =
            "module Desktop\nopen Axial\nopen Axial.ElmishFlows\n"
            + "let start options layer input =\n"
            + "    ElmishRuntime.startLayer options layer input |> Async.RunSynchronously"
            + (if marked then " // axial-allow-AXG001: desktop startup owns this blocking host boundary" else "")
            + "\n"
        let analyze declared marked = task {
            write policy ("version = 2\n[default]\nrole = \"host\"\n[boundaries]\nfiles = [\"" + declared + "\"]\n")
            // GuardrailConfig caches the TSV (not policy mtimes), just as an analyzer host does per build.
            let config = f.Config + declared + string marked
            File.Copy(f.Config, config, true)
            Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, config)
            let! ctx = AnalyzerTestContext.createWithAxialAt f.Internal (source marked)
            let errors = ctx.CheckFileResults.Diagnostics |> Array.filter (fun d -> d.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error)
            Assert.Empty errors
            return! EffectBoundaryAnalyzer.effectBoundaryAnalyzer ctx |> Async.StartAsTask
        }
        try
            let! unmarked = analyze "Internal.fs" false
            Assert.Contains(unmarked, fun message -> message.Code = "AXG001")
            let! wrongFile = analyze "Other.fs" true
            Assert.Contains(wrongFile, fun message -> message.Code = "AXG001")
            let! accepted = analyze "Internal.fs" true
            Assert.DoesNotContain(accepted, fun message -> message.Code = "AXG001")
        finally
            cleanup f
    }

    let private allSources =
        "module DefaultRunnerReporters\n"
        + "open System\n"
        + "let ambient = DateTime.UtcNow\n"
        + "let raw = Threading.Tasks.Task.FromResult 1\n"
        + "let invalid = 1 // axial-note AXG010: not a handler\n"
        + "let formatting = sprintf \"%A\" [1]\n"
        + "let raising () = raise (InvalidOperationException())\n"

    [<Fact>]
    let ``external packaged source is skipped by every default test analyzer`` () =
        task {
            let f = fixture ()
            try
                let source = "module DefaultRunnerReporters\nopen System\nlet value = DateTime.UtcNow\nlet wait = Threading.Thread.Sleep 1\n"
                let! fixtureMessages = analyze f FixtureAnalyzer.fixtureAnalyzer source
                let! waitMessages = analyze f WaitForFactsAnalyzer.waitForFactsAnalyzer source
                let! concurrencyMessages = analyze f ConcurrencyHazardsAnalyzer.rawConcurrencyAnalyzer source
                let! integrityMessages = analyze f SuppressionIntegrityAnalyzer.suppressionIntegrityAnalyzer source
                Assert.Empty fixtureMessages
                Assert.Empty waitMessages
                Assert.Empty concurrencyMessages
                Assert.Empty integrityMessages
                Assert.False(File.Exists f.BaselineOutput)
            finally cleanup f
        }

    [<Fact>]
    let ``external source skip is shared across all analyzers`` () =
        task {
            let f = fixture ()
            try
                let! effect = analyze f EffectBoundaryAnalyzer.effectBoundaryAnalyzer allSources
                let! raw = analyze f ConcurrencyHazardsAnalyzer.rawConcurrencyAnalyzer allSources
                let! swallowed = analyze f ConcurrencyHazardsAnalyzer.swallowedExceptionAnalyzer allSources
                let! uncancellable = analyze f ConcurrencyHazardsAnalyzer.uncancellableWorkAnalyzer allSources
                let! discarded = analyze f ConcurrencyHazardsAnalyzer.discardedWorkAnalyzer allSources
                let! integrity = analyze f SuppressionIntegrityAnalyzer.suppressionIntegrityAnalyzer allSources
                let! formatting = analyze f ReflectionFormattingAnalyzer.reflectionFormattingAnalyzer allSources
                let! raising = analyze f RaiseInFlowAnalyzer.raiseInFlowAnalyzer allSources
                let! fixtureMessages = analyze f FixtureAnalyzer.fixtureAnalyzer allSources
                let! wait = analyze f WaitForFactsAnalyzer.waitForFactsAnalyzer allSources
                for messages in [ effect; raw; swallowed; uncancellable; discarded; integrity; formatting; raising; fixtureMessages; wait ] do
                    Assert.Empty messages
                Assert.False(File.Exists f.BaselineOutput)
            finally cleanup f
        }

    [<Fact>]
    let ``relative compiler source resolves against the project directory`` () =
        task {
            let f = fixture ()
            try
                let! ctx = AnalyzerTestContext.createAt f.Internal "module Internal\nopen System\nlet value = DateTime.UtcNow\n"
                let relative = { ctx with FileName = "Internal.fs" }
                let! messages = EffectBoundaryAnalyzer.effectBoundaryAnalyzer relative |> Async.StartAsTask
                Assert.NotEmpty messages
                let generated = File.ReadAllLines f.BaselineOutput
                Assert.NotEmpty generated
                for line in generated do Assert.Equal("Internal.fs", line.Split('\t')[1])
                // Re-analyze with the generated baseline and a relative compiler filename.
                File.AppendAllText(f.Config, "\n" + (generated |> Array.map (fun line -> "baseline\t" + line) |> String.concat "\n"))
                let! baselined = EffectBoundaryAnalyzer.effectBoundaryAnalyzer relative |> Async.StartAsTask
                Assert.All(baselined, fun message -> Assert.Equal(Severity.Warning, message.Severity))
            finally cleanup f
        }

    [<Fact>]
    let ``unusable source path without a policy root does not throw`` () =
        task {
            Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, null)
            let! ctx = AnalyzerTestContext.createAt (Path.Combine(Path.GetTempPath(), "Axial.Guardrails.Unusable.fs")) "module Unusable\nopen System\nlet value = DateTime.UtcNow\n"
            let unusable = { ctx with FileName = "bad\000name.fs" }
            let! messages = EffectBoundaryAnalyzer.effectBoundaryAnalyzer unusable |> Async.StartAsTask
            Assert.Empty messages
        }

    [<Fact>]
    let ``internal forbidden sample still reports under the same policy`` () =
        task {
            let f = fixture ()
            try
                let! ctx = AnalyzerTestContext.createAt f.Internal "module Internal\nopen System\nlet value = DateTime.UtcNow\n"
                let! messages = EffectBoundaryAnalyzer.effectBoundaryAnalyzer ctx |> Async.StartAsTask
                Assert.NotEmpty messages
            finally cleanup f
        }

    [<Fact>]
    let ``external sources never enter lock inventory`` () =
        task {
            let f = fixture ()
            try
                File.WriteAllText(f.External, "module DefaultRunnerReporters\nlet value = 1 // axial-note AXG010: outside\n")
                let baselinePath = Path.Combine(f.Repo, "Axial.Guardrails.baseline")
                File.WriteAllText(baselinePath, "AXG010\t../.nuget/packages/xunit.v3.extensibility.core/4.0.1/_content/DefaultRunnerReporters.fs\t" + String('0', 64) + "\n")
                let snapshot =
                    match Axial.Tool.PolicyLock.inspect f.Repo with
                    | Ok snapshot -> snapshot
                    | Error error -> failwith error
                Assert.False(snapshot.BaselineRoles.ContainsKey("../.nuget/packages/xunit.v3.extensibility.core/4.0.1/_content/DefaultRunnerReporters.fs"))
                Assert.DoesNotContain(snapshot.Suppressions, fun entry -> entry.Contains("DefaultRunnerReporters.fs"))

                let before = snapshot.Suppressions
                File.WriteAllText(f.External, "module DefaultRunnerReporters\nlet value = 2 // axial-note AXG010: changed outside\n")
                let afterExternal = (Axial.Tool.PolicyLock.inspect f.Repo |> Result.defaultWith failwith).Suppressions
                Assert.Equal<Set<string>>(before, afterExternal)

                File.WriteAllText(f.Internal, "module Internal\nlet value = 2 // axial-note AXG010: internal change\n")
                let afterInternal = (Axial.Tool.PolicyLock.inspect f.Repo |> Result.defaultWith failwith).Suppressions
                Assert.NotEqual<Set<string>>(before, afterInternal)
            finally cleanup f
        }

[<Collection("ExternalSource")>]
module SourceClassificationTests =
    open Axial.Guardrails.Policy
    open System.Text.RegularExpressions

    [<Theory>]
    [<InlineData("AXIAL_GUARDRAILS_SEVERITY")>]
    [<InlineData("AxialGuardrailsSeverity")>]
    let ``standalone analyzer rejects the removed severity environment setting on every load`` (setting: string) =
        let before = Environment.GetEnvironmentVariable setting
        Environment.SetEnvironmentVariable(setting, "warning")
        try
            for _ in 1..2 do
                let error = Assert.Throws<InvalidOperationException>(fun () -> GuardrailConfig.load () |> ignore)
                Assert.Contains("error AXIALGR001", error.Message)
                Assert.Contains("policy-roles", error.Message)
                Assert.Contains("Axial.Guardrails.toml", error.Message)
        finally
            Environment.SetEnvironmentVariable(setting, before)

    let private tempRoot () =
        let root = Path.Combine(Path.GetTempPath(), "Axial.SourceClassification", Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        root

    let private expectRepository root path =
        match classifySource root path with
        | RepositorySource relative -> relative
        | other -> failwith $"expected repository source, got {other}"

    let private expectExternal root path =
        match classifySource root path with
        | ExternalSource -> ()
        | other -> failwith $"expected external source, got {other}"

    [<Fact>]
    let ``classification uses directory containment not a name prefix`` () =
        let root = tempRoot ()
        try
            let sibling = root + "-other"
            Directory.CreateDirectory sibling |> ignore
            let inside = Path.Combine(root, "nested", "File.fs")
            Directory.CreateDirectory(Path.GetDirectoryName inside) |> ignore
            File.WriteAllText(inside, "module File")
            let outside = Path.Combine(sibling, "File.fs")
            File.WriteAllText(outside, "module File")
            Assert.Equal("nested/File.fs", expectRepository root inside)
            expectExternal root outside
            expectExternal root (Path.Combine(root, "..", Path.GetFileName sibling, "File.fs"))
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``project relative internal link stays in the repository`` () =
        let root = tempRoot ()
        try
            let project = Path.Combine(root, "src", "Project")
            Directory.CreateDirectory(project) |> ignore
            let linked = Path.Combine(root, "shared", "Linked.fs")
            Directory.CreateDirectory(Path.GetDirectoryName linked) |> ignore
            File.WriteAllText(linked, "module Linked")
            let viaProject = Path.Combine(project, "..", "..", "shared", "Linked.fs")
            Assert.Equal("shared/Linked.fs", expectRepository root viaProject)
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``relative source resolves against the supplied project directory`` () =
        let root = tempRoot ()
        try
            let project = Path.Combine(root, "src", "Project")
            Directory.CreateDirectory(project) |> ignore
            let linked = Path.Combine(root, "shared", "Linked.fs")
            Directory.CreateDirectory(Path.GetDirectoryName linked) |> ignore
            File.WriteAllText(linked, "module Linked")
            match classifySourceFrom project root "../../shared/Linked.fs" with
            | RepositorySource relative -> Assert.Equal("shared/Linked.fs", relative)
            | other -> failwith $"expected repository source, got {other}"
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``filesystem root is preserved as a containment root`` () =
        let root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))
        let probe = Path.Combine(root, "AxialClassification", "File.fs")
        match classifySource root probe with
        | RepositorySource relative -> Assert.Equal("AxialClassification/File.fs", relative.Replace('\\', '/'))
        | other -> failwith $"expected repository source under the filesystem root, got {other}"

    [<Fact>]
    let ``malformed source path is invalid rather than an exception`` () =
        let root = tempRoot ()
        try
            match classifySource root "" with
            | InvalidSource _ -> ()
            | other -> failwith $"expected invalid source, got {other}"
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``file symlink escaping the root is external`` () =
        if OperatingSystem.IsWindows() then () else
        let root = tempRoot ()
        try
            let outside = Path.Combine(root, "..", Path.GetFileName root + "-outside.fs")
            File.WriteAllText(outside, "module Outside")
            let link = Path.Combine(root, "Linked.fs")
            File.CreateSymbolicLink(link, outside) |> ignore
            expectExternal root link
        finally Directory.Delete(root, true)
