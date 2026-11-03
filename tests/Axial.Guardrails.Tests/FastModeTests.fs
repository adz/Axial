namespace Axial.Guardrails.Tests

open System
open System.IO
open System.Diagnostics
open System.Xml.Linq
open Xunit

module FastModeTests =
    let private root () = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."))
    let private config () = if AppContext.BaseDirectory.Contains("/release/") then "release" else "debug"
    let private escape (s: string) = System.Security.SecurityElement.Escape s
    let private fixture () =
        let dir = Path.Combine(Path.GetTempPath(), "axial-fast-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        let bin name = Path.Combine(root (), "artifacts/bin", name, config ())
        File.WriteAllText(Path.Combine(dir, "Consumer.fsproj"), $"""<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit>
<_AxialGuardrailsAnalyzersPath>{escape (bin "Axial.Guardrails")}</_AxialGuardrailsAnalyzersPath>
<_AxialGuardrailsTaskAssembly>{escape (Path.Combine(bin "Axial.Guardrails.Build", "Axial.Guardrails.Build.dll"))}</_AxialGuardrailsTaskAssembly>
</PropertyGroup><ItemGroup><Compile Include="Program.fs"/><Compile Include="Other.fs"/><AxialGuardrail Include="Axial"/></ItemGroup>
<Import Project="{escape (Path.Combine(root (), "src/Axial.Guardrails/build/Axial.Guardrails.props"))}"/>
<Import Project="{escape (Path.Combine(root (), "src/Axial.Guardrails/build/Axial.Guardrails.targets"))}"/>
</Project>""")
        File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\nlet now () = System.DateTimeOffset.UtcNow\n")
        File.WriteAllText(Path.Combine(dir, "Other.fs"), "module Other\nlet value = 1\n")
        dir
    let private build dir args =
        let start = ProcessStartInfo("dotnet")
        start.WorkingDirectory <- dir
        start.UseShellExecute <- false
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        for arg in ["build"; "--nologo"; "-v:q"; "-nodeReuse:false"; "-m:1"; "-c"; config ()] @ args do start.ArgumentList.Add arg
        use child = Process.Start start
        let stdout, stderr = child.StandardOutput.ReadToEndAsync(), child.StandardError.ReadToEndAsync()
        if not (child.WaitForExit(90000)) then child.Kill true; failwith "build timed out"
        child.ExitCode, stdout.Result + stderr.Result
    let private report dir = Directory.GetFiles(dir, "*.result.xml", SearchOption.AllDirectories) |> Array.exactlyOne |> XDocument.Load
    let private attr name (e: XElement) = e.Attribute(XName.Get name).Value
    let private checkSuccess (code, output) = Assert.True(code = 0, output)
    let private withFixture test =
        let dir = fixture ()
        try test dir finally Directory.Delete(dir, true)

    [<Fact>]
    let ``Fast is default runs syntax hints and explicitly defers typed coverage`` () = withFixture (fun dir ->
        let result = build dir []
        checkSuccess result
        Assert.Contains("fast/unverified", snd result)
        let doc = report dir
        Assert.Equal("Fast", attr "mode" doc.Root)
        let entries = doc.Root.Elements(XName.Get "coverage") |> Seq.toArray
        Assert.Contains(entries, fun e -> attr "rule" e = "Fixture" && attr "status" e = "ran")
        Assert.Contains(entries, fun e -> attr "rule" e = "ScopeFromRun" && attr "status" e = "deferred to full"))

    [<Fact>]
    let ``Fast never launches the analyzer CLI and cache replays hints`` () = withFixture (fun dir ->
        let args = ["-p:AxialGuardrailsMode=Fast"; "-p:_AxialGuardrailsExe=DO_NOT_LAUNCH"; "-p:_AxialGuardrailsUsesPackagedTool=true"]
        checkSuccess (build dir args)
        let second = build dir args
        checkSuccess second
        Assert.Contains("fast/unverified", snd second)
        Assert.DoesNotContain("Running Axial.Guardrails analysis", snd second))

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``Fast load failure is one infrastructure error no stamp even with warnings as errors`` werror = withFixture (fun dir ->
        let code, output = build dir (["-p:AxialGuardrailsMode=Fast"; "-p:_AxialGuardrailsFastAssembly=/missing/Fast.dll"] @ if werror then ["-warnaserror"] else [])
        Assert.NotEqual(0, code)
        Assert.Contains("AXGINF001", output)
        Assert.Empty(Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories)))

    [<Fact>]
    let ``Fast defines invalidate syntax results including inactive branches`` () = withFixture (fun dir ->
        File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\n#if SPECIAL\nlet now () = System.DateTimeOffset.UtcNow\n#else\nlet now () = 1\n#endif\n")
        let args = ["-p:AxialGuardrailsMode=Fast"]
        let first = build dir args
        checkSuccess first
        Assert.DoesNotContain("AXG001", snd first)
        let second = build dir ("-p:DefineConstants=SPECIAL" :: args)
        checkSuccess second
        Assert.Contains("AXG001", snd second))

    [<Fact>]
    let ``Fast stale report is rejected and only changed file is reparsed`` () = withFixture (fun dir ->
        let args = ["-p:AxialGuardrailsMode=Fast"; "-p:AxialGuardrailsTimings=true"]
        checkSuccess (build dir args)
        File.AppendAllText(Path.Combine(dir, "Program.fs"), "// edit\n")
        checkSuccess (build dir args)
        let entries = (report dir).Root.Elements(XName.Get "coverage") |> Seq.filter (fun e -> attr "rule" e = "Fixture") |> Seq.toArray
        Assert.Contains(entries, fun e -> attr "file" e |> fun p -> p.EndsWith("Other.fs") && attr "cacheHit" e = "true")
        Assert.Contains(entries, fun e -> attr "file" e |> fun p -> p.EndsWith("Program.fs") && attr "cacheHit" e = "false")
        let resultPath = Directory.GetFiles(dir, "*.result.xml", SearchOption.AllDirectories) |> Array.exactlyOne
        File.WriteAllText(resultPath, "<broken")
        checkSuccess (build dir args)
        Assert.Equal("true", attr "complete" (report dir).Root))
