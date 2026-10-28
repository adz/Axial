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
        startInfo.EnvironmentVariables["DOTNET_ROLL_FORWARD"] <- "Major"
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        use child = Process.Start startInfo
        let output = child.StandardOutput.ReadToEnd() + child.StandardError.ReadToEnd()
        child.WaitForExit()
        Assert.True(child.ExitCode = 0, output)

    let private writeV2Policy dir role level =
        runPolicyTool dir [ "init" ]
        File.WriteAllText(
            Path.Combine(dir, "Axial.Guardrails.toml"),
            $"version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n\n[default]\nrole = \"{role}\"\n\n[roles.domain.rules]\n\"effects.ambient\" = \"{level}\"\n")
        runPolicyTool dir [ "policy"; "approve"; "--yes-i-reviewed" ]

    let private xmlEscape (value: string) =
        value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;")

    let private writeConsumerProject (dir: string) (frameworkProperty: string) (source: string) =
        let fsproj =
            $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    {frameworkProperty}
    <OutputType>Library</OutputType>
    <_AxialGuardrailsAnalyzersPath>{xmlEscape (analyzersPath ())}</_AxialGuardrailsAnalyzersPath>
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

    let private runBuildWithArgs (extraArguments: string list) (project: string) (framework: string option) =
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

    [<Fact>]
    let ``package build imports expose only v2 policy controls`` () =
        let buildDir = Path.Combine(repoRoot (), "src", "Axial.Guardrails", "build")
        let imports =
            [ "Axial.Guardrails.props"; "Axial.Guardrails.targets" ]
            |> List.map (fun file -> File.ReadAllText(Path.Combine(buildDir, file)))
            |> String.concat "\n"
        for removed in
            [ "AxialGuardrailsUseLegacyPolicy"
              "AxialGuardrailsSeverity"
              "AxialGuardrailsGenerateBaseline"
              "AxialGuardrailsBoundary" ] do
            Assert.DoesNotContain(removed, imports)

    [<Fact>]
    let ``v2 policy roles levels and lock are enforced before analysis`` () =
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
        Assert.Contains("AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED", drift.Output)
        Assert.Contains("protected files changed:", drift.Output)

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
            Assert.NotEqual(0, drift.ExitCode)
            Assert.Contains("AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED", drift.Output)
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
            runPolicyTool dir [ "policy"; "approve"; "--yes-i-reviewed" ]
            let denied = build ()
            Assert.NotEqual(0, denied.ExitCode)
            Assert.Contains("error AXG001", denied.Output)

            File.WriteAllText(policy, File.ReadAllText(policy).Replace("\"effects.ambient\" = \"deny\"", "\"effects.ambient\" = \"allow\""))
            runPolicyTool dir [ "policy"; "approve"; "--yes-i-reviewed" ]
            Assert.Equal(0, (build ()).ExitCode)
            File.SetLastWriteTimeUtc(stamp, DateTime.UtcNow.AddDays(1.0))
            File.WriteAllText(Path.Combine(dir, ".editorconfig"), "root = true\n[*.fs]\ndotnet_diagnostic.AXG001.severity = error\n")
            runPolicyTool dir [ "policy"; "approve"; "--yes-i-reviewed" ]
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
            File.WriteAllText(generated, "module Generated\nlet value = \"two\"\n")
            assertAnalysisRan true (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            assertAnalysisRan false (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])

            File.SetLastWriteTimeUtc(reference, DateTime.UtcNow.AddSeconds(2.0))
            assertAnalysisRan false (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            File.Copy(Path.Combine(analyzersPath (), "Axial.Guardrails.Analyzers.dll"), reference, true)
            assertAnalysisRan true (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])
            assertAnalysisRan false (build [ "-p:DefineConstants=CACHE_KEY_TEST" ])

            File.WriteAllText(toolMarker, "v2")
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
