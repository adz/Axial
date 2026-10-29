namespace Axial.Guardrails.Tests

open System
open System.IO
open System.Diagnostics
open Axial.Tool.PolicyLock
open Xunit

module PolicyReviewTests =
    let private run root executable (args: string list) =
        let info = ProcessStartInfo(executable)
        info.WorkingDirectory <- root
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        for arg in args do info.ArgumentList.Add arg
        use child = Process.Start info
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        if not (child.WaitForExit(60000)) then
            child.Kill(true)
            failwith "command timed out"
        child.ExitCode, stdout.Result + stderr.Result

    let private repository action =
        let root = Path.Combine(Path.GetTempPath(), "axial-review-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            let write name (text: string) =
                let path = Path.Combine(root, name)
                Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
                File.WriteAllText(path, text)
            let git args =
                let code, text = run root "git" args
                Assert.True((code = 0), text)
                text.Trim()
            git ["init"; "-q"] |> ignore
            git ["config"; "user.email"; "tests@example.invalid"] |> ignore
            git ["config"; "user.name"; "Test"] |> ignore
            write ConfigName "version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n[default]\nrole = \"domain\"\n[roles.domain.rules]\n\"clock.datetime\" = \"deny\"\n"
            write DefaultBaseline ""
            write "App.fs" "module App\n"
            write "App.fsproj" "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><AxialGuardrailsEnabled>true</AxialGuardrailsEnabled><AxialGuardrailsRole>domain</AxialGuardrailsRole><IsTestProject>false</IsTestProject></PropertyGroup><ItemGroup><Compile Include=\"App.fs\"/><AxialGuardrail Include=\"Axial.PlatformService\"/></ItemGroup></Project>"
            let snapshot = inspect root |> function Ok s -> s | Error e -> failwith e
            write LockName (serialize snapshot)
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "initial"] |> ignore
            git ["tag"; "v0.11.3"] |> ignore
            let cli args =
                let runtime = Path.ChangeExtension(typeof<FactAttribute>.Assembly.Location, ".runtimeconfig.json")
                let testRuntime = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".runtimeconfig.json")
                let runtime = if File.Exists testRuntime then testRuntime else runtime
                run root "dotnet" (["exec"; "--runtimeconfig"; runtime; typeof<Snapshot>.Assembly.Location] @ args)
            action root write git cli
        finally Directory.Delete(root, true)

    [<Theory>]
    [<InlineData("// axial-allow-AXG001: foreign boundary")>]
    [<InlineData("// axial-allow-file-AXG001: foreign boundary")>]
    [<InlineData("// axial-note AXG001: reviewed guide")>]
    let ``check warns once and succeeds for policy changes`` directive =
        repository (fun _ write _ cli ->
            write "App.fs" ("module App\n" + directive + "\n")
            let code, output = cli ["check"]
            Assert.Equal(0, code)
            Assert.Contains("1 change", output)
            Assert.Contains("axial policy review", output)
            Assert.Equal(1, output.Split("WARNING").Length - 1))

    [<Theory>]
    [<InlineData("Axial.Guardrails.lock", "corrupt")>]
    [<InlineData("Axial.Guardrails.toml", "broken")>]
    let ``check still rejects malformed policy inputs`` path contents =
        repository (fun _ write _ cli ->
            write path contents
            let code, _ = cli ["check"]
            Assert.NotEqual(0, code))

    [<Fact>]
    let ``review evaluates disabled projects without expanding inactive guardrails globs`` () =
        repository (fun root write _ cli ->
            let project = File.ReadAllText(Path.Combine(root, "App.fsproj"))
            let targets = """<PropertyGroup Condition="'$(AxialGuardrailsEnabled)' == 'true'"><_AxialGuardrailsPolicyGlobRoot>$(MSBuildProjectDirectory)</_AxialGuardrailsPolicyGlobRoot></PropertyGroup><ItemGroup Condition="'$(AxialGuardrailsEnabled)' == 'true'"><Protected Include="$(_AxialGuardrailsPolicyGlobRoot)/**/*.props" Exclude="$(_AxialGuardrailsPolicyGlobRoot)/**/obj/**" /></ItemGroup>"""
            write "App.fsproj" (project.Replace("Enabled>true", "Enabled>false").Replace("</Project>", targets + "</Project>"))
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"]
            Assert.Equal(0, code)
            Assert.DoesNotContain("cannot evaluate", output)
            Assert.Contains("App.fsproj: AxialGuardrailsEnabled: true -> false", output))

    [<Fact>]
    let ``review reports unevaluable projects and continues reviewing other projects`` () =
        repository (fun _ write _ cli ->
            write "Broken.fsproj" "<Project><Import Project=\"missing.targets\" /></Project>"
            write "App.fs" "module App\n// axial-allow-AXG001: foreign boundary\n"
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"]
            Assert.Equal(0, code)
            Assert.Contains("cannot evaluate Broken.fsproj:", output)
            Assert.Contains("missing.targets", output)
            Assert.Contains("foreign boundary", output)
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"; "--check"; "--json"]
            Assert.NotEqual(0, code)
            use document = System.Text.Json.JsonDocument.Parse output
            Assert.Contains("cannot evaluate Broken.fsproj:", document.RootElement.GetProperty("loosened").ToString()))

    [<Fact>]
    let ``first review requires since and explicit tag works`` () =
        repository (fun _ _ _ cli ->
            let code, output = cli ["policy"; "review"]
            Assert.NotEqual(0, code)
            Assert.Contains("--since", output)
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"; "--check"]
            Assert.Equal(0, code)
            Assert.Contains("No policy loosening", output))

    [<Theory>]
    [<InlineData("// axial-allow-AXG001: foreign boundary")>]
    [<InlineData("// axial-allow-file-AXG001: foreign boundary")>]
    [<InlineData("// axial-note AXG001: reviewed guide")>]
    let ``review scans legacy sources and detects added exceptions`` directive =
        repository (fun _ write _ cli ->
            write "App.fs" ("module App\n" + directive + "\n")
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"]
            Assert.Equal(0, code)
            Assert.Contains("App.fs", output)
            Assert.Contains(directive, output)
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"; "--check"; "--json"]
            Assert.NotEqual(0, code)
            use document = System.Text.Json.JsonDocument.Parse output
            Assert.True(document.RootElement.GetProperty("loosened").GetArrayLength() > 0))

    [<Fact>]
    let ``reason changes are reviewed but movement and quoted directives are not`` () =
        repository (fun _ write git cli ->
            write "App.fs" "module App\n// axial-allow-AXG001: original\n"
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "exception"] |> ignore
            write "App.fs" "module App\n\n// axial-allow-AXG001: original\nlet quoted = \"// axial-allow-AXG002: not a directive\"\n"
            let code, output = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.Equal(0, code)
            Assert.DoesNotContain("not a directive", output)
            write "App.fs" "module App\n// axial-allow-AXG001: changed reason\n"
            let code, output = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.NotEqual(0, code)
            Assert.Contains("changed reason", output))

    [<Theory>]
    [<InlineData("role")>]
    [<InlineData("rule")>]
    [<InlineData("runtime")>]
    [<InlineData("boundary")>]
    [<InlineData("ignore")>]
    [<InlineData("baseline")>]
    [<InlineData("enabled")>]
    [<InlineData("project-role")>]
    [<InlineData("guardrail")>]
    [<InlineData("test-project")>]
    [<InlineData("verify")>]
    [<InlineData("workflow")>]
    [<InlineData("package")>]
    let ``review detects policy and evaluated project loosening`` kind =
        repository (fun root write _ cli ->
            let config = File.ReadAllText(Path.Combine(root, ConfigName))
            let project = File.ReadAllText(Path.Combine(root, "App.fsproj"))
            match kind with
            | "role" -> write ConfigName (config.Replace("role = \"domain\"", "role = \"host\""))
            | "rule" -> write ConfigName (config.Replace("= \"deny\"", "= \"allow\""))
            | "runtime" -> write ConfigName (config + "\n[runtime]\nfiles = [\"App.fs\"]\n")
            | "boundary" -> write ConfigName (config + "\n[boundaries]\nfiles = [\"App.fs\"]\n")
            | "ignore" -> write ConfigName (config + "\n[suppressions]\nignore = [\"App.fs\"]\n")
            | "baseline" -> write DefaultBaseline "clock.datetime\tApp.fs\tfingerprint\n"
            | "enabled" -> write "App.fsproj" (project.Replace("Enabled>true", "Enabled>false"))
            | "project-role" -> write "App.fsproj" (project.Replace("Role>domain", "Role>host"))
            | "guardrail" -> write "App.fsproj" (project.Replace("</ItemGroup>", "<AxialGuardrail Remove=\"Axial.PlatformService\"/></ItemGroup>"))
            | "test-project" -> write "App.fsproj" (project.Replace("IsTestProject>false", "IsTestProject>true"))
            | "verify" -> write "scripts/verify.sh" "echo skipped\n"
            | "workflow" -> write ".github/workflows/release.yml" "jobs: {}\n"
            | _ -> write "Directory.Packages.props" "<Project><ItemGroup><PackageVersion Include=\"Axial.Guardrails\" Version=\"0.12.0\"/></ItemGroup></Project>"
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"; "--check"]
            Assert.NotEqual(0, code)
            Assert.DoesNotContain("Unknown command", output)
            Assert.DoesNotContain("unknown key", output))

    [<Fact>]
    let ``tightening and removals do not fail check`` () =
        repository (fun root write git cli ->
            write "App.fs" "module App\n// axial-allow-AXG001: original\n"
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "exception"] |> ignore
            write "App.fs" "module App\n"
            let config = File.ReadAllText(Path.Combine(root, ConfigName))
            write ConfigName (config.Replace("= \"deny\"", "= \"forbid\""))
            let code, output = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.Equal(0, code)
            Assert.Contains("removed", output))

    [<Fact>]
    let ``review defaults to recorded commit and rejects invalid refs`` () =
        repository (fun root write git cli ->
            let sha = git ["rev-parse"; "HEAD"]
            write LockName (File.ReadAllText(Path.Combine(root, LockName)) + "REVIEWED_POINT  " + sha + "\n")
            let code, _ = cli ["policy"; "review"; "--check"]
            Assert.Equal(0, code)
            let code, _ = cli ["policy"; "review"; "--since"; "missing-ref"]
            Assert.NotEqual(0, code))

    [<Fact>]
    let ``adding an allow override relaxes the default deny`` () =
        repository (fun root write _ cli ->
            let config = File.ReadAllText(Path.Combine(root, ConfigName))
            write ConfigName (config + "\"effects.ambient\" = \"allow\"\n")
            let code, output = cli ["policy"; "review"; "--since"; "v0.11.3"; "--check"]
            Assert.NotEqual(0, code)
            Assert.Contains("effects.ambient", output))

    [<Fact>]
    let ``removing an allow override tightens to the default deny`` () =
        repository (fun root write git cli ->
            let config = File.ReadAllText(Path.Combine(root, ConfigName))
            write ConfigName (config + "\"effects.ambient\" = \"allow\"\n")
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "allow ambient"] |> ignore
            write ConfigName config
            let code, _ = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.Equal(0, code))

    [<Theory>]
    [<InlineData("scripts/verify.sh")>]
    [<InlineData(".github/workflows/release.yml")>]
    [<InlineData("Directory.Packages.props")>]
    let ``removing enforcement or analyzer package is reviewed`` (path: string) =
        repository (fun root write git cli ->
            let text = if path.EndsWith(".props") then "<Project><ItemGroup><PackageVersion Include=\"Axial.Guardrails\" Version=\"0.12.0\"/></ItemGroup></Project>" else "original\n"
            write path text
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "enforcement"] |> ignore
            File.Delete(Path.Combine(root, path))
            let code, output = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.NotEqual(0, code)
            Assert.Contains(path, output))

    [<Fact>]
    let ``conditional imported settings are evaluated for every target`` () =
        repository (fun root write git cli ->
            let project = File.ReadAllText(Path.Combine(root, "App.fsproj"))
            write "App.fsproj" (project.Replace("<TargetFramework>net8.0</TargetFramework>", "<TargetFrameworks>net8.0;net10.0</TargetFrameworks>").Replace("</Project>", "<Import Project=\"policy.props\"/></Project>"))
            write "policy.props" "<Project/>"
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "multitarget"] |> ignore
            write "policy.props" "<Project><PropertyGroup Condition=\"'$(TargetFramework)' == 'net10.0'\"><AxialGuardrailsEnabled>false</AxialGuardrailsEnabled></PropertyGroup></Project>"
            let code, output = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.NotEqual(0, code)
            Assert.Contains("net10.0", output))

    [<Fact>]
    let ``review supports the required pre TOML starting tag`` () =
        repository (fun root write git cli ->
            let config = File.ReadAllText(Path.Combine(root, ConfigName))
            let lockText = File.ReadAllText(Path.Combine(root, LockName))
            git ["rm"; ConfigName; LockName; DefaultBaseline] |> ignore
            git ["commit"; "-qm"; "pre-policy state like Axial v0.11.3"] |> ignore
            git ["tag"; "legacy"] |> ignore
            write ConfigName config
            write LockName lockText
            write DefaultBaseline ""
            let code, output = cli ["policy"; "review"; "--since"; "legacy"]
            Assert.True((code = 0), output)
            Assert.Contains("Policy introduced since legacy", output)
            Assert.Contains("policy files did not exist", output)
            Assert.Contains("projects: 1", output)
            Assert.Contains("roles:", output)
            Assert.Contains("runtime: 0", output)
            Assert.Contains("baseline: 0", output)
            let code, output = cli ["policy"; "review"; "--since"; "legacy"; "--check"; "--json"]
            Assert.NotEqual(0, code)
            use json = System.Text.Json.JsonDocument.Parse output
            Assert.Equal(1, json.RootElement.GetProperty("loosened").GetArrayLength())
            write "App.fs" "module App\n// axial-note AXG001: introduced note\n"
            let code, output = cli ["policy"; "review"; "--since"; "legacy"; "--check"; "--json"]
            Assert.NotEqual(0, code)
            use json = System.Text.Json.JsonDocument.Parse output
            Assert.Equal(2, json.RootElement.GetProperty("loosened").GetArrayLength())
            Assert.Contains("introduced note", output))

    [<Fact>]
    let ``approval text records HEAD even without policy differences`` () =
        repository (fun root write git cli ->
            let snapshot = inspect root |> function Ok value -> value | Error error -> failwith error
            let text = Axial.Tool.PolicyReview.prepareReviewedLock root snapshot
            Assert.Contains("REVIEWED_POINT  " + git ["rev-parse"; "HEAD"], text)
            write LockName text
            let code, output = cli ["policy"; "review"; "--check"]
            Assert.True((code = 0), output)
            write "App.fs" "module App\n// axial-note AXG001: after review\n"
            let code, output = cli ["policy"; "review"; "--check"]
            Assert.NotEqual(0, code)
            Assert.Contains("after review", output))

    [<Fact>]
    let ``deleted source exceptions are reported as removals`` () =
        repository (fun root write git cli ->
            write "Other.fs" "module Other\n// axial-note AXG001: old exception\n"
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "exception file"] |> ignore
            File.Delete(Path.Combine(root, "Other.fs"))
            let code, output = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.True((code = 0), output)
            Assert.Contains("removed suppressions/notes: Other.fs", output)
            let code, output = cli ["check"]
            Assert.True((code = 0), output))

    [<Fact>]
    let ``conditionally removed analyzer package settings are reviewed`` () =
        repository (fun root write git cli ->
            let project = File.ReadAllText(Path.Combine(root, "App.fsproj"))
            write "App.fsproj" (project.Replace("</Project>", "<Import Project=\"policy.props\"/><ItemGroup Condition=\"'$(EnableAnalyzer)' == 'true'\"><PackageReference Include=\"Axial.Guardrails\" Version=\"0.12.0\"/></ItemGroup></Project>"))
            write "policy.props" "<Project><PropertyGroup><EnableAnalyzer>true</EnableAnalyzer></PropertyGroup></Project>"
            git ["add"; "."] |> ignore
            git ["commit"; "-qm"; "conditional package"] |> ignore
            write "policy.props" "<Project><PropertyGroup><EnableAnalyzer>false</EnableAnalyzer></PropertyGroup></Project>"
            let code, output = cli ["policy"; "review"; "--since"; "HEAD"; "--check"]
            Assert.NotEqual(0, code)
            Assert.Contains("Axial.Guardrails version", output))

    [<Fact>]
    let ``malformed reviewed point is rejected`` () =
        repository (fun root write _ cli ->
            write LockName (File.ReadAllText(Path.Combine(root, LockName)) + "REVIEWED_POINT  invalid\n")
            let code, _ = cli ["check"]
            Assert.NotEqual(0, code))
