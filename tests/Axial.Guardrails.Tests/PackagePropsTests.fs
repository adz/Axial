namespace Axial.Guardrails.Tests

open System
open System.Diagnostics
open System.IO
open Xunit

/// Verifies the consumer-facing behaviour of `build/Axial.Guardrails.props`, which a `dotnet add
/// package Axial.Guardrails` import exercises. A real consumer gets the analyzer pre-built inside the
/// restored nupkg; this test imports the same props/targets files and points `_AxialGuardrailsAnalyzersPath`
/// at this repository's own build output, so it checks the props mapping without packing.
module PackagePropsTests =
    let private repoRoot () =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))

    let private configuration () =
        if AppContext.BaseDirectory.Contains(string Path.DirectorySeparatorChar + "release" + string Path.DirectorySeparatorChar) then
            "release"
        else
            "debug"

    let private analyzersPath () = Path.Combine(repoRoot (), "artifacts", "bin", "Axial.Guardrails", configuration ())

    let private nuGetRoot () =
        match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
        | null
        | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")
        | root -> root

    let private cliDll () =
        Path.Combine(nuGetRoot (), "fsharp-analyzers", "0.37.2", "tools", "net8.0", "any", "FSharp.Analyzers.Cli.dll")

    let private policyToolDll () =
        Path.Combine(repoRoot (), "artifacts", "bin", "Axial.Tool", configuration (), "Axial.Tool.dll")

    let private dotnetHost () =
        match Environment.GetEnvironmentVariable "DOTNET_ROOT" with
        | null | "" -> "dotnet"
        | root -> Path.Combine(root, "dotnet")

    let private runPolicyTool dir arguments =
        let startInfo = ProcessStartInfo(dotnetHost ())
        startInfo.WorkingDirectory <- dir
        startInfo.ArgumentList.Add(policyToolDll ())
        arguments |> List.iter startInfo.ArgumentList.Add
        startInfo.UseShellExecute <- false
        // Simulate a caller that pins net8 when installed; analyzer launches must override this inherited policy.
        startInfo.EnvironmentVariables["DOTNET_ROLL_FORWARD"] <- "Major"
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        use child = Process.Start startInfo
        let output = child.StandardOutput.ReadToEnd() + child.StandardError.ReadToEnd()
        child.WaitForExit()
        Assert.True(child.ExitCode = 0, output)

    let private writeFixtureLock dir =
        // Construct a temporary consumer fixture; never invoke a human approval command.
        match Axial.Tool.PolicyLock.inspect dir with
        | Error error -> failwith error
        | Ok snapshot -> File.WriteAllText(Path.Combine(dir, Axial.Tool.PolicyLock.LockName), Axial.Tool.PolicyLock.serialize snapshot)

    let private writeV2Policy dir role level =
        runPolicyTool dir [ "init" ]
        File.WriteAllText(
            Path.Combine(dir, "Axial.Guardrails.toml"),
            $"version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n\n[default]\nrole = \"{role}\"\n\n[roles.domain.rules]\n\"effects.ambient\" = \"{level}\"\n")
        writeFixtureLock dir

    let private xmlEscape (value: string) =
        value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;")

    let private writeConsumerProject (dir: string) (frameworkProperty: string) (source: string) =
        let fsproj =
            $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    {frameworkProperty}
    <OutputType>Library</OutputType>
    <_AxialGuardrailsAnalyzersPath>{xmlEscape (analyzersPath ())}</_AxialGuardrailsAnalyzersPath>
    <_AxialGuardrailsTaskAssembly>{xmlEscape (Path.Combine(repoRoot (), "artifacts", "bin", "Axial.Guardrails.Build", configuration (), "Axial.Guardrails.Build.dll"))}</_AxialGuardrailsTaskAssembly>
    <_AxialGuardrailsExe>dotnet &quot;{xmlEscape (cliDll ())}&quot;</_AxialGuardrailsExe>
    <_AxialGuardrailsBootstrapProject>{xmlEscape (Path.Combine(repoRoot (), "src", "Axial.Guardrails", "Axial.Guardrails.fsproj"))}</_AxialGuardrailsBootstrapProject>
    <_AxialGuardrailsPolicyToolAssembly>{xmlEscape (policyToolDll ())}</_AxialGuardrailsPolicyToolAssembly>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.fs" />
    <AxialGuardrail Include="Axial" />
    <_AxialGuardrailsConfiguredToolInput Include="{xmlEscape (cliDll ())}" />
  </ItemGroup>
  <Import Project="{xmlEscape (Path.Combine(repoRoot (), "src", "Axial.Guardrails", "build", "Axial.Guardrails.props"))}" />
  <Import Project="{xmlEscape (Path.Combine(repoRoot (), "src", "Axial.Guardrails", "build", "Axial.Guardrails.targets"))}" />
