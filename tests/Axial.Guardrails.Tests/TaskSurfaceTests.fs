namespace Axial.Guardrails.Tests

open System
open System.IO
open System.Xml.Linq
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Axial.Guardrails
open Xunit

module TaskSurfaceTests =
    let private checker = lazy (FSharpChecker.Create())
    let private parse (path: string) source =
        let options = { FSharpParsingOptions.Default with SourceFiles = [| path |] }
        checker.Value.ParseFile(path, SourceText.ofString source, options) |> Async.RunSynchronously |> _.ParseTree

    /// A repository with Fast.fs, an optional runtime declaration and a policy whose default role is `role`.
    let private withPolicy (role: string) (extra: string) (settings: string list) action =
        let dir = Path.Combine(Path.GetTempPath(), "axial-task-surface-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        let policy = Path.Combine(dir, "Axial.Guardrails.toml")
        File.WriteAllText(Path.Combine(dir, "Fast.fs"), "module M\n")
        File.WriteAllText(policy, $"version = 2\n[default]\nrole = \"{role}\"\n{extra}")
        try
            let config =
                GuardrailConfig.parse (
                    [ "guardrail\tAxial"; $"setting\tpolicyRoot\t{dir}"; $"setting\tpolicyFile\t{policy}"; $"setting\tprojectPath\t{dir}/App.fsproj" ] @ settings)
            action config (Path.Combine(dir, "Fast.fs"))
        finally Directory.Delete(dir, true)

    let private fast config path source =
        FastSyntax.analyze config path (SourceText.ofString source) (parse path source)
        |> List.map snd |> List.filter (fun m -> m.Code = "AXG014")

    let private full config path (source: string) =
        File.WriteAllText(path, source)
        let ctx = AnalyzerTestContext.wait (fun () -> AnalyzerTestContext.createWithAxialAt path source)
        Assert.Empty(ctx.CheckFileResults.Diagnostics |> Array.filter (fun d -> d.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error))
        TaskSurfaceAnalyzer.analyzeWithConfig config ctx

    let private lines (messages: FSharp.Analyzers.SDK.Message list) = messages |> List.map _.Range.StartLine |> Set.ofList

    [<Theory>]
    [<InlineData("let run () = task { return 1 }", "task { }")>]
    [<InlineData("let run () = backgroundTask { return 1 }", "backgroundTask { }")>]
    [<InlineData("open System.Threading.Tasks\nlet run () : Task<int> = Task.FromResult 1", "returns Task<'T>")>]
    [<InlineData("type Api =\n    abstract Load: unit -> System.Threading.Tasks.ValueTask", "returns ValueTask")>]
    [<InlineData("type Service() =\n    member _.Run() : System.Threading.Tasks.Task = System.Threading.Tasks.Task.CompletedTask", "returns Task")>]
    let ``domain code gets a Fast guide hint for raw task expressions and Task signatures`` (body: string) (expected: string) =
        withPolicy "domain" "" [] (fun config path ->
            let findings = fast config path ("module M\n" + body + "\n")
            Assert.NotEmpty findings
            for finding in findings do
                Assert.Equal(FSharp.Analyzers.SDK.Severity.Info, finding.Severity)
                Assert.StartsWith("[fast/unverified] AXG014 guide:", finding.Message)
            Assert.Contains(findings, fun m -> m.Message.Contains expected)
            Assert.Contains(findings, fun m -> m.Message.Contains "Flow.fromTask"))

    [<Theory>]
    [<InlineData("let task = async\nlet run () = task { return 1 }")>]
    [<InlineData("let run () = async { return 1 }")>]
    [<InlineData("let private run () : System.Threading.Tasks.Task = System.Threading.Tasks.Task.CompletedTask")>]
    [<InlineData("module private Inner =\n    let run () : System.Threading.Tasks.Task = System.Threading.Tasks.Task.CompletedTask")>]
    [<InlineData("type Task = int\nlet run () : Task = 1")>]
    [<InlineData("type T = System.Threading.Tasks.Task\nlet run () : T = T.CompletedTask")>]
    [<InlineData("type Wrapper() =\n    interface System.IAsyncDisposable with\n        member _.DisposeAsync() : System.Threading.Tasks.ValueTask = System.Threading.Tasks.ValueTask.CompletedTask")>]
    let ``Fast never claims shadowed aliased private or interface-dictated task shapes`` (body: string) =
        withPolicy "domain" "" [] (fun config path -> Assert.Empty(fast config path ("module M\n" + body + "\n")))

    [<Theory>]
    [<InlineData("capabilities", "let read () = task { return! System.Threading.Tasks.Task.FromResult 1 }", false)>]
    [<InlineData("host", "let read () = task {\n    let! value = System.Threading.Tasks.Task.FromResult 1\n    return value + 1 }", false)>]
    [<InlineData("capabilities", "let read () : System.Threading.Tasks.Task<int> = System.Threading.Tasks.Task.FromResult 1", false)>]
    [<InlineData("capabilities", "let read () = task {\n    let! a = System.Threading.Tasks.Task.FromResult 1\n    let! b = System.Threading.Tasks.Task.FromResult 2\n    return a + b }", true)>]
    [<InlineData("host", "let read (xs: int list) = task {\n    for x in xs do\n        do! System.Threading.Tasks.Task.Delay x }", true)>]
    [<InlineData("capabilities", "let read flag = task {\n    if flag then return! System.Threading.Tasks.Task.FromResult 1 else return 0 }", true)>]
    [<InlineData("capabilities", "let read () = task {\n    let inner = fun () -> if true then 1 else 2\n    return! System.Threading.Tasks.Task.FromResult(inner ()) }", false)>]
    [<InlineData("capabilities", "let read () = task {\n    try\n        let! value = System.Threading.Tasks.Task.FromResult 1\n        return Ok value\n    with :? System.IO.IOException as error -> return Error error.Message }", false)>]
    [<InlineData("capabilities", "let read () = task {\n    let! value = System.Threading.Tasks.Task.FromResult 1\n    match value with\n    | 1 -> return Ok value\n    | _ -> return Error \"unexpected\" }", false)>]
    [<InlineData("capabilities", "let read (path: string) = task {\n    if isNull path then nullArg \"path\"\n    let! value = System.Threading.Tasks.Task.FromResult path\n    return value }", false)>]
    [<InlineData("capabilities", "let read flag = task {\n    if flag then\n        do! System.Threading.Tasks.Task.Yield() }", true)>]
    [<InlineData("capabilities", "let read flag = task {\n    match flag with\n    | true -> return 0\n    | false ->\n        let! value = System.Threading.Tasks.Task.FromResult 1\n        return value }", true)>]
    let ``effect roles allow a thin bridge and guide a composing one`` (role: string) (body: string) (expected: bool) =
        withPolicy role "" [] (fun config path ->
            let findings = fast config path ("module M\n" + body + "\n")
            Assert.Equal(expected, not findings.IsEmpty)
            for finding in findings do Assert.Contains("bridge composes", finding.Message))

    [<Theory>]
    [<InlineData("open FSharp.Control.Tasks")>]
    [<InlineData("open FSharp.Control.Tasks.Affine")>]
    let ``Fast does not claim a task that a Ply or TaskBuilder.fs open may rebind`` (opening: string) =
        // Full reports only FSharp.Core's builders, so these stay out of Fast to keep Fast within Full.
        withPolicy "domain" "" [] (fun config path ->
            Assert.Empty(fast config path ("module M\n" + opening + "\nlet run () = task { return 1 }\n"))
            Assert.NotEmpty(fast config path ("module M\n" + opening + "\nlet run () = backgroundTask { return 1 }\n")))

    [<Fact>]
    let ``declared runtime files are exempt even in a domain project`` () =
        withPolicy "domain" "[runtime]\nfiles = [\"Fast.fs\"]\n" [] (fun config path ->
            let composing = "module M\nlet run () : System.Threading.Tasks.Task = task {\n    do! System.Threading.Tasks.Task.Yield()\n    do! System.Threading.Tasks.Task.Yield() }\n"
            Assert.Empty(fast config path composing)
            Assert.Empty(full config path composing))

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``test roles run only with InTests all and then as bridges`` (all: bool) =
        withPolicy "test" "" (if all then [ "setting\tinTests\tall" ] else []) (fun config path ->
            let thin = "module M\nlet run () = task { return 1 }\n"
            let composing = "module M\nlet run () = task {\n    do! System.Threading.Tasks.Task.Yield()\n    do! System.Threading.Tasks.Task.Yield() }\n"
            Assert.Empty(fast config path thin)
            Assert.Equal(all, not (fast config path composing).IsEmpty)
            Assert.Empty(full config path thin)
            Assert.Equal(all, not (full config path composing).IsEmpty))

    [<Fact>]
    let ``Fast hints are a subset of Full and Full resolves aliases and abbreviations`` () =
        // (source, Full-only lines): every Fast line must also be a Full line.
        let corpus =
            [ "module M\nlet run () = task { return 1 }\n", Set.empty
              "module M\nlet t = task\nlet run () = t { return 1 }\n", set [ 3 ]
              "module M\ntype T = System.Threading.Tasks.Task<int>\nlet run () : T = System.Threading.Tasks.Task.FromResult 1\n", set [ 3 ]
              "module M\nlet run () = System.Threading.Tasks.Task.FromResult 1\n", set [ 2 ]
              "module M\ntype B() =\n    member _.Return x = x\n    member _.Delay f = f ()\nlet task = B()\nlet run () = task { return 1 }\n", Set.empty
              "module M\nlet private run () : System.Threading.Tasks.Task = System.Threading.Tasks.Task.CompletedTask\n", Set.empty
              "module M\nopen System.Threading.Tasks\ntype Api =\n    abstract Load: unit -> ValueTask<int>\n", Set.empty
              // A class `let` is private to the type: neither mode reports its signature.
              "module M\ntype Service() =\n    let load () : System.Threading.Tasks.Task<int> = System.Threading.Tasks.Task.FromResult 1\n    member _.Value = 1\n", Set.empty
              // An annotated signature and its task body are two findings; an inferred one is the body's only.
              "module M\nlet run () : System.Threading.Tasks.Task<int> = task { return 1 }\nlet inferred () =\n    task { return 2 }\n", Set.empty ]
        withPolicy "domain" "" [] (fun config path ->
            for source, fullOnly in corpus do
                let fastLines = lines (fast config path source)
                let fullLines = lines (full config path source)
                Assert.True(Set.isSubset fastLines fullLines, $"Fast {fastLines} not within Full {fullLines} for:\n{source}")
                Assert.Equal<Set<int>>(fullOnly, Set.difference fullLines fastLines))

    [<Fact>]
    let ``Full findings stay Info guides at checkpoints and notes acknowledge them`` () =
        withPolicy "domain" "" [ "setting\tcheckpoint\ttrue" ] (fun config path ->
            let source = "module M\nlet run () = task { return 1 }\n// axial-note AXG014: interop entry point for a .NET host\nlet other () = task { return 2 }\n"
            let findings = full config path source
            // One finding per binding: the inferred Task signature of `let run () = task { }` is the expression's.
            Assert.Equal(2, findings.Length)
            for finding in findings do Assert.Equal(FSharp.Analyzers.SDK.Severity.Info, finding.Severity)
            Assert.Equal(1, findings |> List.filter (fun m -> m.Message.Contains "Acknowledged: interop entry point") |> List.length)
            Assert.Contains(fast config path source, fun m -> m.Message.Contains "Acknowledged: interop entry point"))

    [<Theory>]
    [<InlineData("let x = flow { try return 1 with _ -> return reraise () }", true)>]
    [<InlineData("let x = flow { return nullArg \"x\" }", false)>]
    let ``Fast AXG003 shares Full's raise names`` (body: string) (expected: bool) =
        withPolicy "domain" "" [] (fun config path ->
            let source = "module M\n" + body + "\n"
            let found = FastSyntax.analyze config path (SourceText.ofString source) (parse path source) |> List.exists (fun (_, m) -> m.Code = "AXG003")
            Assert.Equal(expected, found)
            Assert.Equal(expected, RaiseInFlowAnalyzer.raiseNames.Contains(if expected then "reraise" else "nullArg")))

    let private fastRequest (dir: string) (config: string) (files: string list) =
        XElement(XName.Get "request", XAttribute(XName.Get "config", config), XAttribute(XName.Get "context", "test"),
                 XAttribute(XName.Get "cache", Path.Combine(dir, "cache")), XAttribute(XName.Get "langVersion", "preview"),
                 files |> List.map (fun file -> XElement(XName.Get "file", file))).ToString()

    let private realParse (path: string) (source: ISourceText) (options: FSharpParsingOptions) =
        checker.Value.ParseFile(path, source, options) |> Async.RunSynchronously |> _.ParseTree

    let private withDirectory action =
        let dir = Path.Combine(Path.GetTempPath(), "axial-fast-runner-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try action dir finally Directory.Delete(dir, true)

    [<Fact>]
    let ``skipped sources are recorded on every run in one process including cache hits`` () =
        withDirectory (fun dir ->
            let source = Path.Combine(dir, "App.fs")
            File.WriteAllText(source, "module App\nlet value = 1\n")
            let policy = Path.Combine(dir, "Axial.Guardrails.toml")
            File.WriteAllText(policy, "version = 2\n[default]\nrole = \"domain\"\n")
            let config = Path.Combine(dir, "rules")
            // An unusable project path makes the source impossible to classify: it is skipped, never silently.
            File.WriteAllLines(config, [ "guardrail\tAxial"; $"setting\tpolicyRoot\t{dir}"; $"setting\tpolicyFile\t{policy}"; "setting\tprojectPath\tbad\000.fsproj" ])
            let run () = FastRunner.runWith realParse FastSyntax.analyzeRule (fastRequest dir config [ source ]) |> XElement.Parse
            for attempt in 1 .. 3 do
                let result = run ()
                Assert.Empty(result.Elements(XName.Get "failure"))
                let skip = result.Elements(XName.Get "skip") |> Seq.exactlyOne
                Assert.Equal(source, skip.Attribute(XName.Get "file").Value)
                Assert.Contains("project path is unusable", skip.Attribute(XName.Get "reason").Value)
                let hits = result.Elements(XName.Get "coverage") |> Seq.map (fun e -> e.Attribute(XName.Get "cacheHit").Value) |> Seq.distinct |> Seq.exactlyOne
                Assert.Equal((if attempt = 1 then "false" else "true"), hits))

    [<Fact>]
    let ``the per-file Fast cache keeps only the current request's entries`` () =
        withDirectory (fun dir ->
            let a, b = Path.Combine(dir, "A.fs"), Path.Combine(dir, "B.fs")
            File.WriteAllText(a, "module A\nlet value = 1\n")
            File.WriteAllText(b, "module B\nlet value = 2\n")
            let config = Path.Combine(dir, "rules")
            File.WriteAllText(config, "guardrail\tAxial\n")
            let run files = FastRunner.runWith realParse FastSyntax.analyzeRule (fastRequest dir config files) |> ignore
            let entries () = Directory.GetFiles(Path.Combine(dir, "cache"), "*.xml")
            run [ a; b ]
            Assert.Equal(2, entries().Length)
            File.AppendAllText(a, "// edit\n")
            run [ a; b ]
            Assert.Equal(2, entries().Length)
            run [ a ]
            Assert.Equal(1, entries().Length)
            run [ a ]
            Assert.Equal(1, entries().Length))

    [<Theory>]
    [<InlineData("domain", "", "let run () = task { return 1 }", 1, 0, 1)>]
    [<InlineData("domain", "", "// task { }\nlet text = \"task { }\"\n(* backgroundTask { } *)\nlet x = 1", 0, 0, 0)>]
    [<InlineData("domain", "", "let run () : System.Threading.Tasks.Task<int> =\n    task { return 1 }", 2, 0, 2)>]
    [<InlineData("domain", "", "// axial-note AXG014: interop entry point for a .NET host\nlet run () = task { return 1 }", 1, 1, 1)>]
    [<InlineData("capabilities", "", "let run () = task { return! System.Threading.Tasks.Task.FromResult 1 }", 0, 0, 1)>]
    [<InlineData("domain", "[runtime]\nfiles = [\"Fast.fs\"]\n", "let run () = task { return 1 }", 0, 0, 1)>]
    [<InlineData("domain", "[files]\ncapabilities = [\"Fast.fs\"]\n", "let run () = task { return 1 }", 0, 0, 1)>]
    [<InlineData("test", "", "let run () = task {\n    do! System.Threading.Tasks.Task.Yield()\n    do! System.Threading.Tasks.Task.Yield() }", 0, 0, 1)>]
    let ``axial check's raw Task trend counts on AXG014's scale with roles tiers and overrides``
        (role: string) (extra: string) (body: string) (findings: int) (acknowledged: int) (sites: int) =
        withPolicy role extra [] (fun config path ->
            let source = "module M\n" + body + "\n"
            let count = Axial.Tool.RawTaskCount.countSource config path source
            Assert.Equal((findings, acknowledged, sites), (count.Findings, count.Acknowledged, count.Sites))
            // The trend's findings are exactly Fast's AXG014 findings.
            Assert.Equal(findings, (fast config path source).Length))

    [<Fact>]
    let ``fixing an AXG014 finding lowers the trend`` () =
        withPolicy "domain" "" [] (fun config path ->
            let before = Axial.Tool.RawTaskCount.countSource config path "module M\nlet run () = task { return 1 }\nlet other () = task { return 2 }\n"
            let after = Axial.Tool.RawTaskCount.countSource config path "module M\nopen Axial\nlet run () : Flow<unit, string, int> = flow { return 1 }\nlet other () = task { return 2 }\n"
            Assert.Equal(2, before.Findings)
            Assert.Equal(1, after.Findings))
