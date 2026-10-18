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

    let private writeConsumer (dir: string) =
        let fsproj =
            $"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
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
        File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n")

    type BuildResult = { ExitCode: int; Output: string }

    let private runBuild (project: string) (severityEnv: string option) (severityProperty: string option) =
        let startInfo = ProcessStartInfo("dotnet")
        startInfo.ArgumentList.Add "build"
        startInfo.ArgumentList.Add project
        startInfo.ArgumentList.Add "-c"
        startInfo.ArgumentList.Add (configuration ())
        startInfo.ArgumentList.Add "-v"
        startInfo.ArgumentList.Add "minimal"
        startInfo.ArgumentList.Add "--nologo"
        match severityProperty with
        | Some value -> startInfo.ArgumentList.Add("-p:AxialGuardrailsSeverity=" + value)
        | None -> ()
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        match severityEnv with
        | Some value -> startInfo.EnvironmentVariables["AXIAL_GUARDRAILS_SEVERITY"] <- value
        | None -> ()
        use child = Process.Start startInfo
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        Assert.True(child.WaitForExit(120_000), "Consumer build timed out")
        System.Threading.Tasks.Task.WaitAll(stdout, stderr)
        { ExitCode = child.ExitCode; Output = stdout.Result + "\n" + stderr.Result }

    let private buildInTemp severityEnv severityProperty =
        let dir = Path.Combine(Path.GetTempPath(), "axial-guardrails-consumer-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeConsumer dir
            runBuild (Path.Combine(dir, "Consumer.fsproj")) severityEnv severityProperty
        finally
            Directory.Delete(dir, true)

    [<Fact>]
    let ``AXIAL_GUARDRAILS_SEVERITY maps to AxialGuardrailsSeverity in the package props`` () =
        // `error` through the environment must turn the finding into a failing error.
        let errorBuild = buildInTemp (Some "error") None
        Assert.NotEqual(0, errorBuild.ExitCode)
        Assert.Contains("error AXG001", errorBuild.Output)

        // `warning` is the documented migration form: the finding stays a warning and the build succeeds.
        let warningBuild = buildInTemp (Some "warning") None
        Assert.Equal(0, warningBuild.ExitCode)
        Assert.Contains("warning AXG001", warningBuild.Output)
        Assert.DoesNotContain("error AXG001", warningBuild.Output)

        // A -p:AxialGuardrailsSeverity value wins over the environment variable.
        let overridden = buildInTemp (Some "error") (Some "warning")
        Assert.Equal(0, overridden.ExitCode)
        Assert.Contains("warning AXG001", overridden.Output)
        Assert.DoesNotContain("error AXG001", overridden.Output)
