namespace Axial.Guardrails.Tests

open System
open System.IO
open System.Xml.Linq
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Axial.Guardrails
open Xunit

module FastSyntaxTests =
    let private parse defines source =
        let checker = FSharpChecker.Create()
        let opts = { FSharpParsingOptions.Default with SourceFiles = [|"/tmp/Fast.fs"|]; ConditionalDefines = defines }
        checker.ParseFile("/tmp/Fast.fs", SourceText.ofString source, opts) |> Async.RunSynchronously |> _.ParseTree
    let private withConfig role action =
        let dir = Path.Combine(Path.GetTempPath(), "axial-syntax-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        let policy = Path.Combine(dir, "Axial.Guardrails.toml")
        File.WriteAllText(Path.Combine(dir, "Fast.fs"), "module M\n")
        File.WriteAllText(policy, $"version = 2\n[default]\nrole = \"{role}\"\n")
        try
            let config = GuardrailConfig.parse [ "guardrail\tAxial"; "guardrail\tAxial.Console"; "guardrail\tAxial.FileSystem"; "setting\tinTests\tall"; $"setting\tpolicyRoot\t{dir}"; $"setting\tpolicyFile\t{policy}"; $"setting\tprojectPath\t{dir}/Tests.fsproj" ]
            action config (Path.Combine(dir, "Fast.fs"))
        finally Directory.Delete(dir, true)
    let private analyzeFor code source = withConfig (if code = "AXG013" then "test" else "domain") (fun config file -> FastSyntax.analyze config file (SourceText.ofString source) (parse [] source))
    let private analyze source = analyzeFor "AXG004" source

    [<Theory>]
    [<InlineData("AXG001", "let value () = System.DateTime.UtcNow")>]
    [<InlineData("AXG003", "let value = flow { return raise (System.Exception()) }")>]
    [<InlineData("AXG006", "let value x = sprintf \"%+A\" x")>]
    [<InlineData("AXG009", "let value () = System.Threading.Tasks.Task.Run(fun () -> ())")>]
    [<InlineData("AXG013", "let value () = task { do! System.Threading.Tasks.Task.Delay(20) }")>]
    let ``syntax advice is unverified and never an ambiguous blocking diagnostic`` code body =
        let source = "module M\n" + body + "\n"
        let findings = analyzeFor code source |> List.map snd |> List.filter (fun m -> m.Code = code)
        Assert.NotEmpty findings
        for finding in findings do
            Assert.Equal(FSharp.Analyzers.SDK.Severity.Info, finding.Severity)
            Assert.Contains("fast/unverified", finding.Message)

    [<Theory>]
    [<InlineData("let System = obj()\nlet x = System.DateTime.UtcNow", "AXG001")>]
    [<InlineData("let flow = obj()\nlet x = flow { return raise (System.Exception()) }", "AXG003")>]
    [<InlineData("let raise x = x\nlet x = flow { return raise 1 }", "AXG003")>]
    [<InlineData("let x = flow { let! v = Flow.fromTask (fun _ -> task { return raise (System.Exception()) }) in return v }", "AXG003")>]
    [<InlineData("let sprintf x y = x\nlet x = sprintf \"%A\" 1", "AXG006")>]
    [<InlineData("module Task = CustomTask\nlet x = Task.Run(fun () -> ())", "AXG009")>]
    [<InlineData("module Task = CustomTask\nlet x = Task.Delay(20)", "AXG013")>]
    let ``shadowing and aliases never claim identity`` body code =
        Assert.DoesNotContain(analyzeFor code ("module M\n" + body + "\n"), fun (_, m) -> m.Code = code)

    [<Theory>]
    [<InlineData("AXG001", "let x = System.DateTime.UtcNow")>]
    [<InlineData("AXG003", "let x = flow { return failwith \"no\" }")>]
    [<InlineData("AXG006", "let x = sprintf \"%A\" 1")>]
    [<InlineData("AXG004", "let fixture = obj()\n[<Xunit.Fact>]\nlet test () = ()")>]
    [<InlineData("AXG009", "let x = System.Threading.Tasks.Task.Run(fun () -> ())")>]
    [<InlineData("AXG013", "let x = task { do! System.Threading.Tasks.Task.Delay(20) }")>]
    let ``inactive conditional syntax produces no advice`` code body =
        let source = "module M\n#if ACTIVE\n" + body + "\n#endif\n"
        withConfig (if code = "AXG013" then "test" else "domain") (fun config file ->
            let run defines = FastSyntax.analyze config file (SourceText.ofString source) (parse defines source)
            Assert.DoesNotContain(run [], fun (_, m) -> m.Code = code)
            Assert.Contains(run ["ACTIVE"], fun (_, m) -> m.Code = code))

    [<Theory>]
    [<InlineData("open System\nlet c = { new System.IDisposable with member _.Dispose() = ignore DateTimeOffset.UtcNow }")>]
    [<InlineData("let run () = async { return 1 } |> Async.RunSynchronously")>]
    [<InlineData("open System\nlet read () = IO.File.ReadAllText \"x\"")>]
    [<InlineData("let wait (work: System.Threading.Tasks.Task<int>) = work.GetAwaiter().GetResult()")>]
    let ``AXG001 advice reaches object expressions pipelines partial namespaces and awaiter chains`` body =
        Assert.Contains(analyzeFor "AXG001" ("module M\n" + body + "\n"), fun (_, m) -> m.Code = "AXG001")

    [<Fact>]
    let ``many opens stay linear and still resolve relative namespaces`` () =
        let opens = [ for i in 1 .. 40 -> $"open System.Generated{i}" ] |> String.concat "\n"
        let source = "module M\n" + opens + "\nopen System\nlet read () = IO.File.ReadAllText \"x\"\n"
        let watch = System.Diagnostics.Stopwatch.StartNew()
        Assert.Contains(analyzeFor "AXG001" source, fun (_, m) -> m.Code = "AXG001")
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds 10.0, string watch.Elapsed)

    [<Theory>]
    [<InlineData("let read () = Console.ReadLine()")>]
    [<InlineData("open System\nopen Axial\nlet console = Console.live")>]
    [<InlineData("let wait (work: System.Threading.Tasks.Task<int>) = work.Result")>]
    let ``AXG001 advice needs an opened namespace and a BCL member shape`` body =
        Assert.DoesNotContain(analyzeFor "AXG001" ("module M\n" + body + "\n"), fun (_, m) -> m.Code = "AXG001")

    [<Theory>]
    [<InlineData("open System.Threading.Tasks\nlet t () = task { do! Task.Delay 20 }", true)>]
    [<InlineData("let t () = async { do! Async.Sleep 20 }", true)>]
    [<InlineData("open System.Threading\nlet t () = Thread.Sleep 1", true)>]
    [<InlineData("let t () = System.Threading.Tasks.Task.Delay 20", false)>]
    [<InlineData("open System.Threading\nopen System.Threading.Tasks\nlet t (ct: CancellationToken) = task { do! Task.Delay(Timeout.Infinite, ct) }", false)>]
    [<InlineData("let t () = async { do! Async.Sleep System.Threading.Timeout.Infinite }", false)>]
    let ``AXG013 advice mirrors awaited delays and cancellation waits`` body expected =
        let found = analyzeFor "AXG013" ("module M\n" + body + "\n") |> List.exists (fun (_, m) -> m.Code = "AXG013")
        Assert.Equal(expected, found)

    [<Theory>]
    [<InlineData("let value = 1", 0)>]
    [<InlineData("let value = obj()", 1)>]
    [<InlineData("let value () = obj()", 0)>]
    [<InlineData("let value = function | x -> x", 0)>]
    [<InlineData("module Nested =\n    let value = obj()\n    [<Xunit.FactAttribute>]\n    let test () = ()", 1)>]
    let ``AXG004 shared visitor preserves nesting attributes and fixture shapes`` body expected =
        let source = "module M\n" + body + "\n[<Xunit.FactAttribute>]\nlet test () = ()\n"
        let tree = parse [] source
        let fast = analyze source |> List.filter (fun (_, m) -> m.Code = "AXG004")
        Assert.Equal(expected, fast.Length)
        Assert.Equal<int list>(FixtureSyntax.ranges tree |> List.map _.StartLine, fast |> List.map (snd >> _.Range.StartLine))

    [<Theory>]
    [<InlineData("host", false)>]
    [<InlineData("capabilities", false)>]
    [<InlineData("domain", true)>]
    [<InlineData("services", true)>]
    [<InlineData("test", true)>]
    let ``AXG001 Full and Fast use file role overrides in a test project`` role expected =
        task {
            let source = "module M\nlet now () = System.DateTime.UtcNow\n"
            let! ctx = AnalyzerTestContext.create source
            let root = Path.GetDirectoryName ctx.FileName
            Directory.CreateDirectory root |> ignore
            File.WriteAllText(ctx.FileName, source)
            let policyPath = Path.Combine(root, "roles-" + Guid.NewGuid().ToString("N") + ".toml")
            File.WriteAllText(policyPath, $"version = 2\n[default]\nrole = \"test\"\n[files]\n{role} = [\"{Path.GetFileName ctx.FileName}\"]\n")
            try
                let conf = GuardrailConfig.parse ["guardrail\tAxial"; $"setting\tpolicyRoot\t{root}"; $"setting\tpolicyFile\t{policyPath}"; $"setting\tprojectPath\t{root}/Tests.fsproj"]
                let fast = FastSyntax.analyze conf ctx.FileName ctx.SourceText ctx.ParseFileResults.ParseTree |> List.exists (fun (_, m) -> m.Code = "AXG001")
                Assert.Equal(expected, fast)
                let full = EffectBoundaryAnalyzer.analyzeWithConfig conf ctx |> List.exists (fun m -> m.Code = "AXG001")
                Assert.Equal(expected, full)
            finally Directory.Delete(root, true)
        }

    [<Theory>]
    [<InlineData("host")>]
    [<InlineData("capabilities")>]
    let ``forbid remains unsuppressible in effect role files in both modes`` role =
        withConfig role (fun original path ->
            let root = original.PolicyRoot.Value
            let source = "// axial-allow-file-AXG001: negative control for an unsuppressible policy\nmodule M\nlet now () = System.DateTime.UtcNow\n"
            File.WriteAllText(path, source)
            let policy = Path.Combine(root, "Axial.Guardrails.toml")
            File.AppendAllText(policy, $"\n[roles.{role}.rules]\n\"effects.ambient\" = \"forbid\"\n[boundaries]\nfiles = [\"Fast.fs\"]\n")
            let config = GuardrailConfig.parse ["guardrail\tAxial"; $"setting\tpolicyRoot\t{root}"; $"setting\tpolicyFile\t{policy}"; $"setting\tprojectPath\t{root}/Tests.fsproj"]
            let ctx = AnalyzerTestContext.createAt path source |> fun t -> t.GetAwaiter().GetResult()
            Assert.NotEmpty(EffectBoundaryAnalyzer.analyzeWithConfig config ctx)
            Assert.Contains(FastSyntax.analyze config path ctx.SourceText ctx.ParseFileResults.ParseTree, fun (_, message) -> message.Code = "AXG001"))

    [<Fact>]
    let ``Fast advice is compared with Full diagnostics on a typed corpus`` () =
        let corpus = [
            "AXG001", "domain", "module M\nlet direct () = System.DateTime.UtcNow\ntype Clock = System.DateTime\nlet aliased () = Clock.UtcNow\n"
            "AXG001", "domain", "module M\nmodule System =\n    let value = 1\nlet safe = System.value\n"
            "AXG003", "domain", "module M\nopen Axial\nlet work : Flow<unit,string,int> = flow { return failwith \"no\" }\n"
            "AXG003", "domain", "module M\nopen Axial\nlet raise x = x\nlet work : Flow<unit,string,int> = flow { return raise 1 }\n"
            "AXG004", "test", "module M\nlet fixture = obj()\n[<Xunit.Fact>]\nlet test () = ()\n"
            "AXG004", "test", "module M\nlet fixture () = obj()\n[<Xunit.Fact>]\nlet test () = ()\n"
            "AXG006", "domain", "module M\nlet render value = sprintf \"%A\" value\nlet plain value = sprintf \"%s\" value\n"
            "AXG006", "domain", "module M\nlet sprintf x y = x\nlet render = sprintf \"%A\" 1\n"
            "AXG009", "domain", "module M\nlet work () = System.Threading.Tasks.Task.Run(fun () -> ())\ntype T = System.Threading.Tasks.Task\nlet other () = T.Run(fun () -> ())\n"
            "AXG009", "domain", "module M\nlet safe = System.Threading.Tasks.Task.CompletedTask\n"
            "AXG013", "test", "module M\nlet work () = task { do! System.Threading.Tasks.Task.Delay 20 }\nlet unawaited () = System.Threading.Tasks.Task.Delay 20\n"
            "AXG013", "test", "module M\nlet cancelled (ct: System.Threading.CancellationToken) = task { do! System.Threading.Tasks.Task.Delay(System.Threading.Timeout.Infinite, ct) }\n"
            "AXG001", "domain", "module M\nopen System\nlet clock = { new IDisposable with member _.Dispose() = ignore DateTimeOffset.UtcNow }\nlet read () = IO.File.ReadAllText \"x\"\n"
            "AXG001", "domain", "module M\nlet run () = async { return 1 } |> Async.RunSynchronously\nlet wait (work: System.Threading.Tasks.Task<int>) = work.GetAwaiter().GetResult()\n"
            "AXG001", "domain", "module M\nlet blocked (work: System.Threading.Tasks.Task<int>) = work.Result\n"
            "AXG013", "test", "module M\nopen System.Threading\nlet poll () = Thread.Sleep 1\nlet slow () = async { do! Async.Sleep 20 }\n" ]
        let counts = System.Collections.Generic.Dictionary<string, int * int * int>()
        for code, role, source in corpus do
            withConfig role (fun config path ->
                File.WriteAllText(path, source)
                let ctx = AnalyzerTestContext.createWithAxialAt path source |> fun t -> t.GetAwaiter().GetResult()
                Assert.Empty(ctx.CheckFileResults.Diagnostics |> Array.filter (fun d -> d.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error))
                let fast = FastSyntax.analyze config path ctx.SourceText ctx.ParseFileResults.ParseTree |> List.map snd |> List.filter (fun m -> m.Code = code)
                let full =
                    match code with
                    | "AXG001" -> EffectBoundaryAnalyzer.analyzeWithConfig config ctx
                    | "AXG003" -> RaiseInFlowAnalyzer.raiseInFlowAnalyzer ctx |> Async.RunSynchronously
                    | "AXG004" -> FixtureAnalyzer.fixtureAnalyzer ctx |> Async.RunSynchronously
                    | "AXG006" -> ReflectionFormattingAnalyzer.analyzeWithNativeAot true ctx
                    | "AXG009" -> ConcurrencyHazardsAnalyzer.analyzeWithConfig config code ctx
                    | _ -> WaitForFactsAnalyzer.analyzeWithConfig config ctx
                let sites messages = messages |> List.map (fun (m: FSharp.Analyzers.SDK.Message) -> m.Range.StartLine) |> Set.ofList
                let fastSites, fullSites = sites fast, sites full
                let tp = Set.intersect fastSites fullSites |> Set.count
                let fp = Set.difference fastSites fullSites |> Set.count
                let fn = Set.difference fullSites fastSites |> Set.count
                let a,b,c = match counts.TryGetValue code with true, value -> value | _ -> 0,0,0
                counts[code] <- a+tp,b+fp,c+fn)
        let json = counts |> Seq.map (fun kv -> let tp,fp,fn = kv.Value in {| rule=kv.Key; tp=tp; fp=fp; fn=fn |}) |> Seq.toArray |> System.Text.Json.JsonSerializer.Serialize
        let output = Path.Combine(AppContext.BaseDirectory, "../../..", "gr-fast-corpus-metrics.json")
        File.WriteAllText(Path.GetFullPath output, json)
        Assert.True(counts["AXG004"] = (1,0,0))
        // Ambiguous syntax is skipped rather than guessed: no rule may claim a site the typed rule rejects.
        for KeyValue(code, (_, fp, _)) in counts do Assert.True((fp = 0), $"{code} Fast false positives: {fp}")

    [<Theory>]
    [<InlineData("syntax")>]
    [<InlineData("parse crash")>]
    [<InlineData("visitor crash")>]
    let ``failures retain other files and visitors without caching incomplete coverage`` fault =
        let dir = Path.Combine(Path.GetTempPath(), "axial-fast-parse-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            let bad, good = Path.Combine(dir, "Bad.fs"), Path.Combine(dir, "Good.fs")
            File.WriteAllText(bad, if fault = "syntax" then "module Bad\nlet =\n" else "module Bad\nlet now () = System.DateTime.UtcNow\n")
            File.WriteAllText(good, "module Good\nlet now () = System.DateTime.UtcNow\n")
            let config = Path.Combine(dir, "rules")
            File.WriteAllText(config, "guardrail\tAxial\n")
            let request = XElement(XName.Get "request", XAttribute(XName.Get "config", config), XAttribute(XName.Get "context", "test"), XAttribute(XName.Get "cache", Path.Combine(dir, "cache")), XAttribute(XName.Get "langVersion", "preview"), XElement(XName.Get "file", bad), XElement(XName.Get "file", good))
            let parseFile (path: string) (source: ISourceText) (options: FSharpParsingOptions) =
                if fault = "parse crash" && path = bad then failwith "injected parser crash"
                let parsed = FSharpChecker.Create().ParseFile(path, source, options) |> Async.RunSynchronously
                if parsed.ParseHadErrors then failwith "parse errors"
                parsed.ParseTree
            let visit rule config path source tree =
                if fault = "visitor crash" && path = bad && rule = Some "Fixture" then failwith "injected visitor crash"
                FastSyntax.analyzeRule rule config path source tree
            let result = FastRunner.runWith parseFile visit (request.ToString()) |> XElement.Parse
            Assert.Single(result.Elements(XName.Get "failure")) |> ignore
            Assert.Equal((if fault = "visitor crash" then 14 else 7), result.Elements(XName.Get "coverage") |> Seq.length)
            Assert.Single(Directory.GetFiles(Path.Combine(dir, "cache"))) |> ignore
        finally Directory.Delete(dir, true)
