namespace Axial.Tests

open System
open Axial
open Axial.Tests.Shared
open Xunit

module SynchronousRunnerContextTests =
    let private scenario = """
suspended (fun flow -> Flow.run () flow) (expectSuccess 42)
suspended (fun flow -> flow.RunSynchronously(())) (expectSuccess 42)
suspended (fun flow -> Flow.runTestWith (TestPlatform.create ()) () flow) (expectSuccess 42)

direct (fun () -> Flow.fail "typed" |> Flow.run ()) (expectTyped "typed")
direct (fun () -> Flow.die (InvalidOperationException "boom") |> Flow.run ()) expectDefect
direct (fun () ->
    let cancellation = new CancellationTokenSource()
    cancellation.Cancel()
    (Flow.never : Flow<unit, string, int>).RunSynchronously((), cancellation.Token)) expectInterrupt
direct (fun () ->
    (Flow.fromTask (fun _ -> Task.FromException<int>(OperationCanceledException("foreign")))) |> Flow.run ()) expectDefect
direct (fun () ->
    (flow {
        do! Flow.scopeFinalizer (Flow.die (InvalidOperationException "cleanup"))
        return 1
    }) |> Flow.run ()) expectDefect
direct (fun () ->
    (flow {
        let! first = Flow.fromTask (fun _ -> Task.Run(fun () -> 1))
        let! second = Flow.fromTask (fun _ -> Task.Run(fun () -> first + 1))
        return second
    }) |> Flow.run ()) (expectSuccess 2)
direct (fun () ->
    (Flow.fromTask (fun _ -> Task.Run(fun () -> Thread.Sleep 20; 41))) |> Flow.run ()) (expectSuccess 41)

let scope = new Scope()
let subscribed = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
let gate = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
// Scope.Register is internal to the product assembly; the script is not a friend assembly, so reach it by reflection.
let register =
    typeof<Scope>.GetMethod("Register", System.Reflection.BindingFlags.Instance ||| System.Reflection.BindingFlags.NonPublic)
let observed = new ManualResetEventSlim(false)
let subscription = signalAfterSubscription gate.Task subscribed
let finalizer = FinalizerFunc((fun () ->
    returnPending subscribed observed gate.Task |> ignore), gate.Task)
register.Invoke(scope, [| box finalizer |]) |> ignore
let worker = Task.Run(fun () ->
    subscribed.Task.Wait()
    let registered = hasSubscription gate.Task
    observed.Set()
    check registered "Scope.Dispose gate released before subscription"
    gate.TrySetResult() |> ignore) :> Task
runOnDispatcher (fun () -> (scope :> IDisposable).Dispose(); true) (fun completed posts restored ->
    worker.GetAwaiter().GetResult()
    subscription.GetAwaiter().GetResult()
    check completed "Scope.Dispose did not complete"
    check (posts = 0) (sprintf "Scope.Dispose posted %d dispatcher continuations" posts)
    check restored "Scope.Dispose did not restore the original context")

printfn "ADAPTER TABLE DONE"
"""

    [<Fact>]
    let ``bounded child process covers the synchronous dispatcher channel table`` () =
        let assemblies = [ typeof<Flow<unit, string, int>>.Assembly.Location ]
        let code, output = DispatcherChild.run assemblies scenario (TimeSpan.FromSeconds 30.0)
        Assert.True((code = 0), $"child exited {code}:{Environment.NewLine}{output}")
        Assert.Contains("PRELUDE READY", output)
        Assert.Contains("ADAPTER TABLE DONE", output)
        Assert.Contains("SCENARIO DONE", output)
