namespace Axial.ElmishFlows.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.ElmishFlows
open Xunit

[<CollectionDefinition("Console error", DisableParallelization = true)>]
type ConsoleErrorCollection() = class end

[<Collection("Console error")>]
module RuntimeTests =
    let private liveClock () =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = TimeSpan.FromTicks(Environment.TickCount64)
            member _.Sleep(delay, token) = Task.Delay(delay, token) }

    let private options post onDefect =
        { Post = post; OnDefect = onDefect; HistoryCapacity = 32; Clock = Some(liveClock ()); LogSink = None }

    type CacheEnvironment = { Cache: Cache<int, Never, string> }

    [<Fact>]
    let ``startLayer installs its explicit clock during layer provisioning`` () =
        let instant = DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)
        let clock = ManualClock instant
        let configuration = { options (fun callback -> callback ()) (fun defect -> failwith defect.Rendered) with Clock = Some clock }
        let layer: Layer<unit, string, DateTimeOffset> = Layer.fromFlow Axial.PlatformService.Clock.now
        let provisioned = ElmishRuntime.startLayer configuration layer () |> Async.RunSynchronously
        use runtime = match provisioned with Ok value -> value | Error error -> failwith error
        Assert.Equal(instant, runtime.Environment)
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.))

    [<Fact>]
    let ``pump installs the configured log sink`` () =
        let sink = Axial.PlatformService.Log.memory ()
        let configuration =
            { options (fun callback -> callback ()) (fun defect -> failwith defect.Rendered) with
                LogSink = Some(sink :> ILogSink) }
        use runtime = ElmishRuntime.start configuration ()
        let command = Cmd.OfFlow.perform "log" runtime (flow { do! Axial.PlatformService.Log.info "pump-entry" }) id
        for start in command do start ignore
        Assert.True(SpinWait.SpinUntil((fun () -> sink.Entries |> Seq.exists (fun entry -> entry.Message = "pump-entry")), 5000))

    [<Fact>]
    let ``startLayer installs the configured log sink during provisioning`` () =
        let sink = Axial.PlatformService.Log.memory ()
        let layer : Layer<unit, string, unit> =
            Layer.fromFlow (flow {
                do! Axial.PlatformService.Log.info "layer-entry"
                return ()
            })
        let configuration =
            { options (fun callback -> callback ()) (fun defect -> failwith defect.Rendered) with
                LogSink = Some(sink :> ILogSink) }
        let result = ElmishRuntime.startLayer configuration layer () |> Async.RunSynchronously
        use _runtime = match result with Ok runtime -> runtime | Error error -> failwith error
        Assert.True(sink.Entries |> Seq.exists (fun entry -> entry.Message = "layer-entry"))

    [<Fact>]
    let ``None keeps the default console log sink`` () =
        let original = Console.Error
        use writer = new System.IO.StringWriter()
        Console.SetError writer
        try
            use runtime = ElmishRuntime.start (options (fun callback -> callback ()) (fun defect -> failwith defect.Rendered)) ()
            let command =
                Cmd.OfFlow.perform "log" runtime (flow { do! Axial.PlatformService.Log.info "default-entry" }) id
            for start in command do start ignore
            Assert.True(SpinWait.SpinUntil((fun () -> writer.ToString().Contains "default-entry"), 5000))
        finally
            Console.SetError original

    [<Fact>]
    let ``startLayer provisions a cache and release flow until stop`` () =
        let messages = ConcurrentQueue<string>()
        let released = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let cacheLayer : Layer<unit, string, CacheEnvironment> =
            Layer.fromFlow (flow {
                let! cache =
                    Cache.make (fun (key: int) ->
                        Flow.succeed $"value-{key}" : Flow<unit, Never, string>)
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ ->
                    released.TrySetResult() |> ignore
                    Task.FromResult(())))
                return { Cache = cache }
            })
        let result =
            ElmishRuntime.startLayer
                (options (fun callback -> callback ()) (fun defect -> failwith defect.Rendered))
                cacheLayer
                ()
            |> Async.RunSynchronously
        use runtime =
            match result with
            | Ok runtime -> runtime
            | Error error -> failwith $"startLayer failed: {error}"
        let command =
            Cmd.OfFlow.perform "get" runtime (flow {
                let! cache = Flow.envWith (fun (env: CacheEnvironment) -> env.Cache)
                let! value = Cache.get 1 cache
                return value
            }) id
        for start in command do start messages.Enqueue
        Assert.True(SpinWait.SpinUntil((fun () -> messages.Count = 1), 5000))
        Assert.Equal("value-1", messages.ToArray().[0])
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.Equal(0, runtime.Registry.LiveFiberCount)
        Assert.True(released.Task.Wait(5000))

    [<Fact>]
    let ``startLayer returns Error and starts no runtime when provisioning fails`` () =
        let released = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let failing : Layer<unit, string, unit> =
            Layer.fromFlow (flow {
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ ->
                    released.TrySetResult() |> ignore
                    Task.FromResult(())))
                return! Flow.fail "provision failed"
            })
        let result =
            ElmishRuntime.startLayer
                (options (fun callback -> callback ()) (fun defect -> failwith defect.Rendered))
                failing
                ()
            |> Async.RunSynchronously
        match result with
        | Ok _ -> failwith "startLayer should have returned Error"
        | Error error -> Assert.Equal("provision failed", error)
        Assert.True(released.Task.Wait(5000))
