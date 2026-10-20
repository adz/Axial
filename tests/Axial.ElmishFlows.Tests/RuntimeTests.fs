namespace Axial.ElmishFlows.Tests

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.ElmishFlows
open Xunit

module RuntimeTests =
    let private liveClock () =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = TimeSpan.FromTicks(Environment.TickCount64)
            member _.Sleep(delay, token) = Task.Delay(delay, token) }

    let private options post onDefect =
        { Post = post; OnDefect = onDefect; HistoryCapacity = 32 }

    type CacheEnvironment = { Clock: IClock; Cache: Cache<int, Never, string> } with
        interface IHasClock with
            member this.Clock = this.Clock

    [<Fact>]
    let ``startLayer provisions a cache and release flow until stop`` () =
        let messages = ConcurrentQueue<string>()
        let released = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let cacheLayer : Layer<ClockEnvironment, string, CacheEnvironment> =
            Layer.fromFlow (flow {
                let! cache =
                    Cache.make (fun (key: int) ->
                        Flow.succeed $"value-{key}" : Flow<ClockEnvironment, Never, string>)
                do! Flow.scopeFinalizer (Flow.fromTask (fun _ ->
                    released.TrySetResult() |> ignore
                    Task.FromResult(())))
                let! clock = Flow.envWith (fun (env: ClockEnvironment) -> env.Clock)
                return { Clock = clock; Cache = cache }
            })
        let result =
            ElmishRuntime.startLayer
                (options (fun callback -> callback ()) (fun defect -> failwith defect.Rendered))
                cacheLayer
                (ClockEnvironment(liveClock ()))
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
        let failing : Layer<ClockEnvironment, string, ClockEnvironment> =
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
                (ClockEnvironment(liveClock ()))
            |> Async.RunSynchronously
        match result with
        | Ok _ -> failwith "startLayer should have returned Error"
        | Error error -> Assert.Equal("provision failed", error)
        Assert.True(released.Task.Wait(5000))
