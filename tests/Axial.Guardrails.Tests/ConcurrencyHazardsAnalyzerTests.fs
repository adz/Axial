namespace Axial.Guardrails.Tests

open System
open System.Reflection
open FSharp.Analyzers.SDK
open Axial.Guardrails
open Xunit

/// Exercise the actual CLI plugin entry points, so an implemented but unregistered rule still fails.
module ConcurrencyHazardsAnalyzerTests =
    let runContext code ctx =
        task {
            let plugins =
                typeof<GuardrailConfig.Config>.Assembly.GetTypes()
                |> Array.collect (fun t -> t.GetMethods(BindingFlags.Public ||| BindingFlags.Static))
                |> Array.filter (fun methodInfo ->
                    methodInfo.GetCustomAttributes(false)
                    |> Array.exists (fun attribute -> attribute.GetType().Name = "CliAnalyzerAttribute"))
            Assert.NotEmpty plugins
            let mutable messages = []
            for plugin in plugins do
                let analysis = plugin.Invoke(null, [| box ctx |]) :?> Async<Message list>
                let! found = analysis |> Async.StartAsTask
                messages <- messages @ found
            return messages |> List.filter (fun m -> m.Code = code)
        }

    let private findings code body =
        task {
            let source = "module M\nopen System\nopen System.Threading\nopen System.Threading.Tasks\nopen Axial\n" + body + "\n"
            let! ctx = AnalyzerTestContext.createWithAxial true source
            let errors =
                Array.append ctx.ParseFileResults.Diagnostics ctx.CheckFileResults.Diagnostics
                |> Array.filter (fun diagnostic -> diagnostic.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error)
            Assert.True(Array.isEmpty errors, sprintf "Invalid analyzer fixture: %A" errors)
            return! runContext code ctx
        }

    [<Theory>]
    [<InlineData("let x = TaskCompletionSource<int>()")>]
    [<InlineData("let x = new Task(fun () -> ())")>]
    [<InlineData("let x = new Task<int>(fun () -> 1)")>]
    [<InlineData("let x = new CancellationTokenSource()")>]
    [<InlineData("let x = Task.Run(fun () -> 1)")>]
    [<InlineData("let x = Task.Factory.StartNew(fun () -> 1)")>]
    [<InlineData("let x = Task<int>.Factory.StartNew(fun () -> 1)")>]
    [<InlineData("let x = Task.CompletedTask.ContinueWith(fun (_: Task) -> ())")>]
    [<InlineData("let x = new Thread(ThreadStart(fun () -> ()))")>]
    [<InlineData("let x = ThreadPool.QueueUserWorkItem(WaitCallback(fun _ -> ()))")>]
    let ``AXG009 reports raw concurrency primitives`` body =
        task {
            let! messages = findings "AXG009" body
            Assert.NotEmpty messages
            Assert.All(messages, fun m -> Assert.Equal(Severity.Error, m.Severity))
        }

    [<Theory>]
    [<InlineData("let f () = try failwith \"bad\" with _ -> ()")>]
    [<InlineData("let f () = try failwith \"bad\" with ex -> ()")>]
    [<InlineData("let f () = try failwith \"bad\" with :? exn -> ()")>]
    [<InlineData("let f () = try failwith \"bad\" with :? exn as error -> ()")>]
    [<InlineData("let f () = try failwith \"bad\" with _ -> 42")>]
    let ``AXG010 reports swallowed exceptions including fallback values`` body =
        task {
            let! messages = findings "AXG010" body
            Assert.Single(messages) |> ignore
        }

    [<Theory>]
    [<InlineData("let f () = try failwith \"bad\" with _ -> reraise()")>]
    [<InlineData("let f () = try failwith \"bad\" with ex -> raise ex")>]
    [<InlineData("let f () = try failwith \"bad\" with ex -> Error ex")>]
    [<InlineData("let f () = try failwith \"bad\" with ex -> Some ex")>]
    [<InlineData("let f () = try failwith \"bad\" with :? InvalidOperationException -> ()")>]
    [<InlineData("let f () = try failwith \"bad\" with :? OperationCanceledException as error -> ()")>]
    [<InlineData("type Failure = System.InvalidOperationException\nlet f () = try failwith \"bad\" with :? Failure -> ()")>]
    let ``AXG010 accepts propagation and specific exception handlers`` body =
        task {
            let! messages = findings "AXG010" body
            Assert.Empty messages
        }

    [<Theory>]
    [<InlineData("let Log () = ()\nlet f () = try failwith \"bad\" with _ -> Log()")>]
    [<InlineData("type Failure = exn\nlet f () = try failwith \"bad\" with :? Failure -> ()")>]
    [<InlineData("let f () = try failwith \"bad\" with :? System.Exception as error -> ()")>]
    [<InlineData("let f () = try failwith \"bad\" with (:? InvalidOperationException | _) -> ()")>]
    let ``AXG010 catch alls cannot hide behind type aliases or reporting names`` body =
        task {
            let! messages = findings "AXG010" body
            Assert.Single(messages) |> ignore
        }

    [<Fact>]
    let ``AXG011 reports CancellationToken None`` () =
        task {
            let! messages = findings "AXG011" "let token = CancellationToken.None"
            let message = Assert.Single messages
            Assert.Equal(Severity.Error, message.Severity)
        }

    [<Theory>]
    [<InlineData("let f () = let _ = Task.FromResult 1 in ()")>]
    [<InlineData("let f () = let _ = ValueTask<int>(1) in ()")>]
    [<InlineData("let f (fiber: Fiber<string, int>) = let _ = fiber in ()")>]
    [<InlineData("let f () = Task.FromResult 1 |> ignore")>]
    [<InlineData("let f () = ignore (Task.FromResult 1)")>]
    [<InlineData("let f () = (Task.FromResult 1 :> Task) |> ignore")>]
    [<InlineData("let f () = ValueTask<int>(1) |> ignore")>]
    [<InlineData("let f () =\n    Task.FromResult 1\n    ()")>]
    let ``AXG012 reports discarded concurrent work`` body =
        task {
            let! messages = findings "AXG012" body
            Assert.NotEmpty messages
        }

    [<Theory>]
    [<InlineData("let f () =\n    let mutable recorded = None\n    try failwith \"bad\" with ex -> recorded <- Some ex")>]
    [<InlineData("let f () =\n    let recorded = ref None\n    try failwith \"bad\" with ex -> recorded.Value <- Some ex")>]
    [<InlineData("let f () = try failwith \"bad\" with ex when (ex :? OperationCanceledException) -> Error () | ex -> Error ()")>]
    [<InlineData("let isOwned (error: exn) = error :? OperationCanceledException\nlet f () = try failwith \"bad\" with ex when isOwned ex -> Error () | ex -> Error ()")>]
    [<InlineData("let f () = try failwith \"bad\" with ex -> System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw()")>]
    [<InlineData("type Failure = { Message: string }\nlet f () = try failwith \"bad\" with ex -> Error { Message = ex.Message }")>]
    let ``AXG010 accepts exception recording assignments and guarded classification`` body =
        task {
            // The second handler still discards its unclassified exception.
            let! messages = findings "AXG010" body
            if body.Contains("when") then Assert.Single(messages) |> ignore
            else Assert.Empty messages
        }

    [<Theory>]
    [<InlineData("let f () = Async.Start(async { return () })")>]
    [<InlineData("let f () = Async.StartImmediate(async { return () })")>]
    [<InlineData("let f () = Flow.fork (Flow.succeed 1) |> ignore")>]
    [<InlineData("let f () = let _ = Flow.fork (Flow.succeed 1) in ()")>]
    [<InlineData("let fork x = x\nlet f () = fork 42 |> ignore")>]
    [<InlineData("let ignore (pending: Task<int>) = pending\nlet f () = Task.FromResult 1 |> ignore")>]
    [<InlineData("let f () = Task.FromResult 1 |> (fun pending -> ignore 1; pending)")>]
    [<InlineData("let f () = [| Task.FromResult 1 :> Task; Task.CompletedTask |]")>]
    [<InlineData("let f () = [ Task.FromResult 1; Task.FromResult 2 ]")>]
    [<InlineData("let f () = task { let! completed = Task.WhenAny(Task.CompletedTask, Task.CompletedTask) in return () }")>]
    [<InlineData("let f () = task { let! _ = Task.WhenAny(Task.CompletedTask, Task.CompletedTask) in return () }")>]
    [<InlineData("let f (pending: Task<Task>) = task { let! _ = pending in return () }")>]
    [<InlineData("let f (pending: Task<Task>) = task {\n    let! _ = Task.WhenAny(pending :> Task, Task.CompletedTask)\n    let! _ = pending\n    return ()\n}")>]
    let ``AXG012 leaves cold descriptions and awaited work alone`` body =
        task {
            let! messages = findings "AXG012" body
            Assert.Empty messages
        }

    [<Fact>]
    let ``AXG012 uses types for aliased task factories but leaves owned tasks alone`` () =
        task {
            let! discarded = findings "AXG012" "let produce () = Task.FromResult 1\nlet f () = produce() |> ignore"
            Assert.Single(discarded) |> ignore
            let! owned = findings "AXG012" "let produce () = Task.FromResult 1\nlet f () = produce()\nlet g () = 42 |> ignore"
            Assert.Empty owned
        }

    [<Theory>]
    [<InlineData("AXG009", "let value = TaskCompletionSource<int>()")>]
    [<InlineData("AXG010", "let value () = try failwith \"bad\" with _ -> ()")>]
    [<InlineData("AXG011", "let value = CancellationToken.None")>]
    [<InlineData("AXG012", "let value () = Task.FromResult 1 |> ignore")>]
    let ``legacy allowances no longer hide tiered findings`` code (site: string) =
        task {
            let other = site.Replace("value", "other")
            let source = $"// axial-allow-{code}: deliberate test witness of an OS bridge\n{site}\n{other}"
            let! messages = findings code source
            Assert.Equal(2, messages.Length)
        }

    [<Theory>]
    [<InlineData("AXG009")>]
    [<InlineData("AXG010")>]
    [<InlineData("AXG011")>]
    [<InlineData("AXG012")>]
    let ``AXG002 rejects new rule suppressions without a reason`` (code: string) =
        task {
            let! messages = findings "AXG002" $"// axial-allow-{code}:\nlet value = 1"
            let message = Assert.Single messages
            Assert.Contains("reason", message.Message, StringComparison.OrdinalIgnoreCase)
        }

    [<Theory>]
    [<InlineData("AXG009", "Error")>]
    [<InlineData("AXG010", "Info")>]
    [<InlineData("AXG011", "Error")>]
    [<InlineData("AXG012", "Error")>]
    [<InlineData("AXG013", "Error")>]
    let ``baselines cannot change tiered severity`` code expected =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true "module M\nlet value = 1"
            let severity = if expected = "Info" then Severity.Info elif expected = "Warning" then Severity.Warning else Severity.Error
            let message: Message =
                { Type = "witness"; Code = code; Message = "witness"; Severity = severity
                  Range = ctx.ParseFileResults.ParseTree.Range; Fixes = [] }
            let entry: GuardrailConfig.BaselineEntry =
                { RuleId = code; File = System.IO.Path.GetFileName ctx.FileName
                  Fingerprint = GuardrailConfig.fingerprint ctx.SourceText message; Count = 1 }
            let config =
                { GuardrailConfig.defaults with BaselineRoot = System.IO.Path.GetDirectoryName ctx.FileName; Baseline = [ entry ] }
            let actual = GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx [ message ] |> Assert.Single
            Assert.Equal(severity, actual.Severity)
        }