</Project>
"""
        File.WriteAllText(Path.Combine(dir, "Consumer.fsproj"), fsproj)
        File.WriteAllText(Path.Combine(dir, "Program.fs"), source)

    let private writeConsumer (dir: string) =
        writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n"

    type BuildResult = { ExitCode: int; Output: string }

    let private runBuildWithEnvironment (environment: (string * string) list) (extraArguments: string list) (project: string) (framework: string option) =
        let startInfo = ProcessStartInfo(dotnetHost ())
        startInfo.ArgumentList.Add "build"
        startInfo.ArgumentList.Add project
        startInfo.ArgumentList.Add "-c"
        startInfo.ArgumentList.Add (configuration ())
        startInfo.ArgumentList.Add "-v"
        startInfo.ArgumentList.Add "minimal"
        startInfo.ArgumentList.Add "--nologo"
        startInfo.ArgumentList.Add "-nodeReuse:false"
        startInfo.ArgumentList.Add "-m:1"
        extraArguments |> List.iter startInfo.ArgumentList.Add
        match framework with
        | Some value ->
            startInfo.ArgumentList.Add "-f"
            startInfo.ArgumentList.Add value
        | None -> ()
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] <- "1"
        for key, value in environment do startInfo.EnvironmentVariables[key] <- value
        use child = Process.Start startInfo
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        let exited = child.WaitForExit(90_000)
        if not exited then
            child.Kill(true)
            child.WaitForExit()
        System.Threading.Tasks.Task.WaitAll(stdout, stderr)
        let output = stdout.Result + "\n" + stderr.Result
        Assert.True(exited, "Consumer build timed out and its process tree was killed:\n" + output)
        { ExitCode = child.ExitCode; Output = output }

    let private runBuildWithArgs (extraArguments: string list) (project: string) (framework: string option) =
        runBuildWithEnvironment [] extraArguments project framework

    let private runBuild project framework =
        runBuildWithArgs [] project framework

    let private buildConsumerInTemp write =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-consumer-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            write dir
            runBuild (Path.Combine(dir, "Consumer.fsproj")) None
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``slice2 modes separate stamps without changing role selection or environment defaults`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-modes-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            writeV2Policy dir "domain" "warn"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let report () =
                Directory.GetFiles(dir, "*.result.xml", SearchOption.AllDirectories)
                |> Array.exactlyOne |> System.Xml.Linq.XDocument.Load
            let attribute name = (report ()).Root.Attribute(System.Xml.Linq.XName.Get name).Value
            let build mode environment extra =
                let args = extra @ (mode |> Option.map (fun value -> ["-p:AxialGuardrailsMode=" + value]) |> Option.defaultValue [])
                let result = runBuildWithEnvironment environment args project None
                Assert.True(result.ExitCode = 0, result.Output)
                Assert.Contains("AXG001", result.Output)
                result.Output
            build None [] [] |> ignore
            Assert.Equal("Full", attribute "mode")
            let original = attribute "fingerprint"
            for mode in ["Fast"; "Full"; "Fast"] do
                let output = build (Some mode) [] []
                Assert.Contains("Running Axial.Guardrails analysis", output)
                Assert.Equal(mode, attribute "mode")
                Assert.All((report ()).Root.Elements(System.Xml.Linq.XName.Get "coverage"), fun entry ->
                    Assert.Equal(mode, entry.Attribute(System.Xml.Linq.XName.Get "mode").Value))
                let cached = build (Some mode) [] []
                Assert.DoesNotContain("Running Axial.Guardrails analysis", cached)
            build (Some "Fast") ["CI", "true"] ["-p:Configuration=Release"] |> ignore
            Assert.Equal("Fast", attribute "mode")
            build None ["CI", "true"] ["-p:Configuration=Release"] |> ignore
            Assert.Equal("Full", attribute "mode")
            build None ["CI", "false"] [] |> ignore
            Assert.Equal(original, attribute "fingerprint")
            let invalid = runBuildWithArgs ["-p:AxialGuardrailsMode=Auto"] project None
            Assert.NotEqual(0, invalid.ExitCode)
            Assert.Contains("AxialGuardrailsMode must be Fast or Full", invalid.Output)
        finally Directory.Delete(dir, true)

    [<Fact>]
    let ``slice2 invalid mode fails even when analysis is disabled`` () =
        let result = buildConsumerInTemp (fun dir ->
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework><AxialGuardrailsMode>Auto</AxialGuardrailsMode><AxialGuardrailsEnabled>false</AxialGuardrailsEnabled>" "module Consumer\n")
        Assert.NotEqual(0, result.ExitCode)
        Assert.Contains("AxialGuardrailsMode must be Fast or Full", result.Output)

    [<Theory>]
    [<InlineData("Fast")>]
    [<InlineData("Full")>]
    let ``slice2 test role selection is identical in both modes`` mode =
        let result = buildConsumerInTemp (fun dir ->
            writeConsumerProject dir ($"<TargetFramework>net10.0</TargetFramework><AxialGuardrailsMode>{mode}</AxialGuardrailsMode>") "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n"
            writeV2Policy dir "test" "deny")
        Assert.True(result.ExitCode = 0, result.Output)
        Assert.DoesNotContain("AXG001", result.Output)

    [<Theory>]
    [<InlineData("complete")>]
    [<InlineData("incomplete")>]
    [<InlineData("record")>]
    [<InlineData("finding")>]
    [<InlineData("warning")>]
    [<InlineData("review")>]
    let ``slice2 full check covers all TFMs fails closed and never writes policy`` scenario =
        let dir = Path.Combine(Path.GetTempPath(), "axial-full-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            let source = if scenario = "finding" || scenario = "warning" then "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n" else "module Consumer\nlet value = 42\n"
            writeConsumerProject dir "<TargetFrameworks>net8.0;net10.0</TargetFrameworks>" source
            writeV2Policy dir "domain" (if scenario = "finding" then "deny" else "warn")
            let other = Path.Combine(dir, "Other")
            Directory.CreateDirectory other |> ignore
            writeConsumerProject other "<TargetFramework>net10.0</TargetFramework>" "module Other\nlet value = 1\n"
            File.WriteAllText(Path.Combine(dir, "Consumer.slnx"), "<Solution><Project Path=\"Consumer.fsproj\" /><Folder Name=\"/Other/\"><Project Path=\"Other/Consumer.fsproj\" /></Folder></Solution>")
            // Full checks must override the ordinary designated-framework optimization.
            let consumer = Path.Combine(dir, "Consumer.fsproj")
            File.WriteAllText(consumer, (File.ReadAllText consumer).Replace("</Project>", "<PropertyGroup><_AxialGuardrailsDesignatedTargetFramework>net8.0</_AxialGuardrailsDesignatedTargetFramework></PropertyGroup></Project>"))
            if scenario = "incomplete" then
                let path = Path.Combine(dir, "Consumer.fsproj")
                File.WriteAllText(path, (File.ReadAllText path).Replace("</Project>", "<PropertyGroup><_AxialGuardrailsExe>echo incomplete</_AxialGuardrailsExe></PropertyGroup></Project>"))
            if scenario = "record" then
                let path = Path.Combine(dir, "Consumer.fsproj")
                let corrupt = "<Target Name=\"CorruptExportedCoverage\" AfterTargets=\"AxialGuardrailsEffectBoundaryCheck\"><ItemGroup><Export Include=\"$(_AxialGuardrailsFullCheckDirectory)/**/*.xml\" /></ItemGroup><WriteLinesToFile File=\"%(Export.Identity)\" Lines=\"&lt;result mode=&quot;Full&quot; complete=&quot;false&quot; /&gt;\" Overwrite=\"true\" /></Target>"
                File.WriteAllText(path, (File.ReadAllText path).Replace("</Project>", corrupt + "</Project>"))
            let git arguments =
                let info = ProcessStartInfo("git")
                info.WorkingDirectory <- dir
                info.RedirectStandardOutput <- true
                info.RedirectStandardError <- true
                for argument in arguments do info.ArgumentList.Add argument
                use child = Process.Start info
                Assert.True(child.WaitForExit(10000))
                Assert.True(child.ExitCode = 0, child.StandardError.ReadToEnd())
            git ["init"; "-q"]
            git ["config"; "user.email"; "tests@example.invalid"]
            git ["config"; "user.name"; "Test"]
            git ["add"; "."]
            git ["commit"; "-qm"; "fixture"]
            let lockPath = Path.Combine(dir, "Axial.Guardrails.lock")
            let snapshot = Axial.Tool.PolicyLock.inspect dir |> function Ok value -> value | Error error -> failwith error
            File.WriteAllText(lockPath, Axial.Tool.PolicyReview.prepareReviewedLock dir snapshot)
            if scenario = "review" then File.AppendAllText(Path.Combine(dir, "Program.fs"), "// axial-allow-AXG001: new boundary\n")
            let baselinePath = Path.Combine(dir, "Axial.Guardrails.baseline")
            let lockBytes = File.ReadAllBytes lockPath
            let baselineBytes = File.ReadAllBytes baselinePath
            let start = ProcessStartInfo(dotnetHost ())
            start.WorkingDirectory <- dir
            start.ArgumentList.Add(policyToolDll ())
            start.ArgumentList.Add "check"
            start.ArgumentList.Add "--full"
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true
            use child = Process.Start start
            let stdout = child.StandardOutput.ReadToEndAsync()
            let stderr = child.StandardError.ReadToEndAsync()
            if not (child.WaitForExit(180000)) then
                child.Kill(true)
                child.WaitForExit()
                failwith "Full check timed out"
            let output = stdout.Result + stderr.Result
            if scenario = "complete" then Assert.True(child.ExitCode = 0, output)
            else Assert.NotEqual(0, child.ExitCode)
            if scenario = "incomplete" then Assert.Contains("AXGINF001", output)
            elif scenario = "record" then Assert.Contains("Full analysis coverage is incomplete:", output)
            elif scenario = "finding" then Assert.Contains("AXG001", output)
            elif scenario = "warning" then Assert.Contains("Full analysis reported findings:", output)
            else
                if scenario = "review" then Assert.Contains("policy review", output)
                let reports = Directory.GetFiles(dir, "*.result.xml", SearchOption.AllDirectories)
                Assert.Equal(3, reports.Length)
                for report in reports do Assert.Contains("mode=\"Full\"", File.ReadAllText report)
            Assert.Equal<byte>(lockBytes, File.ReadAllBytes lockPath)
            Assert.Equal<byte>(baselineBytes, File.ReadAllBytes baselinePath)
        finally Directory.Delete(dir, true)

    [<Fact>]
    let ``slice1 cache report corruption reruns and cache hits replay findings with opt in timings`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-slice1-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            writeV2Policy dir "domain" "warn"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let first = runBuild project None
            Assert.True(first.ExitCode = 0, first.Output)
            Assert.Contains("AXG001", first.Output)
            let report = Directory.GetFiles(dir, "axial-guardrails.*.result.xml", SearchOption.AllDirectories) |> Array.exactlyOne
            let document = System.Xml.Linq.XDocument.Load report
            Assert.Equal("true", document.Root.Attribute(System.Xml.Linq.XName.Get "complete").Value)
            Assert.NotEmpty(document.Descendants(System.Xml.Linq.XName.Get "coverage"))
            Assert.Empty(Directory.GetFiles(dir, "axial-guardrails.*.timings.xml", SearchOption.AllDirectories))
            let memo = Directory.GetFiles(dir, "*.digests.xml", SearchOption.AllDirectories) |> Array.exactlyOne
            let memoTime = DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            File.SetLastWriteTimeUtc(memo, memoTime)
            let cached = runBuild project None
            Assert.Equal(memoTime, File.GetLastWriteTimeUtc memo)
            Assert.Equal(0, cached.ExitCode)
            Assert.DoesNotContain("Running Axial.Guardrails analysis", cached.Output)
            Assert.Contains("AXG001", cached.Output)
            File.WriteAllText(report, "<truncated")
            let rerun = runBuildWithArgs [ "-p:AxialGuardrailsTimings=true" ] project None
            Assert.Equal(0, rerun.ExitCode)
            Assert.Contains("Running Axial.Guardrails analysis", rerun.Output)
            let timings = Directory.GetFiles(dir, "axial-guardrails.*.timings.xml", SearchOption.AllDirectories) |> Array.exactlyOne
            let text = File.ReadAllText timings
            for phase in [ "startup"; "project-load"; "fcs-check"; "output"; "fingerprint"; "rule" ] do
                Assert.Contains(phase, text)
        finally Directory.Delete(dir, true)

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``slice1 analyzer exception fails closed with rule file and cause while retaining findings`` recordFailure =
        let dir = Path.Combine(Path.GetTempPath(), "axial-slice1-plugin-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            let plugin = Path.Combine(dir, "plugin")
            Directory.CreateDirectory plugin |> ignore
            let reference name =
                let path = Path.Combine(analyzersPath (), name + ".dll") |> xmlEscape
                $"<Reference Include=\"{name}\"><HintPath>{path}</HintPath></Reference>"
            let references = [ "Axial.Guardrails.Analyzers"; "FSharp.Analyzers.SDK"; "FSharp.Compiler.Service" ] |> List.map reference |> String.concat ""
            File.WriteAllText(Path.Combine(plugin, "Plugin.fsproj"), $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Plugin.Analyzers</AssemblyName></PropertyGroup><ItemGroup><Compile Include=\"Plugin.fs\" />{references}</ItemGroup></Project>")
            File.WriteAllText(Path.Combine(plugin, "Plugin.fs"), """module Plugin
