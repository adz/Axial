namespace Axial.ElmishFlows.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.ElmishFlows
open Xunit

module ServiceTests =
    let private liveClock () =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = TimeSpan.FromTicks(Environment.TickCount64)
            member _.Sleep(delay, token) = Task.Delay(delay, token) }

    let private options onDefect =
        { Post = (fun callback -> callback ())
          OnDefect = onDefect
          HistoryCapacity = 64
          Clock = Some(liveClock ())
          LogSink = None }

    let private signal () = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

    let private mark (source: TaskCompletionSource<unit>) : Flow<'env, 'error, unit> =
        Flow.delay (fun () -> source.TrySetResult() |> ignore; Flow.ok ())

    type PipelineEnvironment = { Requests: Queue<string>; Handled: ConcurrentQueue<string> }

    [<Fact>]
    let ``a service runs once as a named fiber and is interrupted when the runtime stops`` () =
        let started = signal ()
        let released = signal ()
        let mutable runs = 0
        use runtime = ElmishRuntime.start (options (fun defect -> failwith defect.Rendered)) ()
        let service : Flow<unit, Never, unit> =
            flow {
                do! Flow.delay (fun () -> Interlocked.Increment(&runs) |> ignore; Flow.ok ())
                do! Flow.scopeFinalizer (mark released)
                do! mark started
                return! Flow.never
            }
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "poller" service)
        Assert.True(started.Task.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () ->
            runtime.Registry.Snapshot() |> List.exists (fun dump -> dump.Name = Some "poller")), 5000))
        Assert.False(released.Task.IsCompleted)
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.True(released.Task.Wait(5000))
        Assert.Equal(1, runs)
        Assert.Equal(0, runtime.Registry.LiveFiberCount)

    [<Fact>]
    let ``a second service with a running name is refused`` () =
        let started = signal ()
        let mutable runs = 0
        use runtime = ElmishRuntime.start (options (fun defect -> failwith defect.Rendered)) ()
        let service : Flow<unit, Never, unit> =
            flow {
                do! Flow.delay (fun () -> Interlocked.Increment(&runs) |> ignore; Flow.ok ())
                do! mark started
                return! Flow.never
            }
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "pipeline" service)
        Assert.Equal(Error ServiceStartError.DuplicateName, runtime |> ElmishRuntime.startService "pipeline" service)
        Assert.True(started.Task.Wait(5000))
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.Equal(1, runs)

    [<Fact>]
    let ``a finished service frees its name`` () =
        let mutable runs = 0
        use runtime = ElmishRuntime.start (options ignore) ()
        let service : Flow<unit, Never, unit> = Flow.delay (fun () -> Interlocked.Increment(&runs) |> ignore; Flow.ok ())
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "warmer" service)
        Assert.True(SpinWait.SpinUntil((fun () -> Volatile.Read(&runs) = 1 && runtime.Pending = 0 && runtime.Registry.LiveFiberCount = 0), 5000))
        Assert.True(SpinWait.SpinUntil((fun () -> runtime |> ElmishRuntime.startService "warmer" service = Ok ()), 5000))
        Assert.True(SpinWait.SpinUntil((fun () -> Volatile.Read(&runs) = 2), 5000))

    [<Fact>]
    let ``a service that completes before stop is reported to OnDefect`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        use runtime = ElmishRuntime.start (options defects.Enqueue) ()
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "pipeline" (Flow.ok ()))
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        let defect = defects.ToArray().[0]
        Assert.Equal("pipeline", defect.Name)
        Assert.True(defect.Error.IsNone)
        Assert.Contains("completed", defect.Rendered)

    [<Fact>]
    let ``a service that completes because the runtime is stopping is not reported`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        let started = signal ()
        use runtime = ElmishRuntime.start (options defects.Enqueue) ()
        let release = signal ()
        // The service ignores cancellation, so it returns normally after Stop has begun, as a pipeline over a queue
        // that shuts down with the runtime would.
        let service : Flow<unit, Never, unit> =
            Flow.fromTask (fun _ ->
                started.TrySetResult() |> ignore
                release.Task)
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "pipeline" service)
        Assert.True(started.Task.Wait(5000))
        let stopping = runtime.Stop() |> Async.StartAsTask
        Assert.True(SpinWait.SpinUntil((fun () -> runtime |> ElmishRuntime.startService "probe" (Flow.ok ()) = Error ServiceStartError.RuntimeStopping), 5000))
        release.TrySetResult() |> ignore
        Assert.True(stopping.Wait(5000))
        Assert.True(SpinWait.SpinUntil((fun () -> runtime.Registry.LiveFiberCount = 0), 5000))
        Assert.Empty(defects)

    [<Fact>]
    let ``a service defect is reported to OnDefect under the service name`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        use runtime = ElmishRuntime.start (options defects.Enqueue) ()
        let service : Flow<unit, Never, unit> = Flow.die (InvalidOperationException "pipeline exploded")
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "pipeline" service)
        Assert.True(SpinWait.SpinUntil((fun () -> defects.Count = 1), 5000))
        Assert.Equal("pipeline", defects.ToArray().[0].Name)
        Assert.Contains("pipeline exploded", defects.ToArray().[0].Rendered)

    [<Fact>]
    let ``interruption at stop is not reported as a defect`` () =
        let defects = ConcurrentQueue<CommandDefect>()
        let started = signal ()
        use runtime = ElmishRuntime.start (options defects.Enqueue) ()
        let service : Flow<unit, Never, unit> =
            flow {
                do! mark started
                return! Flow.never
            }
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "idle" service)
        Assert.True(started.Task.Wait(5000))
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.Empty(defects)

    [<Fact>]
    let ``a service cannot start after stop`` () =
        use runtime = ElmishRuntime.start (options (fun defect -> failwith defect.Rendered)) ()
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.Equal(Error ServiceStartError.RuntimeStopping, runtime |> ElmishRuntime.startService "late" (Flow.ok ()))

    [<Fact>]
    let ``a service uses the environment that startLayer provisioned`` () =
        let layer : Layer<unit, string, PipelineEnvironment> =
            Layer.fromFlow (flow {
                let! requests = Queue.unbounded ()
                return { Requests = requests; Handled = ConcurrentQueue<string>() }
            })
        let provisioned =
            ElmishRuntime.startLayer (options (fun defect -> failwith defect.Rendered)) layer ()
            |> Async.RunSynchronously
        use runtime = match provisioned with Ok runtime -> runtime | Error error -> failwith error
        let pipeline : Flow<PipelineEnvironment, Never, unit> =
            flow {
                let! env = Flow.env
                return!
                    FlowStream.fromDequeue env.Requests
                    |> FlowStream.runForEach (fun request -> env.Handled.Enqueue("handled:" + request))
            }
        Assert.Equal(Ok (), runtime |> ElmishRuntime.startService "pipeline" pipeline)
        Assert.True(Queue.tryOffer "a" runtime.Environment.Requests = QueueTryOfferResult.Accepted)
        Assert.True(SpinWait.SpinUntil((fun () -> runtime.Environment.Handled.Count = 1), 5000))
        Assert.Equal("handled:a", runtime.Environment.Handled.ToArray().[0])
        Assert.True(runtime.StopAndWait(TimeSpan.FromSeconds 5.0))
        Assert.Equal(0, runtime.Registry.LiveFiberCount)
