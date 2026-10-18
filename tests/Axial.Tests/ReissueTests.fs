namespace Axial.Tests

open System
open Axial
open Swensen.Unquote
open Xunit

module ReissueTests =
    [<Fact>]
    let ``Reissue wakes interrupt three runs without retrying`` () =
        let mutable starts = 0
        let events = ResizeArray<string>()
        let mutable failures = 0
        let workflow : Flow<ClockEnvironment, string, int * int * int> =
            flow {
                let! wakes = Queue.unbounded ()
                let! started = Queue.unbounded ()
                let! cleaning = Queue.unbounded ()
                let! permits = Queue.unbounded ()
                let! finish = Deferred.make<ClockEnvironment, string, int> ()
                let work =
                    Flow.delay (fun () ->
                        starts <- starts + 1
                        events.Add(sprintf "started %d" starts)
                        flow {
                            do! started |> Queue.offer starts |> Flow.ignore
                            return! Deferred.await finish
                        })
                    |> Flow.ensuring (flow {
                        let index = starts
                        do! cleaning |> Queue.offer index |> Flow.ignore
                        let! _ = Dequeue.take permits
                        events.Add(sprintf "cleaned %d" index)
                    })
                    |> Flow.mapError (fun error -> failures <- failures + 1; error)
                    |> Flow.retry (Schedule.recurs 5)
                let! running = FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) work |> Flow.fork
                for index in 1..3 do
                    let! observed = Dequeue.take started
                    if observed <> index then failwithf "expected run %d, got %d" index observed
                    do! wakes |> Queue.offer () |> Flow.ignore
                    let! cleaningIndex = Dequeue.take cleaning
                    if cleaningIndex <> index then failwithf "expected cleanup %d" index
                    do! permits |> Queue.offer () |> Flow.ignore
                let! _fourth = Dequeue.take started
                do! Deferred.succeed 42 finish |> Flow.ignore
                let! _ = Dequeue.take cleaning
                do! permits |> Queue.offer () |> Flow.ignore
                let! value = Fiber.join running
                return value, starts, failures
            }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(42, 4, 0) @>
        test <@ Seq.toList events = [ "started 1"; "cleaned 1"; "started 2"; "cleaned 2"; "started 3"; "cleaned 3"; "started 4"; "cleaned 4" ] @>

    [<Fact>]
    let ``Reissue propagates a typed failure to retry`` () =
        let mutable failures = 0
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! wakes = Queue.unbounded ()
                let work = Flow.fail "broken" |> Flow.mapError (fun error -> failures <- failures + 1; error)
                return! FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) (work |> Flow.retry (Schedule.recurs 0))
            }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Failure(Cause.Fail "broken") @>
        test <@ failures = 1 @>

    [<Fact>]
    let ``Reissue timeout closes the current run`` () =
        let mutable stopped = 0
        let workflow : Flow<ClockEnvironment, string, _> =
            flow {
                let! wakes = Queue.unbounded ()
                let! gate = Deferred.make<ClockEnvironment, string, int> ()
                let work = Deferred.await gate |> Flow.ensuring (Flow.delay (fun () -> stopped <- stopped + 1; Flow.ok ()))
                return! FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) work
                        |> Flow.timeout (TimeSpan.FromMilliseconds 20.0) "timed out"
            }
        let result = Flow.runSync (TestSupport.clockEnv ()) workflow
        test <@ stopped = 1 @>
        test <@ result = Exit.Failure(Cause.Fail "timed out") @>

    [<Fact>]
    let ``Wake failure propagates`` () =
        let wake : FlowStream<ClockEnvironment, string, unit> = FlowStream.fromFlow (Flow.fail "wake failed")
        let workflow = FlowStream.reissueOnWake wake (Flow.never<ClockEnvironment, string, int>)
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Failure(Cause.Fail "wake failed") @>

    [<Fact>]
    let ``Ended wake allows current run to finish`` () =
        let wake : FlowStream<ClockEnvironment, string, unit> = FlowStream.empty
        let workflow = FlowStream.reissueOnWake wake (Flow.ok 23)
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 23 @>

    [<Fact>]
    let ``Reissue returns initial result without wakes`` () =
        let workflow : Flow<ClockEnvironment, string, int> =
            flow {
                let! wakes = Queue.unbounded ()
                return! FlowStream.reissueOnWake (FlowStream.fromDequeue wakes) (Flow.ok 17)
            }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 17 @>
