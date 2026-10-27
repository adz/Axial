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
            return! runContext code ctx
        }

    [<Theory>]
    [<InlineData("let x = TaskCompletionSource<int>()")>]
    [<InlineData("let mutable n = 0\nlet x = Interlocked.Increment(&n)")>]
    [<InlineData("let x = lock (obj()) (fun () -> 1)")>]
    [<InlineData("let x = Monitor.Enter(obj())")>]
    [<InlineData("let x = new CancellationTokenSource()")>]
    [<InlineData("let x = Task.Run(fun () -> 1)")>]
    [<InlineData("let x = Task.Factory.StartNew(fun () -> 1)")>]
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
    [<InlineData("let f () = try failwith \"bad\" with :? InvalidOperationException -> ()")>]
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
    [<InlineData("let Report () = ()\nlet f () = try failwith \"bad\" with _ -> Report()")>]
    [<InlineData("let notify () = ()\nlet f () = try failwith \"bad\" with _ -> notify()")>]
    [<InlineData("let Observer () = ()\nlet f () = try failwith \"bad\" with _ -> Observer()")>]
    [<InlineData("let Log () = ()\nlet f () = try failwith \"bad\" with _ -> Log()")>]
    [<InlineData("let Record () = ()\nlet f () = try failwith \"bad\" with _ -> Record()")>]
    let ``AXG010 accepts propagation recording and named reporting`` body =
        task {
            let! messages = findings "AXG010" body
            Assert.Empty messages
        }

    [<Fact>]
    let ``AXG011 reports CancellationToken None`` () =
        task {
            let! messages = findings "AXG011" "let token = CancellationToken.None"
            Assert.Single(messages) |> ignore
        }

    [<Theory>]
    [<InlineData("let f () = let _ = Task.FromResult 1 in ()")>]
    [<InlineData("let f () = let _ = ValueTask<int>(1) in ()")>]
    [<InlineData("let f (fiber: Fiber<string, int>) = let _ = fiber in ()")>]
    [<InlineData("let f () = Task.FromResult 1 |> ignore")>]
    [<InlineData("let f () = (Task.FromResult 1 :> Task) |> ignore")>]
    [<InlineData("let f () = ValueTask<int>(1) |> ignore")>]
    [<InlineData("let f () =\n    Task.FromResult 1\n    ()")>]
    [<InlineData("let f () = Async.Start(async { return () })")>]
    [<InlineData("let f () = Async.StartImmediate(async { return () })")>]
    let ``AXG012 reports discarded concurrent work`` body =
        task {
            let! messages = findings "AXG012" body
            Assert.NotEmpty messages
        }

    [<Theory>]
    [<InlineData("let f () =\n    let mutable recorded = None\n    try failwith \"bad\" with ex -> recorded <- Some ex")>]
    [<InlineData("let f () =\n    let recorded = ref None\n    try failwith \"bad\" with ex -> recorded.Value <- Some ex")>]
    [<InlineData("let f () = try failwith \"bad\" with ex when ex :? OperationCanceledException -> Error () | ex -> Error ()")>]
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
    [<InlineData("let f () = Flow.fork (Flow.succeed 1) |> ignore")>]
    [<InlineData("let f () = let _ = Flow.fork (Flow.succeed 1) in ()")>]
    [<InlineData("let fork x = x\nlet f () = fork 42 |> ignore")>]
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
    let ``a reason suppresses exactly the adjacent site`` code (site: string) =
        task {
            let other = site.Replace("value", "other")
            let source = $"// axial-allow-{code}: deliberate test witness of an OS bridge\n{site}\n{other}"
            let! messages = findings code source
            Assert.Single(messages) |> ignore
        }

    [<Theory>]
    [<InlineData("AXG009")>]
    [<InlineData("AXG010")>]
    [<InlineData("AXG011")>]
    [<InlineData("AXG012")>]
    let ``AXG002 rejects new rule suppressions without a reason`` code =
        task {
            let! messages = findings "AXG002" $"// axial-allow-{code}:\nlet value = 1"
            let message = Assert.Single messages
            Assert.Contains("reason", message.Message, StringComparison.OrdinalIgnoreCase)
        }

[<CollectionDefinition("Concurrency policy environment", DisableParallelization = true)>]
type ConcurrencyPolicyEnvironmentCollection() = class end

[<Collection("Concurrency policy environment")>]
type ConcurrencyHazardsPolicyTests() =
    let run role runtime boundary code =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true
                           "module M\nopen System.Threading\nopen System.Threading.Tasks\nlet source = TaskCompletionSource<int>()\nlet token = CancellationToken.None\nlet swallowed () = try failwith \"bad\" with _ -> ()\nlet discarded () = Task.FromResult 1 |> ignore\n"
            let root = System.IO.Path.GetDirectoryName ctx.FileName
            System.IO.Directory.CreateDirectory root |> ignore
            let policyPath = System.IO.Path.Combine(root, "Axial.Guardrails.toml")
            let configPath = System.IO.Path.Combine(root, "config.tsv")
            let runtimeTable = if runtime then "\n[runtime]\nfiles = [\"Fixture.fs\"]\n" else ""
            let boundaryTable = if boundary then "\n[boundaries]\nfiles = [\"Fixture.fs\"]\n" else ""
            System.IO.File.WriteAllText(ctx.FileName, ctx.SourceText.ToString())
            System.IO.File.WriteAllText(System.IO.Path.Combine(root, "Fixture.fsproj"), "<Project />")
            System.IO.File.WriteAllText(policyPath, $"version = 2\n[default]\nrole = \"{role}\"\n" + runtimeTable + boundaryTable)
            System.IO.File.WriteAllLines(configPath,
                [| "guardrail\tAxial"
                   "setting\tpolicyRoot\t" + root
                   "setting\tpolicyFile\t" + policyPath
                   "setting\tprojectPath\t" + System.IO.Path.Combine(root, "Fixture.fsproj") |])
            let previous = Environment.GetEnvironmentVariable GuardrailConfig.EnvironmentVariable
            try
                Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, configPath)
                return! ConcurrencyHazardsAnalyzerTests.runContext code ctx
            finally
                Environment.SetEnvironmentVariable(GuardrailConfig.EnvironmentVariable, previous)
                System.IO.Directory.Delete(root, true)
        }

    [<Theory>]
    [<InlineData("domain")>]
    [<InlineData("capabilities")>]
    [<InlineData("services")>]
    [<InlineData("host")>]
    member _.``all non-test roles report concurrency hazards`` role =
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
    member _.``runtime list exempts raw primitives but not uncancellable work`` () =
        task {
            let! raw = run "services" true false "AXG009"
            Assert.Empty raw
            let! uncancellable = run "services" true false "AXG011"
            Assert.Single(uncancellable) |> ignore
        }

    [<Fact>]
    member _.``adapter boundary is not a raw concurrency exemption`` () =
        task {
            let! messages = run "services" false true "AXG009"
            Assert.Single(messages) |> ignore
        }
