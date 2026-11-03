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
        let nuget = match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with null | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages") | value -> value
        File.WriteAllText(Path.Combine(dir, "Consumer.fsproj"), $"""<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit>
<_AxialGuardrailsAnalyzersPath>{escape (bin "Axial.Guardrails")}</_AxialGuardrailsAnalyzersPath>
<_AxialGuardrailsToolPath>{escape (Path.Combine(nuget, "fsharp-analyzers/0.37.2/tools/net8.0/any"))}/</_AxialGuardrailsToolPath>
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
        for arg in ["build"; "--nologo"; "-v:minimal"; "-nodeReuse:false"; "-m:1"; "-c"; config ()] @ args do start.ArgumentList.Add arg
        use child = Process.Start start
        let stdout, stderr = child.StandardOutput.ReadToEndAsync(), child.StandardError.ReadToEndAsync()
        if not (child.WaitForExit(90000)) then child.Kill true; failwith "build timed out"
        child.ExitCode, stdout.Result + stderr.Result
    let private report dir = Directory.GetFiles(dir, "*.result.xml", SearchOption.AllDirectories) |> Array.exactlyOne |> XDocument.Load
    let private attr name (e: XElement) = e.Attribute(XName.Get name).Value
    let private checkSuccess (code, output) = Assert.True((code = 0), output)
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
        Assert.Equal("true", attr "isolated" doc.Root)
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

    [<Theory>]
    [<InlineData("parser crash")>]
    [<InlineData("visitor crash")>]
    let ``Fast failure receipts aggregate once retain findings and never stamp under warnings as errors`` fault = withFixture (fun dir ->
        let plugin = Path.Combine(dir, "plugin")
        Directory.CreateDirectory plugin |> ignore
        File.WriteAllText(Path.Combine(plugin, "Plugin.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><AssemblyName>FakeFast</AssemblyName></PropertyGroup></Project>")
        File.WriteAllText(Path.Combine(plugin, "Runner.cs"), """using System.IO;
using System.Xml.Linq;
namespace Axial.Guardrails {
public static class FastRunner {
public static string Run(string input) {
 var root = new XElement("fast");
 foreach (var source in XElement.Parse(input).Elements("file")) {
  bool bad = Path.GetFileName(source.Value) == "Program.fs";
  if (bad) root.Add(new XElement("failure", new XAttribute("file", source.Value), "FAULT\nsecond-line cause"));
  foreach (var rule in new [] {"EffectBoundary", "RaiseInFlow", "Fixture", "ReflectionFormatting", "RawConcurrency", "WaitForFacts"}) {
   if (bad && ("FAULT" == "parser crash" || rule == "Fixture")) continue;
   var entry = new XElement("coverage", new XAttribute("file", source.Value), new XAttribute("rule", rule), new XAttribute("status", "ran"));
   if (!bad && rule == "EffectBoundary") entry.Add(new XElement("finding", new XAttribute("code", "AXG001"), new XAttribute("severity", "Info"), new XAttribute("line", 2), new XAttribute("column", 0), "kept available finding"));
   root.Add(entry);
  }
 }
 return root.ToString();
}}}
""".Replace("FAULT", fault))
        checkSuccess (build dir ["plugin/Plugin.csproj"])
        let assembly = Path.Combine(plugin, "bin", config (), "net8.0", "FakeFast.dll")
        let code, output = build dir ["-p:_AxialGuardrailsFastAssembly=" + assembly; "-warnaserror"]
        Assert.NotEqual(0, code)
        Assert.Contains(fault, output)
        Assert.Contains("kept available finding", output)
        let errors = output.Split('\n') |> Array.filter (fun line -> line.Contains(": error AXGINF001:")) |> Array.map _.Trim() |> Array.distinct
        Assert.Single errors |> ignore
        Assert.Equal("false", attr "complete" (report dir).Root)
        Assert.Empty(Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories)))

    [<Fact>]
    let ``corrupt MSBuild task assembly reports AXGINF001 rather than silently dropping analysis`` () = withFixture (fun dir ->
        let broken = Path.Combine(dir, "BrokenTask.dll")
        File.WriteAllText(broken, "not an assembly")
        let code, output = build dir ["-p:_AxialGuardrailsTaskAssembly=" + broken]
        Assert.NotEqual(0, code)
        Assert.Contains("AXGINF001", output)
        Assert.Contains("task-load failure", output)
        Assert.Empty(Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories)))

    [<Fact>]
    let ``inventory cache detects protected file additions removals and corrupted snapshots`` () = withFixture (fun dir ->
        let assembly = System.Reflection.Assembly.LoadFrom(Path.Combine(root (), "artifacts/bin/Axial.Guardrails.Build", config (), "Axial.Guardrails.Build.dll"))
        let typ = assembly.GetType("AxialGuardrailsTask")
        Directory.CreateDirectory(Path.Combine(dir, "obj")) |> ignore
        let memo = Path.Combine(dir, "obj", "inventory.xml")
        let inventory () =
            let instance = Activator.CreateInstance typ
            typ.GetProperty("WorkingDirectory").SetValue(instance, dir)
            typ.GetProperty("MemoFile").SetValue(instance, memo)
            typ.GetMethod("Inventory", System.Reflection.BindingFlags.Instance ||| System.Reflection.BindingFlags.NonPublic).Invoke(instance, [||]) |> ignore
            typ.GetProperty("InventoryFiles").GetValue(instance) :?> Microsoft.Build.Framework.ITaskItem[] |> Array.map _.ItemSpec
        let before = Directory.GetLastWriteTimeUtc dir
        Assert.Empty(inventory ())
        let props = Path.Combine(dir, "New.props")
        File.WriteAllText(props, "<Project/>")
        Directory.SetLastWriteTimeUtc(dir, before)
        Assert.Contains(props, inventory ())
        File.Delete props
        Directory.SetLastWriteTimeUtc(dir, before)
        Assert.DoesNotContain(props, inventory ())
        File.WriteAllText(memo, "<broken")
        Assert.Empty(inventory ()))

    [<Fact>]
    let ``Fast cannot satisfy Full even after a successful Fast stamp`` () = withFixture (fun dir ->
        checkSuccess (build dir ["-p:AxialGuardrailsMode=Fast"])
        let full = build dir ["-p:AxialGuardrailsMode=Full"; "-p:_AxialGuardrailsExe=DO_NOT_LAUNCH"; "-p:_AxialGuardrailsUsesPackagedTool=true"]
        Assert.NotEqual(0, fst full)
        Assert.Contains("AXGINF001", snd full)
        Assert.Equal("Full", attr "mode" (report dir).Root)
        Assert.Equal("false", attr "complete" (report dir).Root)
        Assert.Empty(Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories)))

    [<Fact>]
    let ``Framework fallback uses effective Full mode even with global Fast property`` () = withFixture (fun dir ->
        let code, output = build dir ["-p:AxialGuardrailsMode=Fast"; "-p:_AxialGuardrailsFrameworkFallback=true"]
        Assert.NotEqual(0, code)
        Assert.Contains("Framework MSBuild uses out-of-process Full", output)
        Assert.Equal("Full", attr "mode" (report dir).Root)
        Assert.Contains("AXG001", output))

    [<Fact>]
    let ``Fast exact fixture errors block and cached findings still block`` () = withFixture (fun dir ->
        File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\nopen System\ntype FactAttribute() = inherit Attribute()\nlet fixture = obj()\n[<Fact>]\nlet test () = ()\n")
        let args = ["-p:AxialGuardrailsMode=Fast"]
        let first = build dir args
        Assert.NotEqual(0, fst first)
        Assert.Contains("error AXG004", snd first)
        Assert.Contains("syntax-exact", snd first)
        let cached = build dir args
        Assert.NotEqual(0, fst cached)
        Assert.Contains("error AXG004", snd cached)
        Assert.DoesNotContain("Running Axial.Guardrails analysis", snd cached))

    [<Fact>]
    let ``Fast ambiguous hints never block warnings as errors`` () = withFixture (fun dir ->
        let result = build dir ["-p:AxialGuardrailsMode=Fast"; "-warnaserror"; "-p:TreatWarningsAsErrors=true"]
        checkSuccess result
        Assert.Contains("fast/unverified", snd result))

    [<Fact>]
    let ``concurrent Fast builds of the same project publish valid reusable coverage`` () = withFixture (fun dir ->
        let args = ["-p:AxialGuardrailsMode=Fast"]
        checkSuccess (build dir args)
        Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories) |> Array.iter File.Delete
        let first = System.Threading.Tasks.Task.Run(fun () -> build dir args)
        let second = System.Threading.Tasks.Task.Run(fun () -> build dir args)
        checkSuccess first.Result
        checkSuccess second.Result
        let cached = build dir args
        checkSuccess cached
        Assert.DoesNotContain("Running Axial.Guardrails analysis", snd cached))

    [<Fact>]
    let ``concurrent Fast TFMs keep independent complete reports and stamps`` () = withFixture (fun dir ->
        let project = Path.Combine(dir, "Consumer.fsproj")
        File.WriteAllText(project, File.ReadAllText(project).Replace("<TargetFramework>net10.0</TargetFramework>", "<TargetFrameworks>net8.0;net10.0</TargetFrameworks>"))
        checkSuccess (build dir ["-p:AxialGuardrailsMode=Fast"])
        Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories) |> Array.iter File.Delete
        let first = System.Threading.Tasks.Task.Run(fun () -> build dir ["-p:AxialGuardrailsMode=Fast"; "-f"; "net8.0"])
        let second = System.Threading.Tasks.Task.Run(fun () -> build dir ["-p:AxialGuardrailsMode=Fast"; "-f"; "net10.0"])
        checkSuccess first.Result
        checkSuccess second.Result
        let reports = Directory.GetFiles(dir, "*.result.xml", SearchOption.AllDirectories) |> Array.map XDocument.Load
        Assert.Equal(2, reports.Length)
        for doc in reports do Assert.Equal("true", attr "complete" doc.Root)
        Assert.Equal(2, Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories).Length))

    [<Theory>]
    [<InlineData("-p:DefineConstants=SPECIAL")>]
    [<InlineData("-p:OtherFlags=--define:SPECIAL")>]
    let ``Fast defines invalidate syntax results including inactive branches`` defineArgument = withFixture (fun dir ->
        File.WriteAllText(Path.Combine(dir, "Program.fs"), "module Consumer\n#if SPECIAL\nlet now () = System.DateTimeOffset.UtcNow\n#else\nlet now () = 1\n#endif\n")
        let args = ["-p:AxialGuardrailsMode=Fast"]
        let first = build dir args
        checkSuccess first
        Assert.DoesNotContain("AXG001", snd first)
        let second = build dir (defineArgument :: args)
        checkSuccess second
        Assert.Contains("AXG001", snd second)
        let restored = build dir args
        checkSuccess restored
        Assert.DoesNotContain("AXG001", snd restored))

    [<Fact>]
    let ``Fast rejects untracked response file flags without stamping`` () = withFixture (fun dir ->
        let response = Path.Combine(dir, "extra.rsp")
        File.WriteAllText(response, "--define:SPECIAL\n")
        let code, output = build dir ["-p:AxialGuardrailsMode=Fast"; "-p:OtherFlags=@" + response]
        Assert.NotEqual(0, code)
        Assert.Contains("AXGINF001", output)
        Assert.Contains("response files", output)
        Assert.Empty(Directory.GetFiles(dir, "*.stamp", SearchOption.AllDirectories)))

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
