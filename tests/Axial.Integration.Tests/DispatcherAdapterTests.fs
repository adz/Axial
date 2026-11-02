namespace Axial.Tests

open System
open Axial
open Axial.Console
open Axial.FileSystem
open Axial.Process
open Axial.Configuration
open Axial.Tests.Shared
open Xunit

module DispatcherAdapterTests =
    let private scenario = """
open System.IO
open Axial.FileSystem
open Axial.Process
open Axial.Process.DSL
open Axial.Configuration

type AdapterEnv =
    { FS: IFileSystem; Process: IProcess }
    interface IHasFileSystem with member this.FileSystem = this.FS
    interface IHasProcess with member this.Process = this.Process

let gated make =
    let gate = TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
    let subscribed = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let monitor = signalAfterSubscription gate.Task subscribed
    let worker = Task.Run(fun () ->
        subscribed.Task.GetAwaiter().GetResult()
        gate.TrySetResult "payload" |> ignore)
    runOnDispatcher
        (fun () -> make gate.Task |> Flow.run ())
        (fun exit posts restored ->
            expectSuccess 42 exit
            check (posts = 0) "adapter posted to blocked dispatcher"
            check restored "adapter failed to restore context")
    worker.GetAwaiter().GetResult()
    monitor.GetAwaiter().GetResult()

gated (fun pending ->
    let fs = TestFileSystem(readAllTextAsync = fun _ -> Flow.fromTask (fun _ -> pending))
    let env = { FS = fs; Process = Process.live fs Axial.Console.Console.live }
    FileSystem.readAllTextAsync "fake.txt"
    |> Flow.localEnv (fun () -> env) |> Flow.mapError string
    |> Flow.map (fun text -> check (text = "payload") "fake read changed output"; 42))

gated (fun pending ->
    Flow.fromAsync (async {
        let nested = Flow.fromAsync (async { return! Async.AwaitTask pending })
        let! exit = nested.ToAsync(())
        match exit with
        | Exit.Success text -> return text
        | other -> return failwithf "nested async: %A" other
    }) |> Flow.map (fun text -> check (text = "payload") "nested async changed output"; 42))

// Acknowledging entry to the blocking delegate proves it is running, not that its resulting Task is subscribed.
// The subsequent pending fromTask forces an incomplete await independently of how quickly Task.Run schedules.
gated (fun pending ->
    let entered = new ManualResetEventSlim(false)
    let release = new ManualResetEventSlim(false)
    let worker = Task.Run(fun () ->
        check (entered.Wait(TimeSpan.FromSeconds 5.)) "blocking reader did not start"
        release.Set())
    let read () =
        entered.Set()
        check (release.Wait(TimeSpan.FromSeconds 5.)) "blocking reader was not released"
        worker.GetAwaiter().GetResult()
        entered.Dispose()
        release.Dispose()
        Ok "cold"
    Flow.delay (fun () -> Flow.fromResult (read ()))
    |> Flow.runOn RunOn.DedicatedThread
    |> Flow.bind (fun _ -> Flow.fromTask (fun _ -> pending))
    |> Flow.map (fun text -> check (text = "payload") "blocking recipe changed output"; 42))

let path = Path.GetTempFileName()
try
    File.WriteAllText(path, "live payload")
    let env = { FS = FileSystem.live; Process = Process.live FileSystem.live Axial.Console.Console.live }
    direct (fun () ->
        FileSystem.readAllTextAsync path |> Flow.localEnv (fun () -> env) |> Flow.mapError string
        |> Flow.map (fun text -> check (text = "live payload") "live read changed output"; 42)
        |> Flow.run ()) (expectSuccess 42)
    if not (OperatingSystem.IsWindows()) then
        direct (fun () ->
            shText "printf native" |> toFlow |> Flow.localEnv (fun () -> env) |> Flow.mapError string
            |> Flow.map (fun result -> check (result.StdOut = "native") "native output missing"; 42)
            |> Flow.run ()) (expectSuccess 42)
    let expectedPath = Environment.GetEnvironmentVariable "PATH"
    check (not (String.IsNullOrEmpty expectedPath)) "child requires an existing PATH; no environment mutation is used"
    direct (fun () ->
        Config.loadData [ ConfigSource.environment None ] |> Flow.mapError string
        |> Flow.bind (fun config ->
            Config.required "PATH"
            |> Flow.localEnv (fun () -> { new IHasConfig with member _.Config = config })
            |> Flow.mapError string)
        |> Flow.map (fun actual -> check (actual = expectedPath) "environment source lost PATH"; 42)
        |> Flow.run ()) (expectSuccess 42)
finally
    File.Delete path

// Exercise the WebSocket custom-connect synchronous bridge without opening a native socket.
let connected = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
let connectGate = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
let connectionSubscription = signalAfterSubscription connectGate.Task connected
let connectionWorker = Task.Run(fun () ->
    connected.Task.GetAwaiter().GetResult()
    connectGate.TrySetResult() |> ignore)
let options =
    Axial.WebSocket.WebSocket.connect "ws://dispatcher.test/"
    |> Axial.WebSocket.WebSocket.connectWith (fun _ ->
        Flow.fromTask (fun _ -> connectGate.Task)
        |> Flow.bind (fun () -> Flow.fail (Axial.WebSocket.WebSocketError.ConnectFailed "controlled")))
let socketEnv = { new Axial.WebSocket.IHasWebSocket with member _.WebSocket = Axial.WebSocket.WebSocket.live() }
runOnDispatcher
    (fun () -> Axial.WebSocket.WebSocket.openSession options |> Flow.run socketEnv)
    (fun exit posts restored ->
        match exit with
        | Exit.Failure(Cause.Fail(Axial.WebSocket.WebSocketError.ConnectFailed _)) -> ()
        | other -> fail (sprintf "unexpected WebSocket channel: %A" other)
        check (posts = 0) "WebSocket bridge posted to dispatcher"
        check restored "WebSocket bridge lost caller context")
connectionWorker.GetAwaiter().GetResult()
connectionSubscription.GetAwaiter().GetResult()

// Unlike the blocking runners, StartAsTask must retain the caller's dispatcher context.
let context = QueueContext()
let gate = TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)
SynchronizationContext.SetSynchronizationContext context
try
    let source =
        Flow.fromTask (fun _ -> gate.Task)
        |> Flow.map (fun value ->
            check (Object.ReferenceEquals(SynchronizationContext.Current, context)) "async runner lost caller context"
            value)
    let running = source.StartAsTask(())
    check (not running.IsCompleted) "async runner did not suspend"
    check (hasSubscription gate.Task) "async runner did not subscribe"
    gate.SetResult 42
    let deadline = DateTime.UtcNow.AddSeconds 5.
    while not running.IsCompleted && DateTime.UtcNow < deadline do
        context.PumpOne(TimeSpan.FromMilliseconds 20.) |> ignore
    check running.IsCompleted "async runner failed to resume while owner pumped"
    expectSuccess 42 running.Result
    check (context.Posted > 0) "async runner did not preserve dispatcher scheduling"
finally
    SynchronizationContext.SetSynchronizationContext null
printfn "CROSS ADAPTERS DONE"
"""

    [<Fact>]
    let ``bounded dispatcher child covers filesystem process async environment and blocking adapters`` () =
        let assemblies =
            [ typeof<Flow<unit, string, int>>.Assembly.Location
              typeof<IFileSystem>.Assembly.Location
              typeof<IProcess>.Assembly.Location
              typeof<IConsole>.Assembly.Location
              typeof<Axial.WebSocket.IWebSocket>.Assembly.Location
              typeof<IConfig>.Assembly.Location
              // ConfigSource's public surface uses Reified Data/Schema; resolve its runtime dependencies next to it.
              IO.Path.Combine(IO.Path.GetDirectoryName(typeof<IConfig>.Assembly.Location), "Reified.Data.dll")
              IO.Path.Combine(IO.Path.GetDirectoryName(typeof<IConfig>.Assembly.Location), "Reified.Schema.dll") ]
        let code, output = DispatcherChild.run assemblies scenario (TimeSpan.FromSeconds 45.)
        Assert.True((code = 0), $"child exited {code}:{Environment.NewLine}{output}")
        Assert.Contains("CROSS ADAPTERS DONE", output)
