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
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.fs" />
    <AxialGuardrail Include="Axial" />
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

    let private runBuild (project: string) (framework: string option) (severityEnv: string option) (severityProperty: string option) (ci: bool) (generateBaseline: bool) (enabledEnv: string option) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add "build"
        startInfo.ArgumentList.Add project
        startInfo.ArgumentList.Add "-c"
        startInfo.ArgumentList.Add (configuration ())
        startInfo.ArgumentList.Add "-v"
        startInfo.ArgumentList.Add "minimal"
        startInfo.ArgumentList.Add "--nologo"
        startInfo.ArgumentList.Add "-nodeReuse:false"
        startInfo.ArgumentList.Add "-m:1"
        match framework with
        | Some value ->
            startInfo.ArgumentList.Add "-f"
            startInfo.ArgumentList.Add value
        | None -> ()
        match severityProperty with
        | Some value -> startInfo.ArgumentList.Add("-p:AxialGuardrailsSeverity=" + value)
        | None -> ()
        if generateBaseline then startInfo.ArgumentList.Add("-p:AxialGuardrailsGenerateBaseline=true")
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] <- "1"
        match severityEnv with
        | Some value -> startInfo.EnvironmentVariables["AXIAL_GUARDRAILS_SEVERITY"] <- value
        | None -> ()
        if ci then startInfo.EnvironmentVariables["CI"] <- "true"
        enabledEnv |> Option.iter (fun value -> startInfo.EnvironmentVariables["AxialGuardrailsEnabled"] <- value)
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

    let private buildConsumerInTemp write severityEnv severityProperty =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-consumer-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            write dir
            runBuild (Path.Combine(dir, "Consumer.fsproj")) None severityEnv severityProperty false false None
        finally
            Directory.Delete(dir, true)

    let private buildInTemp severityEnv severityProperty =
        buildConsumerInTemp writeConsumer severityEnv severityProperty

    [<Fact>]
    let ``findings are errors and severity settings cannot downgrade them`` () =
        let defaultBuild = buildInTemp None None
        Assert.NotEqual(0, defaultBuild.ExitCode)
        Assert.Contains("error AXG001", defaultBuild.Output)

        let environmentAttempt = buildInTemp (Some "warning") None
        Assert.NotEqual(0, environmentAttempt.ExitCode)
        Assert.Contains("error AXG001", environmentAttempt.Output)

        let propertyAttempt = buildInTemp None (Some "warning")
        Assert.NotEqual(0, propertyAttempt.ExitCode)
        Assert.Contains("error AXG001", propertyAttempt.Output)

    [<Fact>]
    let ``CI rejects an environment severity bypass`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-consumer-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            let project = Path.Combine(dir, "Consumer.fsproj")
            let severity = runBuild project None (Some "warning") None true false None
            Assert.NotEqual(0, severity.ExitCode)
            Assert.Contains("cannot be disabled or downgraded through environment variables", severity.Output)
            let disabled = runBuild project None None None true false (Some "false")
            Assert.NotEqual(0, disabled.ExitCode)
            Assert.Contains("cannot be disabled or downgraded through environment variables", disabled.Output)
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``baseline generation writes a stable candidate and lets the build finish`` () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-consumer-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            let result = runBuild (Path.Combine(dir, "Consumer.fsproj")) None None None false true None
            Assert.Equal(0, result.ExitCode)
            let candidates = Directory.GetFiles(dir, "axial-guardrails.*.baseline", SearchOption.AllDirectories)
            Assert.Single(candidates) |> ignore
            let fields = File.ReadAllText(candidates[0]).Trim().Split('\t')
            Assert.Equal(3, fields.Length)
            Assert.Equal("AXG001", fields[0])
            Assert.Equal("Program.fs", fields[1])
            Assert.Matches("^[0-9a-f]{64}$", fields[2])
        finally
            Directory.Delete(dir, true)

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
            let result = runBuild project None None None false false None
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
            let netstandard = runBuild project (Some "netstandard2.1") None None false false None
            let net10 = runBuild project (Some "net10.0") None None false false None

            Assert.Equal(0, netstandard.ExitCode)
            Assert.DoesNotContain("AXG006", netstandard.Output)
            Assert.True(net10.ExitCode <> 0, net10.Output)
            Assert.Contains("error AXG006", net10.Output)
        finally
            Directory.Delete(dir, true)