[<CollectionDefinition("Concurrency policy environment", DisableParallelization = true)>]
type ConcurrencyPolicyEnvironmentCollection() = class end

[<Collection("Concurrency policy environment")>]
type ConcurrencyHazardsPolicyTests() =
    let runWithSettings (settings: string list) (role: string) runtime boundary project source code =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true source
            let root = System.IO.Path.GetDirectoryName ctx.FileName
            System.IO.Directory.CreateDirectory root |> ignore
            let policyPath = System.IO.Path.Combine(root, "Axial.Guardrails.toml")
            let configPath = System.IO.Path.Combine(root, "config.tsv")
            let runtimeTable = if runtime then "\n[runtime]\nfiles = [\"Fixture.fs\"]\n" else ""
            let boundaryTable = if boundary then "\n[boundaries]\nfiles = [\"Fixture.fs\"]\n" else ""
            System.IO.File.WriteAllText(ctx.FileName, ctx.SourceText.ToString())
            let projectPath = System.IO.Path.Combine(root, project)
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName projectPath) |> ignore
            System.IO.File.WriteAllText(projectPath, "<Project />")
            System.IO.File.WriteAllText(policyPath, $"version = 2\n[default]\nrole = \"{role}\"\n" + runtimeTable + boundaryTable)
            System.IO.File.WriteAllLines(configPath,
                [| "guardrail\tAxial"
                   "setting\tpolicyRoot\t" + root
                   "setting\tpolicyFile\t" + policyPath
                   "setting\tprojectPath\t" + projectPath
                   yield! settings |])
            let previous = Environment.GetEnvironmentVariable GuardrailConfig.EnvironmentVariable
            try
                Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, configPath)
                return! ConcurrencyHazardsAnalyzerTests.runContext code ctx
            finally
                Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, previous)
                System.IO.Directory.Delete(root, true)
        }

    let runWith role runtime boundary project source code = runWithSettings [] role runtime boundary project source code

    let source = "module M\nopen System.Threading\nopen System.Threading.Tasks\nlet source = TaskCompletionSource<int>()\nlet token = CancellationToken.None\nlet swallowed () = try failwith \"bad\" with _ -> ()\nlet discarded () = Task.FromResult 1 |> ignore\n"
    let run role runtime boundary code = runWith role runtime boundary "Fixture.fsproj" source code

    [<Theory>]
    [<InlineData("domain")>]
    [<InlineData("services")>]
    [<InlineData("host")>]
    member _.``domain services and ordinary hosts report concurrency hazards`` role =
        task {
            for code in [ "AXG009"; "AXG010"; "AXG011"; "AXG012" ] do
                let! messages = run role false false code
                Assert.NotEmpty messages
        }

    [<Theory>]
    [<InlineData("AXG009")>]
    [<InlineData("AXG010")>]
    [<InlineData("AXG011")>]
    [<InlineData("AXG012")>]
    member _.``test role is exempt`` code =
        task {
            let! messages = run "test" false false code
            Assert.Empty messages
        }

    [<Fact>]
    member _.``runtime list exempts raw primitives and discarded work but not uncancellable work`` () =
        task {
            let! raw = run "services" true false "AXG009"
            Assert.Empty raw
            let! discarded = run "services" true false "AXG012"
            Assert.Empty discarded
            let! discardedOutside = run "services" false false "AXG012"
            Assert.NotEmpty discardedOutside
            let! uncancellable = run "services" true false "AXG011"
            Assert.Single(uncancellable) |> ignore
        }

    [<Fact>]
    member _.``capability role owns raw task bridges but not discarded work`` () =
        task {
            let! raw = run "capabilities" false false "AXG009"
            Assert.Empty raw
            for code in [ "AXG010"; "AXG011"; "AXG012" ] do
                let! messages = run "capabilities" false false code
                Assert.NotEmpty messages
        }

    [<Theory>]
    [<InlineData("src/Axial.Guardrails/Axial.Guardrails.fsproj")>]
    [<InlineData("src/Axial.Tool/Axial.Tool.fsproj")>]
    [<InlineData("src/Axial.Tool.TestsSupport/Axial.Tool.TestsSupport.fsproj")>]
    member _.``host tooling projects are exempt from concurrency rules`` project =
        task {
            for code in [ "AXG009"; "AXG010"; "AXG011"; "AXG012" ] do
                let! messages = runWith "host" false false project source code
                Assert.Empty messages
        }

    [<Theory>]
    [<InlineData("AXG009")>]
    [<InlineData("AXG012")>]
    member _.``benchmark projects permit measured raw concurrency baselines`` code =
        task {
            let! messages = runWith "host" false false "benchmarks/Axial.Benchmarks/Axial.Benchmarks.fsproj" source code
            Assert.Empty messages
            let! outside = runWith "host" false false "samples/benchmarks/Axial.Benchmarks.fsproj" source code
            Assert.NotEmpty outside
            let! service = runWith "services" false false "benchmarks/Axial.Benchmarks/Axial.Benchmarks.fsproj" source code
            Assert.NotEmpty service
        }

    [<Theory>]
    [<InlineData("AXG010")>]
    [<InlineData("AXG011")>]
    member _.``benchmark projects retain exception and cancellation checks`` code =
        task {
            let! messages = runWith "host" false false "benchmarks/Axial.Benchmarks/Axial.Benchmarks.fsproj" source code
            Assert.NotEmpty messages
        }

    [<Theory>]
    [<InlineData("services", "src/Axial.Tool/Axial.Tool.fsproj")>]
    [<InlineData("host", "samples/Axial.Tool/Axial.Tool.fsproj")>]
    [<InlineData("host", "src/Axial.Guardrails.Consumer/Axial.Guardrails.Consumer.fsproj")>]
    member _.``tooling exemption requires both host role and tooling project path`` role project =
        task {
            for code in [ "AXG009"; "AXG010"; "AXG011"; "AXG012" ] do
                let! messages = runWith role false false project source code
                Assert.NotEmpty messages
        }

    [<Theory>]
    [<InlineData("let mutable n = 0\nlet x = Interlocked.Increment(&n)")>]
    [<InlineData("let x = lock (obj()) (fun () -> 1)")>]
    [<InlineData("let x = Monitor.Enter(obj())")>]
    member _.``lock, Monitor and Interlocked are not reported in any role`` body =
        task {
            for role in [ "domain"; "services"; "capabilities"; "host"; "test" ] do
                let input = "module M\nopen System.Threading\n" + body
                let! messages = runWith role false false "Fixture.fsproj" input "AXG009"
                Assert.Empty messages
        }

    [<Theory>]
    [<InlineData(false, "", "Warning")>]
    [<InlineData(true, "", "Error")>]
    [<InlineData(false, "// axial-note AXG010: foreign observer is best effort\n", "Info")>]
    [<InlineData(true, "// axial-note AXG010: foreign observer is best effort\n", "Info")>]
    member _.``guide findings remain visible and checkpoints reject only unacknowledged sites`` (checkpoint: bool) (note: string) (expected: string) =
        task {
            let input = "module M\n" + note + "let f () = try failwith \"bad\" with _ -> ()"
            let! messages = runWithSettings [ $"setting\tcheckpoint\t{checkpoint.ToString().ToLowerInvariant()}" ] "services" false false "Fixture.fsproj" input "AXG010"
            let message = Assert.Single messages
            Assert.Equal(expected, string message.Severity)
            Assert.Contains("Consider", message.Message)
            if note <> "" then Assert.Contains("foreign observer is best effort", message.Message)
        }

    [<Fact>]
    member _.``only the Cancellation never definition owns the uncancellable token`` () =
        task {
            let input = "module Axial.Platform.Cancellation\nopen System.Threading\nlet never: CancellationToken = CancellationToken.None\nlet other = CancellationToken.None"
            let! messages = runWith "services" false false "Fixture.fsproj" input "AXG011"
            let message = Assert.Single messages
            Assert.Equal(4, message.Range.StartLine)
            Assert.Equal(Severity.Error, message.Severity)
        }

    [<Theory>]
    [<InlineData("AXG009", "let x = System.Threading.Tasks.TaskCompletionSource<int>()", "Error")>]
    [<InlineData("AXG012", "let f () = System.Threading.Tasks.Task.FromResult 1 |> ignore", "Error")>]
    [<InlineData("AXG011", "let x = System.Threading.CancellationToken.None", "Error")>]
    member _.``block and deferred block findings cannot be noted or checkpoint promoted`` (code: string) (body: string) (expected: string) =
        task {
            for checkpoint in [ "false"; "true" ] do
                let input = $"module M\n// axial-note {code}: intentional witness\n{body}"
                let! messages = runWithSettings [ $"setting\tcheckpoint\t{checkpoint}" ] "services" false false "Fixture.fsproj" input code
                Assert.Equal(expected, string (Assert.Single messages).Severity)
                let! invalid = runWithSettings [ $"setting\tcheckpoint\t{checkpoint}" ] "services" false false "Fixture.fsproj" input "AXG002"
                Assert.Equal((if checkpoint = "true" then Severity.Error else Severity.Warning), (Assert.Single invalid).Severity)
        }

    [<Theory>]
    [<InlineData("// axial-note AXG010:")>]
    [<InlineData("// axial-note AXG999: unknown")>]
    [<InlineData("// axial-note AXG001: not a guide")>]
    [<InlineData("// axial-note: malformed")>]
    [<InlineData("// axial-note AXG010: stale\nlet unrelated = 1")>]
    member _.``invalid notes warn and fail checkpoints`` (note: string) =
        task {
            for checkpoint in [ "false"; "true" ] do
                let input = $"module M\n{note}\nlet value = 1"
                let! messages = runWithSettings [ $"setting\tcheckpoint\t{checkpoint}" ] "services" false false "Fixture.fsproj" input "AXG002"
                Assert.Equal((if checkpoint = "true" then Severity.Error else Severity.Warning), (Assert.Single messages).Severity)
        }

    [<Theory>]
    [<InlineData("let f () = task { do! System.Threading.Tasks.Task.Delay 10 } // axial-note AXG013: simulates latency", true)>]
    [<InlineData("// axial-note AXG013: simulates latency\nlet f () = task { do! System.Threading.Tasks.Task.Delay 10 }", true)>]
    [<InlineData("let f () = System.Threading.Thread.Sleep 10 // axial-note AXG013: blocking is not a guide", false)>]
    [<InlineData("// axial-note AXG013: too far away\nlet unrelated = 1\nlet f () = task { do! System.Threading.Tasks.Task.Delay 10 }", false)>]
    member _.``AXG002 distinguishes live delay notes from blocking and misplaced notes`` (body: string) valid =
        task {
            let input = "module M\n" + body
            let! messages = runWith "test" false false "Fixture.fsproj" input "AXG002"
            if valid then Assert.Empty messages
            else Assert.Equal(Severity.Warning, (Assert.Single messages).Severity)
            let! findings = runWith "test" false false "Fixture.fsproj" input "AXG013"
            Assert.Single findings |> ignore
            if valid then Assert.Equal(Severity.Info, findings.Head.Severity)
        }

    [<Fact>]
    member _.``notes must cover live scoped findings`` () =
        task {
            let input = "module M\n// axial-note AXG010: irrelevant in test role\nlet f () = try failwith \"bad\" with _ -> ()"
            let! invalid = runWith "test" false false "Fixture.fsproj" input "AXG002"
            Assert.Equal(Severity.Warning, (Assert.Single invalid).Severity)
            let! valid = runWith "services" false false "Fixture.fsproj" input "AXG002"
            Assert.Empty valid
        }

    [<Theory>]
    [<InlineData("let text = \"// axial-note AXG010: not a comment\"")>]
    [<InlineData("(* // axial-note AXG010: block comment *)")>]
    [<InlineData("/// axial-note AXG010: documentation")>]
    member _.``quoted notes never acknowledge findings or produce invalid note diagnostics`` prefix =
        task {
            let input = "module M\n" + prefix + "\nlet f () = try failwith \"bad\" with _ -> ()"
            let! findings = runWith "services" false false "Fixture.fsproj" input "AXG010"
            Assert.Equal(Severity.Warning, (Assert.Single findings).Severity)
            let! invalid = runWith "services" false false "Fixture.fsproj" input "AXG002"
            Assert.Empty invalid
        }

    [<Fact>]
    member _.``adapter boundary is not a raw concurrency exemption`` () =
        task {
            let! messages = run "services" false true "AXG009"
            Assert.Single(messages) |> ignore
        }