open FSharp.Analyzers.SDK
[<CliAnalyzer("InjectedFailure", "Test failure", "https://example.com")>]
let analyzer: Analyzer<CliContext> = Axial.Guardrails.Execution.track "InjectedFailure" (fun _ -> async { return failwith ("injected rule failure" + string (char 1)) })
[<CliAnalyzer("InjectedFinding", "Test finding", "https://example.com")>]
let finding: Analyzer<CliContext> = Axial.Guardrails.Execution.track "InjectedFinding" (fun _ -> async {
    return [{ Type = "test"; Code = "TEST001"; Severity = Severity.Warning
              Message = "retained finding" + string (char 1) + " emoji: 😀"
              Range = FSharp.Compiler.Text.Range.range0; Fixes = [] }] })
            """)
            if recordFailure then
                let file = Path.Combine(plugin, "Plugin.fs")
                let text = File.ReadAllText file
                File.WriteAllText(file, text.Replace("return failwith (\"injected rule failure\" + string (char 1))", "let directory = System.Environment.GetEnvironmentVariable \"AXIAL_GUARDRAILS_RUN\" in System.IO.Directory.Delete(directory, true); System.IO.File.WriteAllText(directory, \"blocked\"); return []"))
            let compiled = runBuild (Path.Combine(plugin, "Plugin.fsproj")) None
            Assert.True(compiled.ExitCode = 0, compiled.Output)
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework><TreatWarningsAsErrors>true</TreatWarningsAsErrors>" "module Consumer\nlet value = 1\n"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let xml = System.Xml.Linq.XDocument.Load project
            let path = xml.Descendants(System.Xml.Linq.XName.Get "_AxialGuardrailsAnalyzersPath") |> Seq.exactlyOne
            path.Value <- Path.Combine(plugin, "bin", configuration (), "net10.0")
            xml.Save project
            let result = runBuild project None
            Assert.True(result.ExitCode <> 0, result.Output)
            Assert.True(result.Output.Contains("error AXGINF001"), result.Output)
            Assert.DoesNotContain("warning AXGINF001", result.Output)
            if recordFailure then
                Assert.Contains("InjectedFailure could not record", result.Output)
                Assert.Contains("Program.fs", result.Output)
                Assert.Empty(Directory.GetFiles(dir, "axial-guardrails.net10.0.stamp", SearchOption.AllDirectories))
            else
                Assert.Contains("retained finding", result.Output)
                Assert.Contains("InjectedFailure skipped", result.Output)
                Assert.Contains("Program.fs", result.Output)
                Assert.Contains("injected rule failure", result.Output)
                Assert.Empty(Directory.GetFiles(dir, "axial-guardrails.net10.0.stamp", SearchOption.AllDirectories))
                let report = Directory.GetFiles(dir, "axial-guardrails.*.result.xml", SearchOption.AllDirectories) |> Array.exactlyOne
                let text = File.ReadAllText report
                Assert.Contains("complete=\"false\"", text)
                Assert.Contains("retained finding", text)
                Assert.Contains("😀", text)
                Assert.Contains("injected rule failure", text)
                Assert.Contains("rule=\"EffectBoundary\"", text)
                Assert.Contains("status=\"ran\"", text)
        finally Directory.Delete(dir, true)

    [<Fact>]
    let ``slice1 process crash fails closed once under warnings as errors without stamping`` () =
        if not (OperatingSystem.IsWindows()) then
            let dir = Path.Combine(Path.GetTempPath(), "axial-slice1-crash-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory dir |> ignore
            try
                writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework><TreatWarningsAsErrors>true</TreatWarningsAsErrors>" "module Consumer\nlet value = 1\n"
                let project = Path.Combine(dir, "Consumer.fsproj")
                let xml = System.Xml.Linq.XDocument.Load project
                let command = xml.Descendants(System.Xml.Linq.XName.Get "_AxialGuardrailsExe") |> Seq.exactlyOne
                command.Value <- "sh -c 'exit 17' --"
                xml.Save project
                let result = runBuildWithArgs [ "-warnaserror" ] project None
                Assert.True(result.ExitCode <> 0, result.Output)
                Assert.Contains("error AXGINF001", result.Output)
                let diagnostics = result.Output.Split('\n') |> Array.takeWhile (fun line -> not (line.StartsWith("Build "))) |> Array.filter (fun line -> line.Contains("AXGINF001"))
                Assert.True(diagnostics.Length = 1, result.Output)
                Assert.Contains("exit code 17", result.Output)
                Assert.Empty(Directory.GetFiles(dir, "axial-guardrails.net10.0.stamp", SearchOption.AllDirectories))
                let report = Directory.GetFiles(dir, "axial-guardrails.*.result.xml", SearchOption.AllDirectories) |> Array.exactlyOne
                Assert.Contains("complete=\"false\"", File.ReadAllText report)
            finally Directory.Delete(dir, true)

    [<Fact>]
    let ``slice1 signature files reach complete coverage and reuse the cache`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-signature-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet value = 1\n"
            let project = Path.Combine(dir, "Consumer.fsproj")
            File.WriteAllText(Path.Combine(dir, "Program.fsi"), "module Consumer\nval value: int\n")
            File.WriteAllText(project, (File.ReadAllText project).Replace("<Compile Include=\"Program.fs\" />", "<Compile Include=\"Program.fsi\" /><Compile Include=\"Program.fs\" />"))
            let first = runBuild project None
            Assert.True(first.ExitCode = 0, first.Output)
            let report = Directory.GetFiles(dir, "*.result.xml", SearchOption.AllDirectories) |> Array.exactlyOne
            Assert.Contains("complete=\"true\"", File.ReadAllText report)
            let second = runBuild project None
            Assert.True(second.ExitCode = 0, second.Output)
            Assert.DoesNotContain("Running Axial.Guardrails analysis", second.Output)
        finally Directory.Delete(dir, true)

    [<Fact>]
    let ``slice1 cached error findings still fail the build`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-error-cache-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            writeV2Policy dir "domain" "deny"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let first = runBuild project None
            Assert.True(first.ExitCode <> 0, first.Output)
            Assert.Contains("error AXG001", first.Output)
            let second = runBuild project None
            Assert.DoesNotContain("Running Axial.Guardrails analysis", second.Output)
            Assert.True(second.ExitCode <> 0, second.Output)
            Assert.Contains("error AXG001", second.Output)
        finally Directory.Delete(dir, true)

    [<Fact>]
    let ``slice1 every CLI entry point is tracked and included in the coverage contract`` () =
        let root = Path.Combine(repoRoot (), "src", "Axial.Guardrails")
        let matches pattern text =
            System.Text.RegularExpressions.Regex.Matches(text, pattern)
            |> Seq.cast<System.Text.RegularExpressions.Match>
            |> Seq.map (fun m -> m.Groups[1].Value)
            |> Seq.toList
        let mutable entries = []
        let mutable tracked = []
        for file in Directory.GetFiles(root, "*.fs") do
            let text = File.ReadAllText file
            entries <- entries @ matches "CliAnalyzer\\(\"([^\"]+)\"" text
            tracked <- tracked @ matches "Execution.track \"([^\"]+)\"" text
            // Check each entry's body, not merely the aggregate set.
            for block in System.Text.RegularExpressions.Regex.Split(text, "\\[<CliAnalyzer") |> Array.skip 1 do
                let name = matches "^\\(\"([^\"]+)\"" block |> List.exactlyOne
                Assert.Contains($"Execution.track \"{name}\"", block)
        let xml = System.Xml.Linq.XDocument.Load(Path.Combine(root, "build", "Axial.Guardrails.targets"))
        let rules = (xml.Descendants(System.Xml.Linq.XName.Get "_AxialGuardrailsRules") |> Seq.exactlyOne).Value.Split(';') |> Array.toList
        Assert.NotEmpty entries
        Assert.Equal<string list>(List.sort entries, List.sort tracked)
        Assert.Equal<string list>(List.sort entries, List.sort rules)

    [<Fact>]
    let ``slice1 Windows shell arguments preserve nested command quotes`` () =
        let assembly = System.Reflection.Assembly.LoadFrom(Path.Combine(repoRoot (), "artifacts", "bin", "Axial.Guardrails.Build", configuration (), "Axial.Guardrails.Build.dll"))
        let task = assembly.GetType("AxialGuardrailsTask")
        let factory = task.GetMethod("CreateStartInfo")
        Assert.NotNull factory
        let command = "dotnet \"C:\\Program Files\\tool.dll\" --project \"C:\\my & app\\App.fsproj\""
        let start = factory.Invoke(null, [| box command; box true |]) :?> ProcessStartInfo
        Assert.Equal("cmd.exe", start.FileName)
        Assert.Equal("/d /s /c \"" + command + "\"", start.Arguments)
        Assert.Empty start.ArgumentList

    [<Fact>]
    let ``fix2 Unix shell overrides retain expansion quoting and backslashes`` () =
        if not (OperatingSystem.IsWindows()) then
            let assembly = System.Reflection.Assembly.LoadFrom(Path.Combine(repoRoot (), "artifacts", "bin", "Axial.Guardrails.Build", configuration (), "Axial.Guardrails.Build.dll"))
            let factory = assembly.GetType("AxialGuardrailsTask").GetMethod("CreateStartInfo")
            let command = "printf '%s|%s|%s' \"$AXIAL_QUOTE_TEST\" \"$(printf substitution)\" 'a\\b'; printf '|tail'"
            let start = factory.Invoke(null, [| box command; box false |]) :?> ProcessStartInfo
            start.EnvironmentVariables["AXIAL_QUOTE_TEST"] <- "expanded value"
            use child = Process.Start start
            let output = child.StandardOutput.ReadToEnd()
            child.WaitForExit()
            Assert.Equal(0, child.ExitCode)
            Assert.Equal("expanded value|substitution|a\\b|tail", output)

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``fix2 cleanup failures are diagnostic only`` unauthorized =
        let assembly = System.Reflection.Assembly.LoadFrom(Path.Combine(repoRoot (), "artifacts", "bin", "Axial.Guardrails.Build", configuration (), "Axial.Guardrails.Build.dll"))
        let cleanup = assembly.GetType("AxialGuardrailsTask").GetMethod("CleanupRunDirectory", System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.NonPublic)
        Assert.NotNull cleanup
        let messages = ResizeArray<string>()
        let remove = Action<string>(fun _ -> if unauthorized then raise (UnauthorizedAccessException("denied")) else raise (IOException("locked")))
        cleanup.Invoke(null, [| box "run"; box remove; box (Action<string>(messages.Add)) |]) |> ignore
        Assert.Single messages |> ignore
        Assert.Contains((if unauthorized then "denied" else "locked"), messages[0])

    [<Fact>]
    let ``fix2 record failure cause reaches the single infrastructure error`` () =
        if not (OperatingSystem.IsWindows()) then
            let dir = Path.Combine(Path.GetTempPath(), "axial-record-failure-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory dir |> ignore
            try
                writeConsumer dir
                let project = Path.Combine(dir, "Consumer.fsproj")
                let xml = System.Xml.Linq.XDocument.Load project
                (xml.Descendants(System.Xml.Linq.XName.Get "_AxialGuardrailsExe") |> Seq.exactlyOne).Value <- "printf '%s\\n' 'Axial.Guardrails infrastructure: InjectedRule could not record Program.fs: injected recording failure'"
                xml.Save project
                let result = runBuild project None
                Assert.NotEqual(0, result.ExitCode)
                Assert.Contains("injected recording failure", result.Output)
                Assert.DoesNotContain("warning AXGINF001", result.Output)
            finally Directory.Delete(dir, true)

    [<Theory>]
    [<InlineData("AxialGuardrailsCheckPolicy")>]
    let ``tool launch overrides inherited runtime policy only for its child`` (targetName: string) =
        let xml = System.Xml.Linq.XDocument.Load(Path.Combine(repoRoot (), "src", "Axial.Guardrails", "build", "Axial.Guardrails.targets"))
        let target =
            xml.Descendants(System.Xml.Linq.XName.Get "Target")
            |> Seq.find (fun node -> node.Attribute(System.Xml.Linq.XName.Get "Name").Value = targetName)
        let launch = target.Descendants(System.Xml.Linq.XName.Get "Exec") |> Seq.exactlyOne
        let environment = launch.Attribute(System.Xml.Linq.XName.Get "EnvironmentVariables")
        Assert.True(environment <> null, $"{targetName} must override inherited Major/Disable runtime policy for its child")
        Assert.Contains("DOTNET_ROLL_FORWARD=LatestMajor", environment.Value.Split(';'))

    [<Fact>]
    let ``analyzer child receives LatestMajor despite inherited Major`` () =
        if not (OperatingSystem.IsWindows()) then
            let dir = Path.Combine(Path.GetTempPath(), "axial-runtime-policy-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory dir |> ignore
            try
                writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet value = 1\n"
                let script = Path.Combine(dir, "runtime.sh")
                File.WriteAllText(script, "printf '%s' \"$DOTNET_ROLL_FORWARD\" > runtime.txt\n")
                let project = Path.Combine(dir, "Consumer.fsproj")
                let xml = System.Xml.Linq.XDocument.Load project
                let command = xml.Descendants(System.Xml.Linq.XName.Get "_AxialGuardrailsExe") |> Seq.exactlyOne
                command.Value <- $"sh \"{script}\""
                xml.Save project
                let result = runBuildWithEnvironment [ "DOTNET_ROLL_FORWARD", "Major" ] [] project None
                Assert.True(result.ExitCode <> 0, result.Output)
                Assert.Contains("error AXGINF001", result.Output)
                Assert.Equal("LatestMajor", File.ReadAllText(Path.Combine(dir, "runtime.txt")))
            finally Directory.Delete(dir, true)

    [<Fact>]
    let ``measurement project explicitly disables guardrails and consumer emits no AXG diagnostics`` () =
        let project = System.Xml.Linq.XDocument.Load(Path.Combine(repoRoot (), "benchmarks", "Axial.Benchmarks", "Axial.Benchmarks.fsproj"))
        let setting = project.Descendants(System.Xml.Linq.XName.Get "AxialGuardrailsEnabled") |> Seq.tryHead
        Assert.True(setting.IsSome, "Measurement projects must explicitly disable guardrails")
        let value = setting.Value.Value
        Assert.Equal("false", value)
        let result =
            buildConsumerInTemp (fun dir ->
                writeConsumerProject dir
                    ($"<TargetFramework>net10.0</TargetFramework><AxialGuardrailsEnabled>{value}</AxialGuardrailsEnabled>")
                    "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\nlet run () = System.Threading.Tasks.Task.Delay(1).Wait()\n")
        Assert.True(result.ExitCode = 0, result.Output)
        Assert.DoesNotContain("AXG", result.Output)

    [<Fact>]
    let ``guide checkpoint switches invalidate cached analysis and retain noted Info`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guide-checkpoint-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            // No policy file: this fixture tests the package's checkpoint, not policy approval.
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet f () = try failwith \"bad\" with _ -> ()\n"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let normal = runBuild project None
            Assert.True(normal.ExitCode = 0, normal.Output)
            Assert.Contains("warning AXG010", normal.Output)
            let checkpoint = runBuildWithArgs [ "-p:AxialGuardrailsCheckpoint=true" ] project None
            Assert.NotEqual(0, checkpoint.ExitCode)
            Assert.Contains("error AXG010", checkpoint.Output)
            File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\nlet f () = try failwith \"bad\" with _ -> () // axial-note AXG010: best effort observer\n")
            let noted = runBuildWithArgs [ "-p:AxialGuardrailsCheckpoint=true" ] project None
            Assert.True(noted.ExitCode = 0, noted.Output)
            Assert.Contains("info axg010", noted.Output.ToLowerInvariant())
            Assert.Contains("best effort observer", noted.Output)
            Assert.DoesNotContain("AXG002", noted.Output)
            let repeated = runBuildWithArgs [ "-p:AxialGuardrailsCheckpoint=true" ] project None
            Assert.True(repeated.ExitCode = 0, repeated.Output)
            Assert.Contains("info axg010", repeated.Output.ToLowerInvariant())
            Assert.Contains("best effort observer", repeated.Output)
            File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\n// axial-note AXG010: stale\nlet value = 1\n")
            let invalid = runBuild project None
            Assert.True(invalid.ExitCode = 0, invalid.Output)
            Assert.Contains("warning AXG002", invalid.Output)
            let invalidCheckpoint = runBuildWithArgs [ "-p:AxialGuardrailsCheckpoint=true" ] project None
            Assert.NotEqual(0, invalidCheckpoint.ExitCode)
            Assert.Contains("error AXG002", invalidCheckpoint.Output)
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``test project defaults run note integrity and ignore lock`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guide-test-defaults-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject>" "module Consumer\nlet x = lock (obj()) (fun () -> 1)\n"
            writeV2Policy dir "test" "deny"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let checkpoint = runBuildWithArgs [ "-p:AxialGuardrailsCheckpoint=true" ] project None
            Assert.True(checkpoint.ExitCode = 0, checkpoint.Output)
            Assert.DoesNotContain("AXG009", checkpoint.Output)
            File.AppendAllText(Path.Combine(dir, "Program.fs"), "// axial-note AXG010: stale\n")
            let invalid = runBuild project None
            Assert.True(invalid.ExitCode = 0, invalid.Output)
            Assert.Contains("warning AXG002", invalid.Output)
            let rejected = runBuildWithArgs [ "-p:AxialGuardrailsCheckpoint=true" ] project None
            Assert.NotEqual(0, rejected.ExitCode)
            Assert.Contains("error AXG002", rejected.Output)
        finally
            Directory.Delete(dir, true)


    let private withConsumerProject source build =
        let dir = Path.Combine(Path.GetTempPath(), "axial-severity-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" source
            build dir (Path.Combine(dir, "Consumer.fsproj"))
        finally Directory.Delete(dir, true)

    [<Fact>]
    let ``policy baseline prepares current rule candidates without approval and is repeatable`` () =
        withConsumerProject "module Consumer\nlet now () = (System.DateTimeOffset.UtcNow, System.DateTimeOffset.UtcNow)\n" (fun dir project ->
            writeV2Policy dir "domain" "deny"
            let baseline = Path.Combine(dir, "Axial.Guardrails.baseline")
            let preserved = "AXG003\tOld.fs\t" + String('a', 64) + "\n"
            File.WriteAllText(baseline, preserved)
            writeFixtureLock dir
            let lockPath = Path.Combine(dir, "Axial.Guardrails.lock")
            let locked = File.ReadAllBytes lockPath
            let toml = File.ReadAllBytes(Path.Combine(dir, "Axial.Guardrails.toml"))
            runPolicyTool dir ["policy"; "baseline"; "--rule"; "effects.ambient"; "--json"]
            let proposed = File.ReadAllText baseline
            Assert.Contains(preserved.Trim(), proposed)
            let entries = File.ReadAllLines baseline |> Array.filter (fun line -> line.StartsWith("AXG001\t"))
            Assert.Single entries |> ignore
            Assert.Matches("^AXG001\\tProgram.fs\\t[0-9a-f]{64} x2$", entries[0])
            Assert.Equal<byte>(locked, File.ReadAllBytes lockPath)
            Assert.Equal<byte>(toml, File.ReadAllBytes(Path.Combine(dir, "Axial.Guardrails.toml")))
            Assert.DoesNotContain("REVIEWED_POINT", File.ReadAllText lockPath)
            runPolicyTool dir ["policy"; "baseline"; "--rule"; "effects.ambient"]
            Assert.Equal(proposed, File.ReadAllText baseline)
            Assert.Equal<byte>(locked, File.ReadAllBytes lockPath)
            // Candidates are consumed by the real analyzer, not just syntactically plausible rows.
            let built = runBuild project None
            Assert.True(built.ExitCode = 0, built.Output))

    [<Fact>]
    let ``policy baseline preserves analyzer crash diagnostics from quiet builds`` () =
        if not (OperatingSystem.IsWindows()) then
            withConsumerProject "module Consumer\nlet value = 1\n" (fun dir project ->
                writeV2Policy dir "domain" "deny"
                let script = Path.Combine(dir, "crash.sh")
                let diagnostic = "Unhandled exception: synthetic analyzer startup failure"
                File.WriteAllText(script, $"printf '%%s\\n' '{diagnostic}'\nexit 22\n")
                let xml = System.Xml.Linq.XDocument.Load project
                let command = xml.Descendants(System.Xml.Linq.XName.Get "_AxialGuardrailsExe") |> Seq.exactlyOne
                command.Value <- $"sh \"{script}\""
                xml.Save project
                let failure = Record.Exception(fun () -> runPolicyTool dir ["policy"; "baseline"; "--rule"; "effects.ambient"])
                Assert.NotNull failure
                Assert.Contains("exit code 22", failure.Message)
                Assert.Contains(diagnostic, failure.Message))

    [<Fact>]
    let ``policy baseline uses repository paths for nested baselines and never multiplies framework counts`` () =
        withConsumerProject "module Consumer\nlet now () = (System.DateTimeOffset.UtcNow, System.DateTimeOffset.UtcNow)\n" (fun dir project ->
            writeConsumerProject dir "<TargetFrameworks>net8.0;net10.0</TargetFrameworks>" "module Consumer\nlet now () = (System.DateTimeOffset.UtcNow, System.DateTimeOffset.UtcNow)\n"
            Directory.CreateDirectory(Path.Combine(dir, "policy")) |> ignore
            File.WriteAllText(Path.Combine(dir, "Axial.Guardrails.toml"), "version = 2\nbaseline = \"policy/adoption.baseline\"\n[default]\nrole = \"domain\"\n[roles.domain.rules]\n\"effects.ambient\" = \"deny\"\n")
            File.WriteAllText(Path.Combine(dir, "policy/adoption.baseline"), "")
            writeFixtureLock dir
            let locked = File.ReadAllBytes(Path.Combine(dir, "Axial.Guardrails.lock"))
            runPolicyTool dir ["policy"; "baseline"; "--rule"; "effects.ambient"]
            let entries = File.ReadAllLines(Path.Combine(dir, "policy/adoption.baseline")) |> Array.filter (fun line -> not (line.StartsWith "#"))
            Assert.Single entries |> ignore
            Assert.Matches("^AXG001\\tProgram.fs\\t[0-9a-f]{64} x2$", entries[0])
            Assert.Equal<byte>(locked, File.ReadAllBytes(Path.Combine(dir, "Axial.Guardrails.lock")))
            let built = runBuild project None
            Assert.True(built.ExitCode = 0, built.Output))

    let private countSeverityError (text: string) =
        text.Split('\n')
        |> Array.map (fun line -> line.Trim())
        |> Array.filter (fun line -> line.Contains("error AXIALGR001"))
        |> Array.distinct
        |> Array.length

    [<Fact>]
    let ``legacy severity property fails every build with an actionable replacement`` () =
        withConsumerProject "module Consumer\nlet value = 1\n" (fun _ project ->
            let warning = runBuildWithArgs [ "-p:AxialGuardrailsSeverity=warning" ] project None
            Assert.NotEqual(0, warning.ExitCode)
            Assert.Equal(1, countSeverityError warning.Output)
            Assert.Contains("policy-roles", warning.Output)
            Assert.Contains("AxialGuardrailsSeverity", warning.Output)
            Assert.Contains("Axial.Guardrails.toml", warning.Output)
            Assert.Contains("AXG009", warning.Output)
            let repeated = runBuildWithArgs [ "-p:AxialGuardrailsSeverity=warning" ] project None
            Assert.NotEqual(0, repeated.ExitCode)
            Assert.Equal(1, countSeverityError repeated.Output)
            let unset = runBuildWithArgs [] project None
            Assert.True(unset.ExitCode = 0, unset.Output)
            Assert.Equal(0, countSeverityError unset.Output))

    [<Fact>]
    let ``legacy severity environment spellings fail and disabled analysis does not hide them`` () =
        withConsumerProject "module Consumer\nlet value = 1\n" (fun _ project ->
            let propertyStyle = runBuildWithEnvironment [ "AxialGuardrailsSeverity", "warning" ] [] project None
            Assert.NotEqual(0, propertyStyle.ExitCode)
            Assert.Equal(1, countSeverityError propertyStyle.Output)
            let underscore = runBuildWithEnvironment [ "AXIAL_GUARDRAILS_SEVERITY", "warning" ] [] project None
            Assert.NotEqual(0, underscore.ExitCode)
            Assert.Equal(1, countSeverityError underscore.Output)
            let disabled = runBuildWithEnvironment [ "AXIAL_GUARDRAILS_SEVERITY", "warning" ] [ "-p:AxialGuardrailsEnabled=false" ] project None
            Assert.NotEqual(0, disabled.ExitCode)
            Assert.Equal(1, countSeverityError disabled.Output))

    [<Theory>]
    [<InlineData("AXIAL_GUARDRAILS_SEVERITY")>]
    [<InlineData("AxialGuardrailsSeverity")>]
    let ``standalone analyzer cli fails for removed severity settings`` (setting: string) =
        withConsumerProject "module Consumer\nlet value = 1\n" (fun dir project ->
            let built = runBuild project None
            Assert.True(built.ExitCode = 0, built.Output)
            let info = ProcessStartInfo(dotnetHost ())
            info.WorkingDirectory <- dir
            info.UseShellExecute <- false
            info.RedirectStandardOutput <- true
            info.RedirectStandardError <- true
            info.Environment[setting] <- "warning"
            info.Environment[Axial.Guardrails.GuardrailConfig.EnvironmentVariable] <- ""
            for arg in ["--roll-forward"; "LatestMajor"; cliDll (); "--project"; project; "--analyzers-path"; analyzersPath (); "--include-analyzers"; "EffectBoundary"] do
                info.ArgumentList.Add arg
            use child = Process.Start info
            let output = child.StandardOutput.ReadToEndAsync()
            let errors = child.StandardError.ReadToEndAsync()
            let exited = child.WaitForExit(90_000)
            if not exited then
                child.Kill(true)
                child.WaitForExit()
            Assert.True(exited, "Standalone analyzer timed out.")
            let text = output.Result + errors.Result
            Assert.NotEqual(0, child.ExitCode)
            Assert.Contains("error AXIALGR001", text)
            Assert.Contains("policy-roles", text))

    [<Fact>]
    let ``legacy severity warning never downgrades a block finding`` () =
        withConsumerProject "module Consumer\nopen System.Threading.Tasks\nlet value = TaskCompletionSource<int>()\n" (fun _ project ->
            let result = runBuildWithArgs [ "-p:AxialGuardrailsSeverity=warning"; "-p:AxialGuardrailsInTests=all" ] project None
            Assert.NotEqual(0, result.ExitCode)
            Assert.Contains("AXIALGR001", result.Output)
            Assert.Contains("AXG009", result.Output))

    [<Fact>]
    let ``package build imports expose only v2 policy controls`` () =
        let buildDir = Path.Combine(repoRoot (), "src", "Axial.Guardrails", "build")
        let imports =
            [ "Axial.Guardrails.props"; "Axial.Guardrails.targets" ]
            |> List.map (fun file -> File.ReadAllText(Path.Combine(buildDir, file)))
            |> String.concat "\n"
        for removed in
            [ "AxialGuardrailsUseLegacyPolicy"
              "AxialGuardrailsGenerateBaseline"
              "AxialGuardrailsBoundary" ] do
            Assert.DoesNotContain(removed, imports)
        Assert.Contains("AXIALGR001", imports)

    [<Fact>]
    let ``no-op builds never launch a policy tool to select analyzers`` () =
        let targets = File.ReadAllText(Path.Combine(repoRoot (), "src", "Axial.Guardrails", "build", "Axial.Guardrails.targets"))
        Assert.DoesNotContain("project-role", targets)
        Assert.DoesNotContain("--include-analyzers", targets)
        let document = System.Xml.Linq.XDocument.Parse targets
        let configTarget = document.Descendants() |> Seq.find (fun node -> node.Name.LocalName = "Target" && (node.Attribute(System.Xml.Linq.XName.Get "Name")).Value = "AxialGuardrailsWriteConfig")
        Assert.DoesNotContain(configTarget.Descendants(), fun node -> node.Name.LocalName = "Exec")

    [<Fact>]
    let ``v2 roles and levels remain enforced while lock drift only warns`` () =
        let build role level mutateAfterLock =
            buildConsumerInTemp
                (fun dir ->
                    writeConsumer dir
                    writeV2Policy dir role level
                    if mutateAfterLock then File.AppendAllText(Path.Combine(dir, "Axial.Guardrails.toml"), "# drift\n"))


        let capability = build "capabilities" "deny" false
        Assert.True(capability.ExitCode = 0, capability.Output)
        Assert.DoesNotContain("AXG001", capability.Output)

        let warning = build "domain" "warn" false
        Assert.True(warning.ExitCode = 0, warning.Output)
        Assert.Contains("warning AXG001", warning.Output)

        let drift = build "domain" "deny" true
        Assert.NotEqual(0, drift.ExitCode)
        Assert.Contains("Policy changed since the last review:", drift.Output)
        Assert.Contains("error AXG001", drift.Output)
        let openDrift = build "capabilities" "deny" true
        Assert.True(openDrift.ExitCode = 0, openDrift.Output)
        Assert.Contains("axial policy review", openDrift.Output)

    [<Theory>]
    [<InlineData("test", false, false, false)>]
    [<InlineData("domain", true, false, true)>]
    [<InlineData("test", false, true, true)>]
    let ``declared project role chooses test analyzer set independently of test SDK`` role isTest all expectsAmbient =
        let result =
            buildConsumerInTemp (fun dir ->
                writeConsumerProject dir
                    ($"<TargetFramework>net10.0</TargetFramework><IsTestProject>{isTest.ToString().ToLowerInvariant()}</IsTestProject>" +
                     (if all then "<AxialGuardrailsInTests>all</AxialGuardrailsInTests>" else ""))
                    "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n"
                writeV2Policy dir "domain" "deny"
                File.AppendAllText(Path.Combine(dir, "Axial.Guardrails.toml"), $"\n[projects]\n{role} = [\"Consumer.fsproj\"]\n")
                writeFixtureLock dir)
        Assert.True((expectsAmbient = (result.ExitCode <> 0)), result.Output)
        if expectsAmbient then Assert.Contains("AXG001", result.Output)
        else Assert.DoesNotContain("AXG001", result.Output)

    [<Fact>]
    let ``changing InTests invalidates cached analyzer selection in both directions`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-selection-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n"
            writeV2Policy dir "test" "deny"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let first = runBuild project None
            Assert.True(first.ExitCode = 0, first.Output)
            let all = runBuildWithArgs [ "-p:AxialGuardrailsInTests=all" ] project None
            Assert.NotEqual(0, all.ExitCode)
            Assert.Contains("AXG001", all.Output)
            let reduced = runBuild project None
            Assert.True(reduced.ExitCode = 0, reduced.Output)
            Assert.Contains("Running Axial.Guardrails analysis", reduced.Output)
            Assert.DoesNotContain("AXG001", reduced.Output)
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``default test builds report guessed waits without enabling ambient effect rules`` () =
        let build source =
            buildConsumerInTemp (fun dir ->
                writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject>" source
                writeV2Policy dir "test" "deny")

        let ambient = build "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n"
        Assert.True(ambient.ExitCode = 0, ambient.Output)
        Assert.DoesNotContain("AXG001", ambient.Output)

        let guessedWait =
            build "module Consumer\nlet wait () = task { do! System.Threading.Tasks.Task.Delay 10 }\n"
        Assert.True(guessedWait.ExitCode = 0, guessedWait.Output)
        Assert.Contains("warning AXG013", guessedWait.Output)

        let checkpoint =
            buildConsumerInTemp (fun dir ->
                writeConsumerProject dir
                    "<TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject><AxialGuardrailsCheckpoint>true</AxialGuardrailsCheckpoint>"
                    "module Consumer\nlet wait () = task { do! System.Threading.Tasks.Task.Delay 10 }\n"
                writeV2Policy dir "test" "deny")
        Assert.True(checkpoint.ExitCode <> 0, checkpoint.Output)
        Assert.Contains("error AXG013", checkpoint.Output)

    [<Fact>]
    let ``missing analyzer folder fails instead of silently skipping`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-consumer-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            let project = Path.Combine(dir, "Consumer.fsproj")
            let xml = File.ReadAllText project
            let missing = Path.Combine(dir, "missing-analyzers")
            let bootstrapProject = Path.Combine(repoRoot (), "src", "Axial.Guardrails", "Axial.Guardrails.fsproj") |> xmlEscape
            let bootstrapElement = $"<_AxialGuardrailsBootstrapProject>{bootstrapProject}</_AxialGuardrailsBootstrapProject>"
            File.WriteAllText(project, xml.Replace(analyzersPath (), missing).Replace(bootstrapElement, "<_AxialGuardrailsBootstrapProject />"))
            let result = runBuild project None
            Assert.NotEqual(0, result.ExitCode)
            Assert.Contains("analyzer folder is missing", result.Output)
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``analyzes code selected by every target framework`` () =
        let write dir =
            writeConsumerProject
                dir
                "<TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>"
                "module Consumer\ntype Conditional = Value of int\n#if NET10_0\nlet render value = sprintf \"%A\" value\n#endif\n"

        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-consumer-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            write dir
            let project = Path.Combine(dir, "Consumer.fsproj")
            let netstandard = runBuild project (Some "netstandard2.1")
            let net10 = runBuild project (Some "net10.0")

            Assert.Equal(0, netstandard.ExitCode)
            Assert.DoesNotContain("AXG006", netstandard.Output)
            Assert.True(net10.ExitCode <> 0, net10.Output)
            Assert.Contains("error AXG006", net10.Output)
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``a designated target framework is the only analyzed target`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-designated-tfm-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumerProject
                dir
                "<TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks><_AxialGuardrailsDesignatedTargetFramework>net10.0</_AxialGuardrailsDesignatedTargetFramework>"
                "module Consumer\nlet value = System.DateTimeOffset.UtcNow\n"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let netstandard = runBuild project (Some "netstandard2.1")
            Assert.Equal(0, netstandard.ExitCode)
            Assert.DoesNotContain("Running Axial.Guardrails analysis", netstandard.Output)

            let net10 = runBuild project (Some "net10.0")
            Assert.NotEqual(0, net10.ExitCode)
            Assert.Contains("error AXG001", net10.Output)
            Assert.Contains("Running Axial.Guardrails analysis", net10.Output)
        finally
            Directory.Delete(dir, true)

    let private assertAnalysisRan expected (result: BuildResult) =
        Assert.Equal(0, result.ExitCode)
        if expected then Assert.Contains("Running Axial.Guardrails analysis", result.Output)
        else Assert.DoesNotContain("Running Axial.Guardrails analysis", result.Output)

    [<Fact>]
    let ``a corrupt policy cache cannot hide policy drift`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-policy-cache-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            writeV2Policy dir "capabilities" "deny"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let initial = runBuild project None
            Assert.True(initial.ExitCode = 0, initial.Output)

            let stamp = Directory.GetFiles(dir, "axial-guardrails-policy.*.stamp", SearchOption.AllDirectories) |> Array.exactlyOne
            File.WriteAllText(stamp, "corrupt cache")
            File.SetLastWriteTimeUtc(stamp, DateTime.UtcNow.AddDays(1.0))
            File.AppendAllText(Path.Combine(dir, "Axial.Guardrails.toml"), "# drift\n")
            let drift = runBuild project None
            Assert.True(drift.ExitCode = 0, drift.Output)
            Assert.Contains("Policy changed since the last review:", drift.Output)
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``future dated stamp cannot hide policy or analyzer config changes`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-content-cache-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            writeV2Policy dir "domain" "allow"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let build () = runBuild project None
            assertAnalysisRan true (build ())
            let stamp = Directory.GetFiles(dir, "axial-guardrails.net10.0.stamp", SearchOption.AllDirectories) |> Array.exactlyOne
            File.SetLastWriteTimeUtc(stamp, DateTime.UtcNow.AddDays(1.0))

            let policy = Path.Combine(dir, "Axial.Guardrails.toml")
            File.WriteAllText(policy, File.ReadAllText(policy).Replace("\"effects.ambient\" = \"allow\"", "\"effects.ambient\" = \"deny\""))
            writeFixtureLock dir
            let denied = build ()
            Assert.NotEqual(0, denied.ExitCode)
            Assert.Contains("error AXG001", denied.Output)

            File.WriteAllText(policy, File.ReadAllText(policy).Replace("\"effects.ambient\" = \"deny\"", "\"effects.ambient\" = \"allow\""))
            writeFixtureLock dir
            Assert.Equal(0, (build ()).ExitCode)
            File.SetLastWriteTimeUtc(stamp, DateTime.UtcNow.AddDays(1.0))
            File.WriteAllText(Path.Combine(dir, ".editorconfig"), "root = true\n[*.fs]\ndotnet_diagnostic.AXG001.severity = error\n")
            writeFixtureLock dir
            assertAnalysisRan true (build ())
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``incremental builds skip unchanged analysis but never skip a new finding`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-incremental-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet value = 1\n"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let build () = runBuild project None

            assertAnalysisRan true (build ())
            assertAnalysisRan false (build ())

            let stamp = Directory.GetFiles(dir, "axial-guardrails.net10.0.stamp", SearchOption.AllDirectories) |> Array.exactlyOne
            File.WriteAllText(stamp, "corrupt cache")
            File.SetLastWriteTimeUtc(stamp, DateTime.UtcNow.AddDays(1.0))
            File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\nlet value = System.DateTimeOffset.UtcNow\n")
            let finding = build ()
            Assert.NotEqual(0, finding.ExitCode)
            Assert.Contains("error AXG001", finding.Output)
            Assert.Contains("Running Axial.Guardrails analysis", finding.Output)
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``cache keys generated sources compiler properties references and overridden tool inputs`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-cache-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumerProject dir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet value = 1\n"
            let project = Path.Combine(dir, "Consumer.fsproj")
            let reference = Path.Combine(dir, "Dependency.dll")
            File.Copy(cliDll (), reference)
            let toolMarker = Path.Combine(dir, "tool.marker")
            File.WriteAllText(toolMarker, "v1")
            let generated = Path.Combine(dir, "obj", "Generated.fs")
            Directory.CreateDirectory(Path.GetDirectoryName generated) |> ignore
            File.WriteAllText(generated, "module Generated\nlet value = \"one\"\n")
            let xml = File.ReadAllText project
            let additions =
                $"""    <Reference Include="CacheDependency"><HintPath>{xmlEscape reference}</HintPath></Reference>
    <_AxialGuardrailsConfiguredToolInput Include="{xmlEscape toolMarker}" />
    <Compile Include="{xmlEscape generated}" />
  </ItemGroup>
  <ItemGroup>"""
            File.WriteAllText(project, xml.Replace("  </ItemGroup>\n  <Import Project=", additions + "\n  </ItemGroup>\n  <Import Project="))

            let build args = runBuildWithArgs args project None
            assertAnalysisRan true (build [])
            assertAnalysisRan false (build [])
            assertAnalysisRan true (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            let sourceTimestamp = File.GetLastWriteTimeUtc generated
            File.WriteAllText(generated, "module Generated\nlet value = \"two\"\n")
            File.SetLastWriteTimeUtc(generated, sourceTimestamp)
            assertAnalysisRan true (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            assertAnalysisRan false (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])

            File.SetLastWriteTimeUtc(reference, DateTime.UtcNow.AddSeconds(2.0))
            assertAnalysisRan false (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            File.Copy(Path.Combine(analyzersPath (), "Axial.Guardrails.Analyzers.dll"), reference, true)
            assertAnalysisRan true (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            assertAnalysisRan false (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])

            let toolTimestamp = File.GetLastWriteTimeUtc toolMarker
            File.WriteAllText(toolMarker, "v2")
            File.SetLastWriteTimeUtc(toolMarker, toolTimestamp)
            assertAnalysisRan true (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            assertAnalysisRan false (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``project reference content invalidates analysis without volatile timestamp invalidation`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-project-reference-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            let dependencyDir = Path.Combine(dir, "Dependency")
            let consumerDir = Path.Combine(dir, "Consumer")
            Directory.CreateDirectory dependencyDir |> ignore
            Directory.CreateDirectory consumerDir |> ignore
            File.WriteAllText(
                Path.Combine(dependencyDir, "Dependency.fsproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>\n  <ItemGroup><Compile Include=\"Dependency.fs\" /></ItemGroup>\n  <!-- Model project outputs whose build rewrites their timestamp despite identical deterministic bytes. -->\n  <Target Name=\"RewriteOutputTimestamp\" AfterTargets=\"Build\"><Touch Files=\"$(TargetPath)\" /></Target>\n</Project>\n")
            let dependencySource = Path.Combine(dependencyDir, "Dependency.fs")
            File.WriteAllText(dependencySource, "namespace Dependency\nmodule Values =\n    let answer () = 1\n")
            writeConsumerProject consumerDir "<TargetFramework>net10.0</TargetFramework>" "module Consumer\nlet value = Dependency.Values.answer ()\n"
            let consumerProject = Path.Combine(consumerDir, "Consumer.fsproj")
            let xml = File.ReadAllText consumerProject
            let dependencyProject = Path.Combine(dependencyDir, "Dependency.fsproj") |> xmlEscape
            let projectReference =
                $"    <ProjectReference Include=\"{dependencyProject}\" />\n"
            File.WriteAllText(consumerProject, xml.Replace("    <Compile Include=\"Program.fs\" />", "    <Compile Include=\"Program.fs\" />\n" + projectReference))

            let build () = runBuild consumerProject None
            assertAnalysisRan true (build ())
            // Dependency.dll is rewritten by the project-reference build, but identical deterministic bytes keep the cache stable.
            assertAnalysisRan false (build ())

            File.WriteAllText(dependencySource, "namespace Dependency\nmodule Values =\n    let answer () = 1\n    let publicValue = 2\n")
            assertAnalysisRan true (build ())
            assertAnalysisRan false (build ())

            // A body-only edit also changes the implementation assembly hash. Reanalysis is intentionally conservative:
            // inline/member bodies and other implementation metadata can affect F# type checking and analyzer behaviour.
            File.WriteAllText(dependencySource, "namespace Dependency\nmodule Values =\n    let answer () = 2\n    let publicValue = 2\n")
            assertAnalysisRan true (build ())
            assertAnalysisRan false (build ())
        finally
            Directory.Delete(dir, true)
